using System;
using System.Threading.Tasks;
using ScreenShooter.Actuator;
using ScreenShooter.Helper;

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