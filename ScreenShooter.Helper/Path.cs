using System.Linq;

namespace ScreenShooter.Helper
{
    public static class Path
    {
        public static string Escape(string s)
        {
            var invalidFileChars = System.IO.Path.GetInvalidFileNameChars();
            return string.Join("", s.Where(x => invalidFileChars.Any(y => x != y)).ToArray());
        }
    }
}
