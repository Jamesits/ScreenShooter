using System.Linq;
using System.Text;

namespace ScreenShooter.Helper
{
    public static class Path
    {
        public static string Escape(string s)
        {
            var invalidFileChars = System.IO.Path.GetInvalidFileNameChars();

            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (!(invalidFileChars.Contains(c) || @"\/".ToCharArray().Contains(c)))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}