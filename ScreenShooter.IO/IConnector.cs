using System;
using System.Threading.Tasks;
using ScreenShooter.Actuator;

namespace ScreenShooter.IO
{
    public interface IConnector
    {
        event EventHandler NewRequest;
        Task CreateSession();
        Task EventLoop();
        Task SendResult(ExecutionResult result, NewRequestEventArgs originRequestEventArgs);
        Task DestroySession();
    }

    public class NewRequestEventArgs : EventArgs
    {
        public string Url { get; set; }
    }
}