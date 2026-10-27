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
            if (Item.Kind == ItemKind.Folder) return "DIR";
            if (Item.Kind is ItemKind.File) return Path.GetExtension(Item.FilePath ?? "").TrimStart('.').ToUpperInvariant()[..Math.Min(3, Path.GetExtension(Item.FilePath ?? "").TrimStart('.').Length)];
            var text = Item.Content.Trim();
            if (Item.Kind == ItemKind.Link && Uri.TryCreate(text, UriKind.Absolute, out var uri)) text = uri.Host.Replace("www.", "");
            var elements = new System.Globalization.StringInfo(text);
            return elements.LengthInTextElements == 0 ? "Aa" : elements.SubstringByTextElements(0, Math.Min(2, elements.LengthInTextElements));
        }
    }
    public string Details => Missing ? "Original unavailable" : Item.Kind switch {
        ItemKind.Text => $"Text · {Item.Content.Length:N0} characters",
        ItemKind.Link => "Link",
        ItemKind.Folder => "Folder",
        _ => Path.GetExtension(Item.FilePath ?? "").TrimStart('.').ToUpperInvariant() + " file"
    };
    public string Help => Item.FilePath ?? Item.Content;
    public bool Missing => Item.FilePath is not null && !File.Exists(Item.FilePath) && !Directory.Exists(Item.FilePath);
    public Visibility Pinned => Item.IsPinned ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ImageVisible => Item.Kind == ItemKind.Image ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IconVisible => Item.Kind == ItemKind.Image ? Visibility.Collapsed : Visibility.Visible;
    public ItemRow(ShelfItem item) { Item = item; }
}
