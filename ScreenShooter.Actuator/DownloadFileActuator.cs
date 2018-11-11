using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NLog;
using ScreenShooter.Helper;

namespace ScreenShooter.Actuator
{
    internal class WebClientWithTimeout : WebClient
    {
        private readonly int _timeout;

        public WebClientWithTimeout(int timeout = 30000)
        {
            _timeout = timeout;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var wr = base.GetWebRequest(address);
            wr.Timeout = _timeout; // timeout in milliseconds (ms)
            return wr;
        }
    }

    public class DownloadFileActuator : IActuator
    {
        UserRequestType[] IActuator.Capability => new[] { UserRequestType.DownloadFile };

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public int Timeout { get; set; } = 30000;
        public async Task<CaptureResponseEventArgs> CapturePage(object sender, UserRequestEventArgs e)
        {
            var ret = new CaptureResponseEventArgs()
            {
                Request = e,
                Url = e.Url,
                Title = "",
            };
            var result = Uri.TryCreate(e.Url, UriKind.Absolute, out var uriResult);
            if (!result)
            {
                ret.StatusText += $"Not a valid URL: \"{e.Url}\"";
                return ret;
            }

            var attachments = new List<string>();
            try
            {
                var wc = new WebClientWithTimeout(Timeout);
                var expectedFileName = Path.ConcentrateFilename(uriResult.Segments.Last(), e.Id.ToString());
                await wc.DownloadFileTaskAsync(e.Url, expectedFileName);
                attachments.Append(expectedFileName);

            }
            catch (WebException we)
            {
                Logger.Warn("Something happened.\n" + we);
                ret.StatusText += we;
            }

            ret.Attachments = attachments.ToArray();
            return ret;
        }
    }
}
