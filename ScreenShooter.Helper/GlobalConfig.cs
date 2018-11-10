namespace ScreenShooter.Helper
{
    public class GlobalConfig
    {
        public bool AggressiveGc { get; set; } = false;
        public long LowMemoryAddMemoryPressure { get; set; } = 0;
        public int ParallelJobs { get; set; } = 1;
    }
}
