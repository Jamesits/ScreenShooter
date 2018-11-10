using ScreenShooter.Helper;
using System.Threading.Tasks;

namespace ScreenShooter.IO
{
    public interface IConnector
    {
        event UserRequestEventHandler NewRequest;
        Task CreateSession();
        Task EventLoop();
        Task SendResult(object sender, CaptureResponseEventArgs e);
        Task DestroySession();
    }
}