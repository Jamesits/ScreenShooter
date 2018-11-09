using System;
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

        public async Task<ExecutionResult> CapturePage(string url, Guid sessionId)
        {
            Logger.Debug($"Enter CapturePage() for session {sessionId}");

            var hasDownloadSucceed = true;

            if (_browser == null)
            {
                Logger.Debug("Launch browser");
                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true
                });
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
                hasDownloadSucceed = false;
            }
            catch (NavigationException)
            {
                Logger.Warn($"Document download time exceeded for url \"{url}\"");
                hasDownloadSucceed = false;
            }

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
            
            // screen shot
            var title = await page.GetTitleAsync();
            var prefix =
                Path.Escape(title.Substring(0, Math.Min(MaxTitlePrependLength, title.Length)) + "-" + sessionId);

            Logger.Debug("Taking screenshot");
            await page.ScreenshotAsync($"{prefix}.png", new ScreenshotOptions
            {
                FullPage = true
            });
            Logger.Debug("Saving PDF");
            await page.PdfAsync($"{prefix}.pdf", new PdfOptions()
            {
                HeaderTemplate = "<title /> - <date />",
                FooterTemplate = "<url /> - Captured by ScreenShooter - https://github.com/Jamesits/ScreenShooter - <pageNumber />/<totalPages />",
                DisplayHeaderFooter = true,
                Format = PaperFormat.A4,
                MarginOptions = new MarginOptions()
                {
                    Bottom = "0.5in",
                    Top = "0.5in",
                    Left = "0.3in",
                    Right = "0.3in",
                },
            });
            var ret = new ExecutionResult
            {
                Identifier = sessionId,
                StatusText = "",
                Title = title,
                Url = page.Url,
                Attachments = new[]
                {
                    $"{prefix}.png",
                    $"{prefix}.pdf"
                },
                HasPotentialUnfinishedDownloads = hasDownloadSucceed
            };

            // clean up
            Logger.Debug("Close tab");
            await page.CloseAsync();
            Logger.Debug("Kill context");
            await context.CloseAsync();

            Logger.Debug("Exit CapturePage()");
            return ret;
        }

        ~HeadlessChromeActuator()
        {
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