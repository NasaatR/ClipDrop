using System;
using System.Collections.Generic;
using System.IO;

namespace ClipDrop.Core;

public enum ItemKind { File, Folder, Image, Text, Link, Clipboard }

public sealed class ShelfItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ItemKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? FilePath { get; set; }
    public bool OwnedAsset { get; set; }
    public bool IsPinned { get; set; }
    public bool KeepAfterUse { get; set; }
    public long PinOrder { get; set; }
    public string Group { get; set; } = "";
    public ClipboardSnapshot? Clipboard { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public static ShelfItem FromPath(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException("The original file or folder is unavailable.");
        var folder = Directory.Exists(path);
        var image = MediaFiles.IsImage(path);
        return new ShelfItem {
            Kind = folder ? ItemKind.Folder : image ? ItemKind.Image : ItemKind.File,
            Title = Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), FilePath = path
        };
    }

    public static ShelfItem FromText(string text)
    {
        var trimmed = text.Trim();
        var link = !trimmed.Contains('\r') && !trimmed.Contains('\n') && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http");
        var title = trimmed.Replace('\r', ' ').Replace('\n', ' ');
        return new ShelfItem { Kind = link ? ItemKind.Link : ItemKind.Text,
            Content = text, Title = title.Length > 90 ? title[..90] + "…" : title };
    }

    public bool SameContent(ShelfItem other) => (SourceUrl is not null && SourceUrl == other.SourceUrl && Clipboard?.Identity == other.Clipboard?.Identity) || Kind == other.Kind &&
        (FilePath is not null && other.FilePath is not null
            ? string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase)
            : FilePath is null && other.FilePath is null && Content == other.Content)
        && Clipboard?.Identity == other.Clipboard?.Identity;
}

public sealed class ShelfSettings : AppearanceStyle
{
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartHidden { get; set; }
    public bool ShakeToShow { get; set; }
    public bool CloseToTray { get; set; } = true;
    public double Width { get; set; } = 376;
    public double Height { get; set; } = 500;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? DockLeft { get; set; }
    public double? DockTop { get; set; }
    public bool LibraryFollowsDock { get; set; } = true;
    public bool AnimationsEnabled { get; set; } = true;
    public double MotionDuration { get; set; } = 240;
    public double DockMaxWidth { get; set; } = 640;
    public string MotionStyle { get; set; } = "Spring";
    public bool CollapseWhenIdle { get; set; } = true;
    public bool FetchDroppedImages { get; set; } = true;
    public bool DownloadsEnabled { get; set; }
    public double DownloadsWidth { get; set; } = 272;
    public double DownloadsRows { get; set; } = 10;
    public double DownloadsRowHeight { get; set; } = 34;
    public double DownloadsIconSize { get; set; } = 44;
    public string DownloadsFolder { get; set; } = "";
    public string DownloadsSource { get; set; } = "Auto";
    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveDownloadsSource => DownloadsSource == "Auto" ? (DownloadsFolder.Length > 0 ? "Folder" : "Chrome") : DownloadsSource;
    public DockLayout DownloadsLayout { get; set; } = new();
    public bool MirrorDownloads { get; set; } = true;
    public List<DockLayout> DownloadsLayouts { get; set; } = new();
    public bool MirrorDocks { get; set; }
    public string HomeDisplayId { get; set; } = "";
    public List<DockLayout> Docks { get; set; } = new();
    public List<SavedStyle> SavedStyles { get; set; } = new();
}

public sealed class ShelfState
{
    public const int CurrentSchema = 7;
    public int SchemaVersion { get; set; } = CurrentSchema;
    public List<ShelfItem> Items { get; set; } = new();
    public List<TrashEntry> Trash { get; set; } = new();
    public ShelfSettings Settings { get; set; } = new();
    public List<PanelSet> Panels { get; set; } = new();
    public string ActivePanelId { get; set; } = "";
    public ShelfState()
    {
        var main = new PanelSet { Name = "Main" }; Panels.Add(main); ActivePanelId = main.Id;
    }
}

public sealed class PanelSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Panel";
    public List<string> ClipIds { get; set; } = new();
    public List<ClipStack> Stacks { get; set; } = new();
    public DateTimeOffset? ClosedUtc { get; set; }
    public PanelWindowState? Window { get; set; }
}

public sealed class ClipStack
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public List<string> ClipIds { get; set; } = new();
}

public sealed class PanelWindowState
{
    public bool IsOpen { get; set; }
    public DateTimeOffset? ClosedUtc { get; set; }
    public DockLayout Layout { get; set; } = new() { Enabled = true };
}

public sealed class TrashEntry
{
    public ShelfItem Item { get; set; } = new();
    public string Batch { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset DeletedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ClipboardSnapshot
{
    public string Identity { get; set; } = "";
    public List<ClipboardFormatBlob> Formats { get; set; } = new();
    public bool Partial { get; set; }
}

public sealed class ClipboardFormatBlob
{
    public uint StandardId { get; set; }
    public string Name { get; set; } = "";
    public string Hash { get; set; } = "";
    public int Length { get; set; }
}
