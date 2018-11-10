using System.Threading.Tasks;
using NLog;
using ScreenShooter.Helper;

namespace ScreenShooter.IO
{
    public class NullConnector : IConnector
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
#pragma warning disable CS0067
        public event UserRequestEventHandler NewRequest;
#pragma warning restore CS0067

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task CreateSession()

        {
        }

        public async Task EventLoop()
        {
        }

        public async Task SendResult(object sender, CaptureResponseEventArgs e)
        {
            Logger.Info("Request finished.");
            if (e.Attachments != null)
            {
                foreach (var item in e.Attachments)
                {
                    Logger.Info($"Attachment: {item}");
                }
            }
        }

        public async Task DestroySession()
        {
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
