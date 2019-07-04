using NLog;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ScreenShooter.Actuator
{
    public class RemoteChromeActuator : HeadlessChromeActuator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string BrowserWSEndpoint { get; set; } = "wss://chrome.browserless.io/";

        public RemoteChromeActuator() : this(false)
        {
        }

        public RemoteChromeActuator(bool autoDownload) : base(false)
        {

        }

        public override async Task<Browser> NewBrowser()
        {
            Logger.Debug("Launch browser");
            var options = new ConnectOptions()
            {
                BrowserWSEndpoint = BrowserWSEndpoint
            };
            return await Puppeteer.ConnectAsync(options);
        }
    }
}
