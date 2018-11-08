using System;
using System.Threading.Tasks;
using Nett;
using PuppeteerSharp;

namespace ScreenShooter.Actuator
{
    public class HeadlessChromeActuator : IActuator
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private Guid _sessionId;

        private BrowserFetcher _browserFetcher;
        private Browser _browser;
        private Page _page;

        private bool _hasDownloadSucceed = true;

        public int WindowWidth { get; set; } = 1920;
        public int WindowHeight { get; set; } = 1080;
        public int PageDownloadTimeout { get; set; } = 30000;
        public int PageScrollActionWaitDelay { get; set; } = 2000;
        public int ExtraDownloadWaitDelay { get; set; } = 10000;
        public int MaxTitlePrependLength { get; set; } = 32;

        public async Task CreateSession(string url, Guid sessionId)
        {
            Logger.Debug($"Enter CreateSession() for session {sessionId}");

            _sessionId = sessionId;

            _browserFetcher = new BrowserFetcher();
            _browserFetcher.DownloadProgressChanged += (sender, e) =>
            {
                Logger.Debug($"Browser download progress {e.ProgressPercentage}% - {e.BytesReceived}/{e.TotalBytesToReceive}bytes");
            };

            Logger.Debug("Download browser");
            await _browserFetcher.DownloadAsync(BrowserFetcher.DefaultRevision);
            
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
                    timeout: PageDownloadTimeout,
                    waitUntil: new[] { WaitUntilNavigation.Networkidle0 }
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

            Logger.Debug("Exit CreateSession()");
        }

        public async Task<ExecutionResult> CapturePage()
        {
            string title = await _page.GetTitleAsync();
            string prefix = ScreenShooter.Helper.Path.Escape(title.Substring(0, Math.Min(MaxTitlePrependLength, title.Length)) + "-" + _sessionId);

            Logger.Debug("Taking screenshot");
            await _page.ScreenshotAsync($"{prefix}.png", new ScreenshotOptions()
            {
                FullPage = true
            });
            Logger.Debug("Saving PDF");
            await _page.PdfAsync($"{prefix}.pdf");
            return new ExecutionResult()
            {
                Identifier = _sessionId,
                StatusText = "",
                Title = title,
                Url = _page.Url,
                Attachments = new []
                {
                    $"{prefix}.png",
                    $"{prefix}.pdf",
                },
                HasPotentialUnfinishedDownloads = _hasDownloadSucceed,
            };
        }

        public async Task DestroySession()
        {
            Logger.Debug("Enter DestroySession()");
            Logger.Debug("Close tab");
            await _page.CloseAsync();
            Logger.Debug("Close browser");
            await _browser.CloseAsync();
            _page = null;
            _browser = null;
            Logger.Debug("Exit DestroySession()");
        }
    }
}
