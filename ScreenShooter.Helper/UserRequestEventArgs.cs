using System;
using System.Collections.Generic;

namespace ScreenShooter.Helper
{
    public enum UserRequestType
    {
        Png,
        Pdf,
        DownloadFile
    }

    public class UserRequestEventArgs : EventArgs
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Url { get; set; }
        public object Requester { get; set; }
        public object RequestContext { get; set; }
        public bool IsPriority { get; set; }
        public List<UserRequestType> RequestTypes { get; set; }
    }

    public delegate void UserRequestEventHandler(object sender, UserRequestEventArgs e);
}
