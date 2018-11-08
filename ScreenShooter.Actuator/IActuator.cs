using System;
using System.Threading.Tasks;
using Nett;

namespace ScreenShooter.Actuator
{
    public interface IActuator
    {
        Task CreateSession(string url, Guid sessionId);
        Task<ExecutionResult> CapturePage();
        Task DestroySession();
    }
}
