using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

public sealed class PreviewWindow : Window
{
    public PreviewWindow(ShelfItem item)
    {
        Title = item.Title; Width = 640; Height = 480; MinWidth = 300; MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (MediaFiles.IsVideo(item.FilePath) && item.FilePath is string video)
        {
            MinWidth = 520;
            var preview = new VideoPreview(video); Content = preview;
            Closed += (_, _) => preview.Dispose();
        }
        else if (item.Kind == ItemKind.Image && item.FilePath is not null)
        {
            try
            {
                if (new FileInfo(item.FilePath).Length > ImageFiles.MaximumBytes) throw new InvalidDataException("Image is too large for a preview; drag its original file instead.");
                Content = new Image { Source = ImageFiles.Load(File.ReadAllBytes(item.FilePath)), Margin = new Thickness(16), Stretch = System.Windows.Media.Stretch.Uniform };
            }
            catch (Exception) { Content = new TextBlock { Text = "Preview unavailable. The original file can still be transferred if it exists.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20) }; }
        }
        else Content = new TextBox { Text = item.Kind == ItemKind.Clipboard ? "Original clipboard formats\n\n" + string.Join("\n", item.Clipboard?.Formats.Select(f => f.Name.Length > 0 ? f.Name : "Windows format " + f.StandardId) ?? Array.Empty<string>()) : item.FilePath ?? item.Content, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12) };
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }
}
