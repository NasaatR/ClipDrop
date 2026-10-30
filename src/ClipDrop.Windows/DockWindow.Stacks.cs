using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

namespace ClipDrop.Windows;

public partial class DockWindow
{
    private void OpenStack(ItemRow row, FrameworkElement anchor)
    {
        var items = row.Members.ToArray(); var scope = PanelId ?? Session.State.ActivePanelId; var id = row.StackId!;
        var menu = new ContextMenu { MaxWidth = 310, MaxHeight = 420, FontSize = 11.5 }; Appearance.Apply(Session.State.Settings, menu, true);
        PopupDismiss.Attach(menu, this);
        MenuItem Add(string title, string tag, Action action)
        {
            var entry = new MenuItem { Header = new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 225 }, Tag = tag, ToolTip = title, Padding = new Thickness(6, 4, 6, 4), FontSize = 11.5 };
            entry.Click += (_, _) => { menu.IsOpen = false; Dispatcher.BeginInvoke(action); }; menu.Items.Add(entry); return entry;
        }
        Add($"Copy all {items.Length} clips", "CopyAll", () => Session.Copy(items));
        Add("Split stack", "SplitStack", () => Session.SplitStack(id, scope)); menu.Items.Add(new Separator());
        var thumbnails = new List<(MenuItem Entry, string Path)>();
        foreach (var item in items.Take(20))
        {
            var member = Add(item.Title, "StackMember:" + item.Id, () => Session.Paste(new[] { item }));
            member.Icon = new AppIcon { Kind = new ItemRow(item).IconKey, Width = 20, Height = 20 };
            if (item.FilePath is string path && (ClipDrop.Core.MediaFiles.IsImage(path) || ClipDrop.Core.MediaFiles.IsVideo(path)))
                thumbnails.Add((member, path));
        }
        if (items.Length > 20) Add($"{items.Length - 20} more clips · Library", "Library", _library);
        anchor.ContextMenu = menu; ClipMenu.Show(menu, anchor, true);
        foreach (var thumbnail in thumbnails) LoadIcon(thumbnail.Entry, thumbnail.Path, menu);
    }
    private async void LoadIcon(MenuItem member, string path, ContextMenu menu)
    {
        var thumbnail = await _thumbnails.Get(path);
        if (thumbnail is not null && menu.IsOpen) member.Icon = new Image { Source = thumbnail, Width = 22, Height = 22, Stretch = Stretch.Uniform };
    }
}
