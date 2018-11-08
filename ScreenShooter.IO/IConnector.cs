using System;

namespace ScreenShooter.IO
{
    interface IRequestConnector
    {
        event EventHandler NewRequest;
    }

    interface IResponseConnector
    {

    }
}
