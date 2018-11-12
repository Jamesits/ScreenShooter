using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using NLog;

namespace ScreenShooter.Helper
{
    public static class Url
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static readonly string TldsListUrl = "https://publicsuffix.org/list/public_suffix_list.dat";
        public static string[] Tlds = null;
        private static readonly IdnMapping Mapping = new IdnMapping();

        static Url()
        {
            UpdateValidTldList();
        }

        public static string ToPunyCode(string s)
        {
            return Mapping.GetAscii(s);
        }

        public static void UpdateValidTldList()
        {
            Logger.Debug("Start updating TLDs info");
            var ret = new List<string>();
            var client = new WebClient();
            using (var stream = client.OpenRead(TldsListUrl))
            using (var reader = new StreamReader(stream ?? throw new InvalidOperationException()))
            {
                string line;
                while ((line = reader.ReadLine()?.Trim()) != null)
                {
                    if (line.Length == 0 || line.StartsWith("//")) continue;
                    ret.Add("." + ToPunyCode(line));
                }
            }

            Tlds = ret.ToArray();
            Logger.Debug($"TLDs list updated, total {ret.Count} entries");
        }

        public static bool IsValidUrl(string s)
        {
            var result = Uri.TryCreate(s, UriKind.Absolute, out var uriResult);

            return result && (
                              uriResult.Scheme == Uri.UriSchemeHttp
                              || uriResult.Scheme == Uri.UriSchemeHttps
                              || uriResult.Scheme == Uri.UriSchemeFtp
                          )
                          && uriResult.IsLoopback == false
                          && Tlds.Any(x => uriResult.IdnHost.EndsWith(x))
            ;
        }

        public static string[] ExtractValidUrls(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return new string[0];

            // check if is a single URL
            if (IsValidUrl(s)) return new[] {s};

            var segments = s.Split();
            switch (segments.Length)
            {
                case 0:
                    return new string[0];
                case 1:
                {
                    // try if it can be extended to be a valid URL
                    var orig = segments[0];
                    var urlseg = orig.Split(new []{ '/' }, 2);
                    var host = urlseg.First(x => x.Length > 0);
                    if (Tlds.Any(x => ToPunyCode(host).EndsWith(x)))
                    {
                        return new[] {$"http://{string.Join('/', urlseg)}"};
                    }
                    break;
                }
                default:
                    // try if we can find some URL from the segments
                    return segments.Select(part => part.Trim()).Where(IsValidUrl).ToArray();
            }

            return new string[0];
        }
    }
}
