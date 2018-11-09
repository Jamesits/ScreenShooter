using System.Linq;
using System.Text;

namespace ScreenShooter.Helper
{
    public static class Path
    {
        public static readonly char[] InvalidFileChars = System.IO.Path.GetInvalidFileNameChars();
        public static readonly char[] FilteredChars = {'/', '\\', ',', ':', '"', '\''};
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
            return sb.ToString();
        }
    }
}