using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClipDrop.Core;
using ClipDrop.Windows.Services;
namespace ClipDrop.Windows.Views;

public static class ClipMenu
{
    public static ContextMenu Create(ShelfSession session, IReadOnlyList<ShelfItem> items, Window owner, bool dock, bool trash = false, Action<ShelfItem>? edit = null)
    {
        var selected = items.ToArray();
        var menu = new ContextMenu(); Appearance.Apply(session.State.Settings, menu, dock);
        System.Windows.Automation.AutomationProperties.SetName(menu, "Clip actions");
        void Add(string header, string id, string icon, Action run, bool enabled = true, string shortcut = "")
        {
            var glyph = new AppIcon { Kind = icon.Replace("Icon", ""), Width = 18, Height = 18 };
            var item = new MenuItem { Header = header, Tag = id, Icon = glyph, InputGestureText = shortcut, IsEnabled = enabled };
            item.Click += (_, _) => { menu.IsOpen = false; owner.Dispatcher.BeginInvoke(run, DispatcherPriority.Background); };
            menu.Items.Add(item);
        }
        var single = selected.Length == 1;
        Add("Paste", "Paste", "PasteIcon", () => session.Paste(selected), selected.Length > 0 && !trash);
        Add("Copy", "Copy", "CopyIcon", () => session.Copy(selected), selected.Length > 0, "Ctrl+C");
        Add("Copy as plain text", "CopyText", "TextIcon", () => session.Copy(selected, true), selected.Length > 0);
        menu.Items.Add(new Separator());
        Add("Preview", "Preview", "ImageIcon", () => new PreviewWindow(selected[0]) { Owner = owner }.Show(), single, "Space");
        Add("Open in app", "Open", "RevealIcon", () => Open(selected[0], session), single && !new ItemRow(selected[0]).Missing && selected[0].Kind is not (ItemKind.Text or ItemKind.Clipboard));
        Add("Show in Explorer", "Reveal", "FolderIcon", () => Reveal(selected[0], session), single && selected[0].FilePath is not null && !new ItemRow(selected[0]).Missing);
        menu.Items.Add(new Separator());
        if (trash) Add("Restore", "Restore", "RestoreIcon", () => session.RestoreTrash(selected), selected.Length > 0);
        else
        {
            var pinned = selected.Length > 0 && selected.All(i => i.IsPinned);
            Add(pinned ? "Unpin from dock" : "Pin to dock", "Pin", "PinIcon", () => session.TogglePin(selected), selected.Length > 0);
            Add("Rename / group", "Edit", "EditIcon", () => edit?.Invoke(selected[0]), single && edit is not null, "F2");
            Add("Move to Trash", "Trash", "TrashIcon", () => session.Remove(selected.ToList()), selected.Length > 0, "Delete");
        }
        return menu;
    }
    public static void Show(ContextMenu menu, FrameworkElement target, bool keyboard = false)
    {
        menu.PlacementTarget = target; menu.Placement = keyboard ? PlacementMode.Bottom : PlacementMode.MousePoint;
        menu.IsOpen = true;
    }
    private static void Open(ShelfItem item, ShelfSession session)
    {
        try
        {
            var target = item.FilePath ?? item.Content.Trim();
            if (item.Kind == ItemKind.Link && (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) return;
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) { session.Error("Open", ex); }
    }
    private static void Reveal(ShelfItem item, ShelfSession session)
    {
        try
        {
            if (item.FilePath is not string path || (!File.Exists(path) && !Directory.Exists(path))) return;
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            info.ArgumentList.Add("/select,"); info.ArgumentList.Add(path); Process.Start(info);
        }
        catch (Exception ex) { session.Error("Show in Explorer", ex); }
    }
}
