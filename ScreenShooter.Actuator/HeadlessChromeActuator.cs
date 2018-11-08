using System;
using System.Threading.Tasks;
using NLog;
using PuppeteerSharp;
using ScreenShooter.Helper;

namespace ScreenShooter.Actuator
{
    public class HeadlessChromeActuator : IActuator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private Browser _browser;

        private bool _hasDownloadSucceed = true;
        private Page _page;

        private Guid _sessionId;

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

            _sessionId = sessionId;

            Logger.Debug("Launch browser");
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });
            Logger.Debug("Create new tab");
            _page = await _browser.NewPageAsync();
            await _page.SetViewportAsync(new ViewPortOptions
            {
                Width = WindowWidth,
                Height = WindowHeight
            });
            Logger.Debug($"Navigate to {url}");
            try
            {
                await _page.GoToAsync(
                    url,
                    PageDownloadTimeout,
                    new[] {WaitUntilNavigation.Networkidle0}
                );
            }
            catch (WaitTaskTimeoutException)
            {
                // TODO: time exceeded
                Logger.Warn("Page loading time exceeded.");
                _hasDownloadSucceed = false;
            }

            Logger.Debug("Trying to load lazy-loading elements");
            // https://www.screenshotbin.com/blog/handling-lazy-loaded-webpages-puppeteer
            var bodyHandle = await _page.QuerySelectorAsync("body");
            var boundingBox = await bodyHandle.BoundingBoxAsync();
            var viewportHeight = _page.Viewport.Height;
            var viewportIncr = 0;

            // scroll down
            while (viewportIncr + viewportHeight < boundingBox.Height)
            {
                await _page.EvaluateExpressionAsync($"window.scrollBy(0, {viewportHeight})");
                await _page.WaitForTimeoutAsync(PageScrollActionWaitDelay);
                viewportIncr += viewportHeight;
            }

            // scroll up
            await _page.EvaluateExpressionAsync("window.scrollTo(0, 0)");
            await _page.WaitForTimeoutAsync(ExtraDownloadWaitDelay);


            // screen shot
            var title = await _page.GetTitleAsync();
            var prefix =
                Path.Escape(title.Substring(0, Math.Min(MaxTitlePrependLength, title.Length)) + "-" + _sessionId);

            Logger.Debug("Taking screenshot");
            await _page.ScreenshotAsync($"{prefix}.png", new ScreenshotOptions
            {
                FullPage = true
            });
            Logger.Debug("Saving PDF");
            await _page.PdfAsync($"{prefix}.pdf");
            var ret = new ExecutionResult
            {
                Identifier = _sessionId,
                StatusText = "",
                Title = title,
                Url = _page.Url,
                Attachments = new[]
                {
                    $"{prefix}.png",
                    $"{prefix}.pdf"
                },
                HasPotentialUnfinishedDownloads = _hasDownloadSucceed
            };

            // clean up
            Logger.Debug("Close tab");
            await _page.CloseAsync();
            Logger.Debug("Close browser");
            await _browser.CloseAsync();
            _page = null;
            _browser = null;

            Logger.Debug("Exit CapturePage()");
            return ret;
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