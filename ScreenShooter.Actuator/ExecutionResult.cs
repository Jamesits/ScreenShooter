using System;
using System.Text;

namespace ScreenShooter.Actuator
{
    public class ExecutionResult
    {
        public Guid Identifier { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public string StatusText { get; set; }
        public string[] Attachments { get; set; }
        public bool HasPotentialUnfinishedDownloads { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ScreenShooter.Actuator.ExecutionResult");
            sb.AppendFormat("Session ID = {0}\n", Identifier);
            sb.AppendFormat("Capture URL = {0}\n", Url);
            sb.AppendFormat("Page Title = {0}\n", Title);
            sb.AppendFormat("Actuator Status = {0}\n", StatusText);
            sb.AppendLine("Attachments:");
            if (Attachments != null)
            foreach (var attachment in Attachments) sb.AppendFormat("\t{0}\n", attachment);

            sb.AppendFormat("Is Download Finished = {0}", HasPotentialUnfinishedDownloads);

            return sb.ToString();
        }
    }
}