using System;
using System.Threading.Tasks;

namespace ScreenShooter.Actuator
{
    interface IActuator
    {
        Task CreateSession(string url, Guid sessionId, int windowWidth=1920, int windowHeight=1080);
        Task<ExecutionResult> CapturePage();
        Task DestroySession();
    }
}
