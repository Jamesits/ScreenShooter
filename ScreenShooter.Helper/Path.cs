using System.Linq;
using System.Text;
using NLog;

namespace ScreenShooter.Helper
{
    public static class Path
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static readonly char[] InvalidFileChars = System.IO.Path.GetInvalidFileNameChars();
        public static readonly char[] FilteredChars = {'/', '\\', ',', ':', '"', '\'', ' ', '.'};
        public static string Escape(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var c in s)
            {
                if (!(InvalidFileChars.Contains(c) || FilteredChars.Contains(c)))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }
            Logger.Trace($"Escaped filename {s} into {sb}");
            return sb.ToString();
        }

        public static string ConcentrateFilename(string originFileName, string identifier, int originalFileNameLimit=32)
        {
            var ext = Escape(System.IO.Path.GetExtension(originFileName));
            var name = Escape(System.IO.Path.GetFileNameWithoutExtension(originFileName).Substring(0, originalFileNameLimit));
            return name + '-' + identifier + ext;
        }
    }
}