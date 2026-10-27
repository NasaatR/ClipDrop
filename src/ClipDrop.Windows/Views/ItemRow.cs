using System;
using System.IO;
using System.Windows;
using ClipDrop.Core;

namespace ClipDrop.Windows.Views;

public sealed class ItemRow
{
    public ShelfItem Item { get; }
    public string Title => Item.Title;
    public string DockGlyph
    {
        get
        {
            return Item.Kind switch { ItemKind.Text => "📝", ItemKind.Link => "🔗", ItemKind.Folder => "📁", ItemKind.Image => "🖼️", ItemKind.Clipboard => "📋", _ => "📄" };
        }
    }
    private string TypeDetails => Missing ? "Original unavailable" : Item.Kind switch {
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
    public Visibility ImageVisible => Item.Kind == ItemKind.Image ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IconVisible => Item.Kind == ItemKind.Image ? Visibility.Collapsed : Visibility.Visible;
    public ItemRow(ShelfItem item) { Item = item; }
}
