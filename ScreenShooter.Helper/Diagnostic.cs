using System;
using System.Diagnostics;
using System.Text;

namespace ScreenShooter.Helper
{
    public class RuntimeInformation
    {
        private readonly Process _self = Process.GetCurrentProcess();
        public TimeSpan TotalProcessorTime => _self.TotalProcessorTime;
        public TimeSpan SystemProcessorTime => _self.PrivilegedProcessorTime;
        public TimeSpan UserProcessorTime => _self.UserProcessorTime;
        public long WorkingSet => _self.WorkingSet64;
        public long PeakWorkingSet => _self.PeakWorkingSet64;

        public string Os => System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        public string Framework => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        public string Hostname => System.Environment.MachineName;
        public string Username => System.Environment.UserDomainName;

        public static ulong OnGoingRequests { get; set; } = 0;
        public static ulong FinishedRequests { get; set; } = 0;
        public static ulong FailedRequests { get; set; } = 0;

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("RuntimeInformation:");

            sb.AppendFormat("OS: {0}\n", Os);
            sb.AppendFormat("Framework: {0}\n", Framework);
            sb.AppendFormat("Hostname: {0}\n", Hostname);
            sb.AppendFormat("Username: {0}\n", Username);

            sb.AppendFormat("Current MEM: {0}MiB\n", WorkingSet / 1048576);
            sb.AppendFormat("Peak MEM: {0}MiB\n", PeakWorkingSet / 1048576);
            sb.AppendFormat("real {0}s\n", TotalProcessorTime);
            sb.AppendFormat("user {0}s\n", UserProcessorTime);
            sb.AppendFormat("sys {0}s\n", SystemProcessorTime);

            sb.AppendFormat("Session requests: r{0} | s{0} | f{0}\n", OnGoingRequests, FinishedRequests, FailedRequests);

            return sb.ToString();
        }
    }
}
