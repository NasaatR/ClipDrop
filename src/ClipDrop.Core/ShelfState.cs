using System;
using System.Collections.Generic;
using System.IO;

namespace ClipDrop.Core;

public enum ItemKind { File, Folder, Image, Text, Link }

public sealed class ShelfItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ItemKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? FilePath { get; set; }
    public bool OwnedAsset { get; set; }
    public bool IsPinned { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public static ShelfItem FromPath(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException("The original file or folder is unavailable.");
        var folder = Directory.Exists(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var image = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff";
        return new ShelfItem {
            Kind = folder ? ItemKind.Folder : image ? ItemKind.Image : ItemKind.File,
            Title = Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), FilePath = path
        };
    }

    public static ShelfItem FromText(string text)
    {
        var trimmed = text.Trim();
        var link = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http");
        var title = trimmed.Replace('\r', ' ').Replace('\n', ' ');
        return new ShelfItem { Kind = link ? ItemKind.Link : ItemKind.Text,
            Content = text, Title = title.Length > 90 ? title[..90] + "…" : title };
    }

    public bool SameContent(ShelfItem other) => Kind == other.Kind &&
        (FilePath is not null && other.FilePath is not null
            ? string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase)
            : FilePath is null && other.FilePath is null && Content == other.Content);
}

public sealed class ShelfSettings
{
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartHidden { get; set; }
    public bool ShakeToShow { get; set; }
    public bool CloseToTray { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    public double FontSize { get; set; } = 13;
    public double RowSpacing { get; set; } = 4;
    public double CornerRadius { get; set; } = 8;
    public double Width { get; set; } = 376;
    public double Height { get; set; } = 500;
    public double? Left { get; set; }
    public double? Top { get; set; }
}

public sealed class ShelfState
{
    public const int CurrentSchema = 1;
    public int SchemaVersion { get; set; } = CurrentSchema;
    public List<ShelfItem> Items { get; set; } = new();
    public ShelfSettings Settings { get; set; } = new();
}
