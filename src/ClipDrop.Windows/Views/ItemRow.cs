using System;
using System.IO;
using System.Windows;
using ClipDrop.Core;

namespace ClipDrop.Windows.Views;

public sealed class ItemRow : System.ComponentModel.INotifyPropertyChanged
{
    public ShelfItem Item { get; }
    public string Title => Item.Title;
    public bool IsVideo => MediaFiles.IsVideo(Item.FilePath);
    public string IconKey => IsVideo ? "Video" : Item.Kind.ToString();
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(null));
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
