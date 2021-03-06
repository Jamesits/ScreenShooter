﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NLog;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using ScreenShooter.Helper;
using System.Runtime.InteropServices;
using RuntimeInformation = System.Runtime.InteropServices.RuntimeInformation;

namespace ScreenShooter.Actuator
{
    public class HeadlessChromeActuator : IActuator
    {
        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, int mode);

        // user permissions
        const int S_IRUSR = 0x100;
        const int S_IWUSR = 0x80;
        const int S_IXUSR = 0x40;

        // group permission
        const int S_IRGRP = 0x20;
        const int S_IWGRP = 0x10;
        const int S_IXGRP = 0x8;

        // other permissions
        const int S_IROTH = 0x4;
        const int S_IWOTH = 0x2;
        const int S_IXOTH = 0x1;

        UserRequestType[] IActuator.Capability => new [] { UserRequestType.Pdf, UserRequestType.Png };
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Browser _browser;

        private bool _requireNewBrowserInstance;

        private readonly LaunchOptions _defaultBrowserLaunchOptions = new LaunchOptions
        {
            Headless = true,
            DumpIO = true,
            Args = new[] { "--no-sandbox" },
        };

        public HeadlessChromeActuator() : this(true)
        {
        }

        public HeadlessChromeActuator(bool autoDownload)
        {
            if (autoDownload) DownloadBrowser();
        }

        public int WindowWidth { get; set; } = 1920;
        public int WindowHeight { get; set; } = 1080;
        public int PageDownloadTimeout { get; set; } = 30000;
        public int DocumentLoadDelay { get; set; } = 500;
        public int PageScrollActionWaitDelay { get; set; } = 2000;
        public int ExtraDownloadWaitDelay { get; set; } = 10000;
        public int MaxTitlePrependLength { get; set; } = 32;

        public virtual async Task<Browser> NewBrowser()
        {
            Logger.Debug("Launch browser");
            return await Puppeteer.LaunchAsync(_defaultBrowserLaunchOptions);
        }

        public async Task<CaptureResponseEventArgs> CapturePage(object sender, UserRequestEventArgs e)
        {
            Logger.Debug($"Enter CapturePage() for session {e.Id}");

            var ret = new CaptureResponseEventArgs
            {
                Request = e,
                StatusText = "",
                Attachments = null,
                HasPotentialUnfinishedDownloads = true
            };

            var currentSessionBrowser = await NewBrowser();

            // create a new session
            Logger.Debug("Create incognito session");
            var context = await currentSessionBrowser.CreateIncognitoBrowserContextAsync();
            Logger.Debug("Create new tab");
            var page = await context.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = WindowWidth,
                Height = WindowHeight
            });

            // load document
            Logger.Debug($"Navigate to \"{e.Url}\"");
            try
            {
                await page.GoToAsync(
                    e.Url,
                    PageDownloadTimeout,
                    new[] {WaitUntilNavigation.Networkidle0}
                );
            }
            catch (WaitTaskTimeoutException)
            {
                // TODO: time exceeded
                Logger.Warn($"Page loading time exceeded for url \"{e.Url}\"");
                ret.StatusText += $"Page loading time exceeded for url \"{e.Url}\"\n";
                ret.HasPotentialUnfinishedDownloads = false;
            }
            catch (NavigationException)
            {
                Logger.Warn($"Document download time exceeded for url \"{e.Url}\"");
                ret.StatusText += $"Document download time exceeded for url \"{e.Url}\"\n";
                ret.HasPotentialUnfinishedDownloads = false;
            }

            await page.WaitForTimeoutAsync(DocumentLoadDelay);

            ret.Url = page.Url;
            ret.Title = await page.GetTitleAsync();
            var prefix =
                Path.Escape(ret.Title.Substring(0, Math.Min(MaxTitlePrependLength, ret.Title.Length)) + "-" + e.Id);

            // scroll through the page to load any lazy-loading elements
            Logger.Debug("Trying to load lazy-loading elements");
            try
            {
                // https://www.screenshotbin.com/blog/handling-lazy-loaded-webpages-puppeteer
                var bodyHandle = await page.QuerySelectorAsync("body");
                var boundingBox = await bodyHandle.BoundingBoxAsync();
                var viewportHeight = page.Viewport.Height;
                var viewportIncr = 0;

                // scroll down
                while (viewportIncr + viewportHeight < boundingBox.Height)
                {
                    await page.EvaluateExpressionAsync($"window.scrollBy(0, {viewportHeight})");
                    await page.WaitForTimeoutAsync(PageScrollActionWaitDelay);
                    viewportIncr += viewportHeight;
                }

                // scroll up
                await page.EvaluateExpressionAsync("window.scrollTo(0, 0)");
                await page.WaitForTimeoutAsync(ExtraDownloadWaitDelay);
            }
            catch (NullReferenceException)
            {
                // body cannot be downloaded
                Logger.Error("Body is missing, either your URL contains a redirection or there is a network issue");
                ret.StatusText += "Failed to download web page\n";
                ret.HasPotentialUnfinishedDownloads = true;
                return ret;
            }

            var attachments = new List<string>();

            if (e.RequestTypes.Contains(UserRequestType.Pdf))
            { // capture PDF
                try
                {
                    Logger.Debug("Saving PDF");
                    await page.PdfAsync($"{prefix}.pdf", new PdfOptions
                    {
                        // TODO: header and footer is not in the correct position, ignore for now
                        // HeaderTemplate = "<title /> - <date />",
                        // FooterTemplate =
                        //    "<url /> - Captured by ScreenShooter - https://github.com/Jamesits/ScreenShooter - <pageNumber />/<totalPages />",
                        DisplayHeaderFooter = true,
                        PrintBackground = true,
                        Format = PaperFormat.A4,
                        MarginOptions = new MarginOptions
                        {
                            Bottom = "0.5in",
                            Top = "0.5in",
                            Left = "0.3in",
                            Right = "0.3in"
                        }
                    });
                    attachments.Add($"{prefix}.pdf");
                }
                catch (TargetClosedException ex)
                {
                    // possibility out of memory, see https://github.com/Jamesits/ScreenShooter/issues/1
                    ret.StatusText += "Possible out of memory when requesting PDF\n";
                    Logger.Error($"Something happened on requesting PDF. \n\nException:\n{ex}\n\nInnerException:{ex.InnerException}");
                    _requireNewBrowserInstance = true;
                }
            }

            if (e.RequestTypes.Contains(UserRequestType.Png))
            { // capture screenshot
                try
                {
                    Logger.Debug("Taking screenshot");
                    await page.ScreenshotAsync($"{prefix}.png", new ScreenshotOptions
                    {
                        FullPage = true
                    });
                    attachments.Add($"{prefix}.png");
                }
                catch (TargetClosedException ex)
                {
                    // possibility out of memory, see https://github.com/Jamesits/ScreenShooter/issues/1
                    ret.StatusText += "Possible out of memory when requesting screenshot\n";
                    Logger.Error($"Something happened on requesting screenshot. \n\nException:\n{ex}\n\nInnerException:{ex.InnerException}");
                    _requireNewBrowserInstance = true;
                }
            }
            
            ret.Attachments = attachments.ToArray();

            // clean up
            try
            {
                Logger.Debug("Close tab");
                await page.CloseAsync();
                Logger.Debug("Kill context");
                await context.CloseAsync();

                Logger.Debug("Closing browser");
                await currentSessionBrowser.CloseAsync();
            }
            catch (Exception ex)
            {
                Logger.Error($"Something happened on cleaning up. \n\nException:\n{ex}\n\nInnerException:{ex.InnerException}");
            }

            Logger.Debug("Exit CapturePage()");
            return ret;
        }

        ~HeadlessChromeActuator()
        {
            if (_browser == null) return;
            Logger.Debug("Close browser");
            AsyncHelper.RunSync(_browser.CloseAsync);
            _browser = null;
        }

        private static void DownloadBrowser()
        {
            var browserFetcher = new BrowserFetcher();
            browserFetcher.DownloadProgressChanged += (sender, e) =>
            {
                Logger.Debug(
                    $"Browser download progress {e.ProgressPercentage}% - {e.BytesReceived}/{e.TotalBytesToReceive}bytes");
            };

            Logger.Debug("Download browser");
            AsyncHelper.RunSync(() => browserFetcher.DownloadAsync(BrowserFetcher.DefaultRevision));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var executablePath = browserFetcher.GetExecutablePath(BrowserFetcher.DefaultRevision);
                const int _0755 =
                S_IRUSR | S_IXUSR | S_IWUSR
                | S_IRGRP | S_IXGRP
                | S_IROTH | S_IXOTH;
                chmod(executablePath, _0755);
            }

            Logger.Debug("Browser downloaded");
        }
    }
}