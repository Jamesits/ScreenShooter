using ScreenShooter.Helper;
using System.Threading.Tasks;

namespace ScreenShooter.Actuator
{
    public interface IActuator
    {
        Task<CaptureResponseEventArgs> CapturePage(object sender, UserRequestEventArgs e);
    }
}