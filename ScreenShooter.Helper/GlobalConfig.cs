using System;
using System.Collections.Generic;
using System.Text;

namespace ScreenShooter
{
    public class GlobalConfig
    {
        public bool AggressiveGc { get; set; } = false;
        public long LowMemoryAddMemoryPressure { get; set; } = 0;
    }
}
