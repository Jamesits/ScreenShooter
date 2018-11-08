using System;

namespace ScreenShooter.IO
{
    interface IConnector
    {
        event EventHandler NewRequest;
    }

}
