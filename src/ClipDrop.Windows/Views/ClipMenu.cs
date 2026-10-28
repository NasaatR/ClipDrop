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
        var menu = new ContextMenu { MaxWidth = 280, FontSize = 11.5 }; Appearance.Apply(session.State.Settings, menu, dock);
        PopupDismiss.Attach(menu, owner);
        System.Windows.Automation.AutomationProperties.SetName(menu, "Clip actions");
        MenuItem Add(string header, string id, string icon, Action run, string shortcut = "")
        {
            var glyph = new AppIcon { Kind = icon, Width = 15, Height = 15 };
            var item = new MenuItem { Header = header, Tag = id, Icon = glyph, InputGestureText = dock ? "" : shortcut, Padding = new Thickness(6, 3, 6, 3), FontSize = 11.5 };
            item.Click += (_, _) => { menu.IsOpen = false; owner.Dispatcher.BeginInvoke(run, DispatcherPriority.Background); };
            menu.Items.Add(item); return item;
        }
        var single = selected.Length == 1;
        if (trash) Add("Restore", "Restore", "Restore", () => session.RestoreTrash(selected));
        else
        {
            Add(selected.Length > 1 ? $"Delete {selected.Length} clips" : "Delete", "Trash", "Trash", () => session.Remove(selected.ToList()), "Del").ToolTip = "Move to Trash · restore in Library";
            var kept = selected.Length > 0 && selected.All(i => i.KeepAfterUse);
            Add(kept ? "Use once" : "Keep after use", "Keep", "Keep", () => session.ToggleKeep(selected));
        }
        Add("Copy", "Copy", "Copy", () => session.Copy(selected), "Ctrl+C");
        var more = new MenuItem { Header = "More…", Tag = "More", Icon = new AppIcon { Kind = "Library", Width = 15, Height = 15 }, StaysOpenOnClick = true, Padding = new Thickness(6, 3, 6, 3), FontSize = 11.5 };
        menu.Items.Add(more);
        more.Click += (_, e) => {
            e.Handled = true; menu.Items.Remove(more); menu.Items.Add(new Separator());
            if (!trash && selected.Length > 0) Add("Paste", "Paste", "Paste", () => session.Paste(selected));
            if (selected.Length > 0) Add("Copy as text", "CopyText", "Text", () => session.Copy(selected, true));
            if (single)
            {
                Add("Preview", "Preview", "Image", () => new PreviewWindow(selected[0]) { Owner = owner }.Show(), "Space");
                var item = selected[0];
                if (!new ItemRow(item).Missing && item.Kind is not (ItemKind.Text or ItemKind.Clipboard)) Add("Open in app", "Open", "Reveal", () => Open(item, session));
                if (item.FilePath is not null && !new ItemRow(item).Missing) Add("Show in Explorer", "Reveal", "Folder", () => Reveal(item, session));
            }
            if (!trash && selected.Length > 0)
            {
                var pinned = selected.All(i => i.IsPinned);
                Add(pinned ? "Remove from dock" : "Add to dock", "Pin", "Pin", () => session.TogglePin(selected));
                if (single && edit is not null) Add("Rename / group", "Edit", "Edit", () => edit(selected[0]), "F2");
            }
        };
        return menu;
    }
    public static void Show(ContextMenu menu, FrameworkElement target, bool keyboard = false)
    {
        PopupDismiss.CloseAll();
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
