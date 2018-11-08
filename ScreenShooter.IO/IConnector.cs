using System;
using System.Collections.Generic;
using System.Text;

namespace ScreenShooter.IO
{
    interface IConnector
    {
        event EventHandler NewRequest;
    }
}
