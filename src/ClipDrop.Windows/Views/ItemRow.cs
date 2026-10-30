using System;
using System.IO;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using ClipDrop.Core;

namespace ClipDrop.Windows.Views;

public sealed class ItemRow : System.ComponentModel.INotifyPropertyChanged
{
    public ShelfItem Item { get; private set; }
    public string Key { get; private set; }
    public string? StackId { get; private set; }
    public ShelfItem[] Members { get; private set; }
    public bool IsStack => StackId is not null && Members.Length > 1;
    public string StackCount => Members.Length.ToString();
    public Visibility StackVisible => IsStack ? Visibility.Visible : Visibility.Collapsed;
    public string? PreviewPath => Item.FilePath;
    public bool IsImage => Item.Kind == ItemKind.Image || MediaFiles.IsImage(Item.FilePath);
    public string Title => IsStack ? $"Stack · {Members.Length} clips" : Item.Title;
    public string SelectName => "Select clip: " + Title;
    public bool IsVideo => MediaFiles.IsVideo(Item.FilePath);
    public string IconKey => IsVideo ? "Video" : IsImage ? "Image" : Item.Kind.ToString();
    private bool _selected;
    private int _dropMark;
    public bool Selected { get => _selected; set { if (_selected == value) return; _selected = value; Notify(nameof(Selected)); } }
    public int DropMark { get => _dropMark; set { if (_dropMark == value) return; _dropMark = value; Notify(nameof(DropMark)); } }
    public bool KeepAfterUse => System.Linq.Enumerable.All(Members, i => i.KeepAfterUse);
    public string DockHelp => Title + (IsStack ? "\nClick to open · drag to transfer all · right-click for stack actions" : "\nClick to paste · drop on center to stack · drop at an edge to reorder");
    public string KeepName => KeepHelp + ": " + Title;
    public string KeepHelp => Item.KeepAfterUse ? "Keep after use · on" : "Keep after use · off";
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private (string Title, string Group, string Content, string? Path, ItemKind Kind, bool Keep, int Formats) _metadata;
    private void Notify(string? name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    public void Refresh()
    {
        var metadata = (Item.Title, Item.Group, Item.Content, Item.FilePath, Item.Kind, KeepAfterUse, Item.Clipboard?.Formats.Count ?? 0);
        if (_metadata == metadata) return;
        _metadata = metadata; Notify(null);
    }
    private string TypeDetails => Missing ? "Original unavailable" : IsVideo ? "Video · " + Path.GetExtension(Item.FilePath ?? "").TrimStart('.').ToUpperInvariant() : Item.Kind switch {
        ItemKind.Text => $"Text · {Item.Content.Length:N0} characters",
        ItemKind.Link => "Link",
        ItemKind.Clipboard => $"Clipboard · {Item.Clipboard?.Formats.Count ?? 0} formats",
        ItemKind.Folder => "Folder",
        _ => Path.GetExtension(Item.FilePath ?? "").TrimStart('.').ToUpperInvariant() + " file"
    };
    public string Details => TypeDetails + (Item.Group.Length > 0 ? " · " + Item.Group : "");
    public string Help => Item.FilePath ?? Item.Content;
    private bool _missing, _checking;
    private DateTimeOffset _lastCheck;
    private static readonly SemaphoreSlim FileChecks = new(2);
    private static int _pendingChecks;
    public bool Missing => _missing;
    public async Task CheckAvailability()
    {
        if (_checking || Item.FilePath is not string path || DateTimeOffset.UtcNow - _lastCheck < TimeSpan.FromSeconds(10)) return;
        if (Interlocked.Increment(ref _pendingChecks) > 64) { Interlocked.Decrement(ref _pendingChecks); return; }
        _checking = true;
        try
        {
            await FileChecks.WaitAsync();
            bool missing;
            try { missing = await Task.Run(() => !File.Exists(path) && !Directory.Exists(path)); }
            finally { FileChecks.Release(); }
            if (Item.FilePath == path)
            {
                _lastCheck = DateTimeOffset.UtcNow;
                if (_missing != missing) { _missing = missing; Notify(nameof(Missing)); Notify(nameof(Details)); }
            }
        }
        finally { _checking = false; Interlocked.Decrement(ref _pendingChecks); }
    }
    public Visibility Pinned => Item.KeepAfterUse ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ImageVisible => IsImage || IsVideo ? Visibility.Visible : Visibility.Collapsed;
    public ItemRow(ShelfItem item) { Item = item; Key = item.Id; Members = new[] { item }; Refresh(); }
    public void SetTile(DockTile tile)
    {
        var changed = Key != tile.Key || StackId != tile.StackId || !System.Linq.Enumerable.SequenceEqual(Members, tile.Items);
        Item = tile.Items[0]; Key = tile.Key; StackId = tile.StackId; Members = tile.Items;
        Refresh(); if (changed) Notify(null);
    }
}
