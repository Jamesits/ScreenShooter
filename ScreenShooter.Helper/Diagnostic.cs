using System;
using System.Diagnostics;
using System.Text;

namespace ScreenShooter.Helper
{
    public static class RuntimeInformation
    {
        private static Process _self = Process.GetCurrentProcess();
        public static TimeSpan TotalProcessorTime => _self.TotalProcessorTime;
        public static TimeSpan SystemProcessorTime => _self.PrivilegedProcessorTime;
        public static TimeSpan UserProcessorTime => _self.UserProcessorTime;
        public static long WorkingSet => _self.WorkingSet64;
        public static long PeakWorkingSet => _self.PeakWorkingSet64;

        public static string Os => System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        public static string Framework => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        public static string Hostname => System.Environment.MachineName;
        public static string Username => System.Environment.UserDomainName;

        public static ulong QueuedRequests { get; set; } = 0;
        public static ulong OnGoingRequests { get; set; } = 0;
        public static ulong FinishedRequests { get; set; } = 0;
        public static ulong FailedRequests { get; set; } = 0;

        public new static string ToString()
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

            sb.AppendFormat("Session requests: q{0} | r{1} | s{2} | f{3}\n", QueuedRequests, OnGoingRequests, FinishedRequests, FailedRequests);

            return sb.ToString();
        }
    }
}
