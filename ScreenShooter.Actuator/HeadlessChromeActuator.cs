using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using NLog;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using ScreenShooter.Helper;

namespace ScreenShooter.Actuator
{
    public class HeadlessChromeActuator : IActuator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Browser _browser;

        private bool _requireNewBrowserInstance = false;

        public HeadlessChromeActuator()
        {
            DownloadBrowser();
        }

        public HeadlessChromeActuator(bool autoDownload)
        {
            if (autoDownload) DownloadBrowser();
        }

        public int WindowWidth { get; set; } = 1920;
        public int WindowHeight { get; set; } = 1080;
        public int PageDownloadTimeout { get; set; } = 30000;
        public int PageScrollActionWaitDelay { get; set; } = 2000;
        public int ExtraDownloadWaitDelay { get; set; } = 10000;
        public int MaxTitlePrependLength { get; set; } = 32;

        private async Task NewBrowser()
        {
            Logger.Debug("Launch browser");
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                DumpIO = true,
            });
        }

        public async Task<ExecutionResult> CapturePage(string url, Guid sessionId)
        {
            Logger.Debug($"Enter CapturePage() for session {sessionId}");

            var ret = new ExecutionResult
            {
                Identifier = sessionId,
                StatusText = "",
                Attachments = null,
                HasPotentialUnfinishedDownloads = true
            };

            if (_browser == null || _browser.IsClosed || _requireNewBrowserInstance)
            {
                await NewBrowser();
            }
            
            Logger.Debug("Create incognito session");
            var context = await _browser.CreateIncognitoBrowserContextAsync();
            Logger.Debug("Create new tab");
            var page = await context.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = WindowWidth,
                Height = WindowHeight
            });
            Logger.Debug($"Navigate to \"{url}\"");
            try
            {
                await page.GoToAsync(
                    url,
                    PageDownloadTimeout,
                    new[] {WaitUntilNavigation.Networkidle0}
                );
            }
            catch (WaitTaskTimeoutException)
            {
                // TODO: time exceeded
                Logger.Warn($"Page loading time exceeded for url \"{url}\"");
                ret.HasPotentialUnfinishedDownloads = false;
            }
            catch (NavigationException)
            {
                Logger.Warn($"Document download time exceeded for url \"{url}\"");
                ret.HasPotentialUnfinishedDownloads = false;
            }

            ret.Url = page.Url;
            ret.Title = await page.GetTitleAsync();
            var prefix =
                Path.Escape(ret.Title.Substring(0, Math.Min(MaxTitlePrependLength, ret.Title.Length)) + "-" + sessionId);

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
                return new ExecutionResult()
                {
                    Identifier = sessionId,
                    StatusText = "Failed to download web page",
                    HasPotentialUnfinishedDownloads = true,
                };
            }

            var attachments = new List<string>();
            // screen shot
            try
            {
                Logger.Debug("Saving PDF");
                await page.PdfAsync($"{prefix}.pdf", new PdfOptions()
                {
                    HeaderTemplate = "<title /> - <date />",
                    FooterTemplate =
                        "<url /> - Captured by ScreenShooter - https://github.com/Jamesits/ScreenShooter - <pageNumber />/<totalPages />",
                    DisplayHeaderFooter = true,
                    PrintBackground = true,
                    Format = PaperFormat.A4,
                    MarginOptions = new MarginOptions()
                    {
                        Bottom = "0.5in",
                        Top = "0.5in",
                        Left = "0.3in",
                        Right = "0.3in",
                    },
                });
                attachments.Add($"{prefix}.pdf");
            }
            catch (TargetClosedException e)
            {
                // possibility out of memory, see https://github.com/Jamesits/ScreenShooter/issues/1
                ret.StatusText += "Possible out of memory when requesting PDF\n";
                Logger.Error($"Something happened on requesting PDF. \n\nException:\n{e}\n\nInnerException:{e?.InnerException}");
                _requireNewBrowserInstance = true;
            }

            try
            {
                Logger.Debug("Taking screenshot");
                await page.ScreenshotAsync($"{prefix}.png", new ScreenshotOptions
                {
                    FullPage = true
                });
                attachments.Add($"{prefix}.png");
            }
            catch (TargetClosedException e)
            {
                // possibility out of memory, see https://github.com/Jamesits/ScreenShooter/issues/1
                ret.StatusText += "Possible out of memory when requesting screenshot\n";
                Logger.Error($"Something happened on requesting screenshot. \n\nException:\n{e}\n\nInnerException:{e?.InnerException}");
                _requireNewBrowserInstance = true;
            }

            ret.Attachments = attachments.ToArray();

            // clean up
            try
            {
                Logger.Debug("Close tab");
                await page.CloseAsync();
                Logger.Debug("Kill context");
                await context.CloseAsync();
            }
            catch (Exception)
            {
                Logger.Error($"Something happened on cleaning up. \n\nException:\n{e}\n\nInnerException:{e?.InnerException}");
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

        private void DownloadBrowser()
        {
            var browserFetcher = new BrowserFetcher();
            browserFetcher.DownloadProgressChanged += (sender, e) =>
            {
                Logger.Debug(
                    $"Browser download progress {e.ProgressPercentage}% - {e.BytesReceived}/{e.TotalBytesToReceive}bytes");
            };

            Logger.Debug("Download browser");
            AsyncHelper.RunSync(() => browserFetcher.DownloadAsync(BrowserFetcher.DefaultRevision));
        }
    }
}