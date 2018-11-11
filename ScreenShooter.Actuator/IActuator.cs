using System.Threading.Tasks;
using ScreenShooter.Helper;

namespace ScreenShooter.Actuator
{
    public interface IActuator
    {
        UserRequestType[] Capability { get; }
        Task<CaptureResponseEventArgs> CapturePage(object sender, UserRequestEventArgs e);
    }
}