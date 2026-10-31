using System;
using System.Windows;
using ClipDrop.Core;

namespace ClipDrop.Windows.Views;

// The floating list and non-expiring Settings sample share these exact metrics.
public static class DownloadsPresentation
{
    public static DownloadsSize Apply(FrameworkElement target, ShelfSettings settings)
    {
        var size = SurfaceSizing.Downloads(settings);
        void Set(string key, object value) { if (!target.Resources.Contains(key) || !Equals(target.Resources[key], value)) target.Resources[key] = value; }
        Set("DownloadsRowHeight", size.RowHeight);
        Set("DownloadsRowMargin", new Thickness(0, 0, 0, size.RowSpacing));
        Set("DownloadsIconSize", size.IconSize);
        Set("DownloadsCapsuleHeight", new GridLength(size.CapsuleHeight));
        Set("DownloadsPreviewSize", Math.Min(30, size.RowHeight - 10));
        Set("DownloadsPreviewSlot", new GridLength(Math.Min(30, size.RowHeight - 10) + 7));
        Set("DownloadsActionSize", Math.Clamp(size.RowHeight - 8, 20, 28));
        Set("DownloadsGlyphSize", Math.Round(size.IconSize * .52));
        Set("DownloadsTimeVisibility", settings.DownloadsShowTime ? Visibility.Visible : Visibility.Collapsed);
        Set("DownloadsFolderTooltip", settings.DownloadsRevealMode == "WindowsExplorer" ? "Show in Windows Explorer · select file" : "Show in Explorer · uses your default file manager");
        Set("DownloadsAlwaysShowFolder", settings.DownloadsAlwaysShowFolder);
        return size;
    }
}
