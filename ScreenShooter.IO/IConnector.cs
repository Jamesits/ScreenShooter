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
        Task SendResult(ExecutionResult result);
        Task DestroySession();

    }

}
