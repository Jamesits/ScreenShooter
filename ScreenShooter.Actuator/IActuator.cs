using System.Threading.Tasks;

namespace ScreenShooter.Actuator
{
    interface IActuator
    {
        Task CreateSession(string url, int windowWidth=1920, int windowHeight=1080);
        Task CaptureImage(string fileName);
        Task CapturePdf(string fileName);
        Task DestroySession();
    }
}
