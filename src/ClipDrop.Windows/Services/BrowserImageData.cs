using System;
using System.IO;
using System.Text;
using System.Windows;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public static class BrowserImageData
{
    // Read the foreign OLE object on the STA. Only immutable URI/text leaves it.
    private static string? ReadFormat(IDataObject data, string format, Encoding encoding)
    {
        if (!data.GetDataPresent(format, false)) return null;
        var value = data.GetData(format, false);
        if (value is string text) return text.Length <= 1048576 ? text.TrimEnd('\0') : null;
        if (value is byte[] bytes) return bytes.Length <= 1048576 ? encoding.GetString(bytes).TrimEnd('\0') : null;
        if (value is not Stream stream) return null;
        var position = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek) { if (stream.Length > 1048576) return null; stream.Position = 0; }
            using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            { if (output.Length + count > 1048576) return null; output.Write(buffer, 0, count); }
            return encoding.GetString(output.ToArray()).TrimEnd('\0');
        }
        finally { if (stream.CanSeek) stream.Position = position; }
    }
    public static string? Text(IDataObject data) => data.GetData(DataFormats.UnicodeText) as string ?? data.GetData(DataFormats.Text) as string ??
        ReadFormat(data, "UniformResourceLocatorW", Encoding.Unicode) ?? ReadFormat(data, "UniformResourceLocator", Encoding.UTF8);
    public static ShelfItem? Read(IDataObject data)
    {
        var text = Text(data);
        var candidate = ImageSourceHints.Find(ReadFormat(data, "HTML Format", Encoding.UTF8), text);
        if (candidate is null) return null;
        var item = ShelfItem.FromText(candidate.AbsoluteUri); item.SourceUrl = candidate.AbsoluteUri; return item;
    }
}
