using System;
using System.Threading.Tasks;

namespace ScreenShooter.Actuator
{
    public interface IActuator
    {
        Task<ExecutionResult> CapturePage(string url, Guid sessionId);
    }
}