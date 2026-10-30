using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ClipDrop.Core;

public static class ImageSourceHints
{
    private static readonly Regex Image = new("<img\\b[^>]{0,8192}>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Attribute = new("\\b(srcset|src)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static bool Allowed(Uri uri) => uri.IsAbsoluteUri && uri.Scheme is "http" or "https" && uri.IsDefaultPort &&
        uri.UserInfo.Length == 0 && uri.AbsoluteUri.Length <= 8192 && uri.DnsSafeHost.Length > 0;
    public static Uri? Find(string? html, string? text)
    {
        try
        {
            if (html is { Length: <= 1048576 })
            {
                Uri? source = null;
                foreach (var line in html.Split('\n').Take(20))
                    if (line.StartsWith("SourceURL:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(line[10..].Trim(), UriKind.Absolute, out var parsed) && Allowed(parsed)) source = parsed;
                var tag = Image.Match(html);
                if (tag.Success)
                {
                    var attributes = Attribute.Matches(tag.Value).Cast<Match>().Select(m => (Name: m.Groups[1].Value, Value: WebUtility.HtmlDecode(m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value))).ToArray();
                    var srcset = attributes.FirstOrDefault(a => a.Name.Equals("srcset", StringComparison.OrdinalIgnoreCase)).Value;
                    if (srcset is not null)
                        foreach (var candidate in srcset.Split(',').Select(s => s.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Where(p => p.Length > 0)
                            .OrderByDescending(p => p.Length > 1 && double.TryParse(p[1].TrimEnd('w', 'x'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0))
                            if (Resolve(candidate[0], source) is { } url) return url;
                    var src = attributes.FirstOrDefault(a => a.Name.Equals("src", StringComparison.OrdinalIgnoreCase)).Value;
                    if (src is not null && Resolve(src, source) is { } image) return image;
                }
            }
            if (text is { Length: <= 8192 } && Uri.TryCreate(text.Trim(), UriKind.Absolute, out var direct) && Allowed(direct) && MediaFiles.IsImage(direct.AbsolutePath)) return direct;
        }
        catch (RegexMatchTimeoutException) { }
        return null;
    }
    private static Uri? Resolve(string value, Uri? source)
    {
        if (value.StartsWith("//", StringComparison.Ordinal)) value = "https:" + value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var url) && Allowed(url)) return url;
        return source is not null && Uri.TryCreate(source, value, out url) && Allowed(url) ? url : null;
    }
    public static bool PublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return b[0] is >= 0x20 and <= 0x3f && !(b[0] == 0x20 && b[1] == 1 && b[2] == 0x0d && b[3] == 0xb8);
        return address.AddressFamily == AddressFamily.InterNetwork && b[0] is > 0 and < 224 && b[0] != 10 && b[0] != 127 &&
            !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] is >= 16 and <= 31) && !(b[0] == 192 && b[1] == 168) &&
            !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 198 && b[1] is 18 or 19) &&
            !(b[0] == 192 && b[1] == 0 && b[2] is 0 or 2) && !(b[0] == 198 && b[1] == 51 && b[2] == 100) && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
}
