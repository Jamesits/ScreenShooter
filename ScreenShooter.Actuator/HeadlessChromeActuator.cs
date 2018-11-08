using System;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace ScreenShooter.Actuator
{
    public class HeadlessChromeActuator : IActuator
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private BrowserFetcher _browserFetcher;
        private Browser _browser;
        private Page _page;

        public async Task CreateSession(string url, int windowWidth=1920, int windowHeight=1080)
        {
            Logger.Debug("Enter CreateSession()");
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
                Width = windowWidth,
                Height = windowHeight
            });
            Logger.Debug($"Navigate to {url}");
            try
            {
                await _page.GoToAsync(
                    url,
                    timeout: 30000,
                    waitUntil: new[] { WaitUntilNavigation.Networkidle0 }
                );
            }
            catch (WaitTaskTimeoutException)
            {
                // TODO: time exceeded
                Logger.Warn("Page loading time exceeded.");
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
                await _page.WaitForTimeoutAsync(2000);
                viewportIncr += viewportHeight;
            }

            // scroll up
            await _page.EvaluateExpressionAsync("window.scrollTo(0, 0)");
            await _page.WaitForTimeoutAsync(10000);

            Logger.Debug("Exit CreateSession()");
        }

        public async Task CaptureImage(string fileName)
        {
            Logger.Debug($"Take screenshot to {fileName}");
            await _page.ScreenshotAsync(fileName, new ScreenshotOptions()
            {
                FullPage = true
            });
        }

        public async Task CapturePdf(string fileName)
        {
            Logger.Debug($"Save PDF to {fileName}");
            await _page.PdfAsync(fileName);
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
