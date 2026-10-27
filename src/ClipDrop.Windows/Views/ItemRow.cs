using System;
using System.IO;
using System.Windows;
using ClipDrop.Core;

namespace ClipDrop.Windows.Views;

public sealed class ItemRow
{
    public ShelfItem Item { get; }
    public string Title => Item.Title;
    public bool IsVideo => MediaFiles.IsVideo(Item.FilePath);
    public string Glyph => Item.Kind switch {
        ItemKind.Text => "M5,4 H19 M12,4 V20 M8,20 H16",
        ItemKind.Link => "M10,14 L14,10 M8,16 L6,18 C3,21 0,17 3,14 L7,10 C9,8 12,9 13,11 M16,8 L18,6 C21,3 25,7 22,10 L18,14 C16,16 13,15 12,13",
        ItemKind.Folder => "M3,6 V20 H21 V7 H12 L10,4 H3 Z",
        ItemKind.Image => "M3,3 H21 V21 H3 Z M3,17 L9,11 L14,16 L17,13 L21,17 M16,7 A1,1 0 1 1 15.99,7",
        ItemKind.Clipboard => "M8,3 H16 V6 H8 Z M8,4 H5 V22 H19 V4 H16 M8,12 H16 M8,16 H14",
        _ => IsVideo ? "M3,4 H21 V20 H3 Z M10,8 L16,12 L10,16 Z" : "M6,2 H14 L20,8 V22 H6 Z M14,2 V8 H20"
    };
    private string TypeDetails => Missing ? "Original unavailable" : IsVideo ? "Video · " + Path.GetExtension(Item.FilePath ?? "").TrimStart('.').ToUpperInvariant() : Item.Kind switch {
        ItemKind.Text => $"Text · {Item.Content.Length:N0} characters",
        ItemKind.Link => "Link",
        ItemKind.Clipboard => $"Clipboard · {Item.Clipboard?.Formats.Count ?? 0} formats",
        ItemKind.Folder => "Folder",
        _ => Path.GetExtension(Item.FilePath ?? "").TrimStart('.').ToUpperInvariant() + " file"
    };
    public string Details => TypeDetails + (Item.Group.Length > 0 ? " · " + Item.Group : "");
    public string Help => Item.FilePath ?? Item.Content;
    public bool Missing => Item.FilePath is not null && !File.Exists(Item.FilePath) && !Directory.Exists(Item.FilePath);
    public Visibility Pinned => Item.IsPinned ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ImageVisible => Item.Kind == ItemKind.Image || IsVideo ? Visibility.Visible : Visibility.Collapsed;
    public ItemRow(ShelfItem item) { Item = item; }
}
