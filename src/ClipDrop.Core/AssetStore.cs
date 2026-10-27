using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

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
    public string WriteBlob(byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var directory = Path.Combine(Root, "clipboard"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, hash + ".bin");
        if (!File.Exists(path))
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return hash;
    }
    public byte[] ReadBlob(ClipboardFormatBlob blob)
    {
        if (blob.Hash.Length != 64 || !System.Linq.Enumerable.All(blob.Hash, Uri.IsHexDigit) || blob.Length < 1 || blob.Length > 64 * 1024 * 1024)
            throw new InvalidDataException("Invalid clipboard asset.");
        var path = Path.Combine(Root, "clipboard", blob.Hash + ".bin");
        if (new FileInfo(path).Length != blob.Length) throw new InvalidDataException("Clipboard asset changed.");
        var bytes = File.ReadAllBytes(path);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(blob.Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Clipboard asset changed.");
        return bytes;
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
