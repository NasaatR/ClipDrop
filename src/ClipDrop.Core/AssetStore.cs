using System;
using System.IO;
using System.Text;

namespace ClipDrop.Core;

public sealed class AssetStore
{
    public string Root { get; }
    public AssetStore(string root) { Root = Path.GetFullPath(root); Directory.CreateDirectory(Root); }

    public string WriteImage(byte[] png)
    {
        var path = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".png");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        file.Write(png);
        return path;
    }

    public string ExportText(ShelfItem item)
    {
        var directory = Path.Combine(Root, "exports");
        Directory.CreateDirectory(directory);
        // Identity is validated before becoming part of a path.
        if (!Guid.TryParse(item.Id, out var id)) throw new InvalidDataException("Invalid item identity.");
        var link = item.Kind == ItemKind.Link;
        var path = Path.Combine(directory, id.ToString("N") + (link ? ".url" : ".txt"));
        if (!File.Exists(path))
            File.WriteAllText(path, link ? "[InternetShortcut]\r\nURL=" + item.Content.Trim() + "\r\n" : item.Content,
                new UTF8Encoding(false));
        return path;
    }
    // No timed cleanup: another application may still be reading an exported file.
    // Removing an item never deletes a source file or an exported clipboard payload.
}
