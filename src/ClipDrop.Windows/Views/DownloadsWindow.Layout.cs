using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

public partial class DownloadsWindow
{
    public bool OpensDown { get; private set; }
    private bool _iconOnLeft;
    private int _scrollRevision;
    private Point RevealOrigin => new(_iconOnLeft ? 0 : 1, OpensDown ? 0 : 1);
    public Point CapsuleCenterPixels()
    {
        if (Capsule.IsLoaded && Capsule.ActualWidth > 0)
            return Capsule.PointToScreen(new Point(Capsule.ActualWidth / 2, Capsule.ActualHeight / 2));
        var bounds = WindowPlacement.PixelBounds(this); var edge = (SurfaceSizing.Downloads(_session.State.Settings).IconSize / 2 + 10) * WindowPlacement.Scale(this);
        return new(_iconOnLeft ? bounds.Left + edge : bounds.Right - edge, OpensDown ? bounds.Top + edge : bounds.Bottom - edge);
    }
    public void ApplyPlacement(DownloadsPlacement placement)
    {
        var follow = IsCompact || AtRecentEnd(); var anchor = follow ? null : ReadScrollAnchor();
        var flip = OpensDown != placement.OpensDown;
        OpensDown = placement.OpensDown; _iconOnLeft = placement.IconOnLeft;
        var capsule = new GridLength(SurfaceSizing.Downloads(_session.State.Settings).CapsuleHeight);
        Stage.RowDefinitions[0].Height = OpensDown ? capsule : new GridLength(1, GridUnitType.Star);
        Stage.RowDefinitions[1].Height = OpensDown ? new GridLength(1, GridUnitType.Star) : capsule;
        Grid.SetRow(Details, OpensDown ? 1 : 0); Grid.SetRow(CapsuleShell, OpensDown ? 0 : 1); Grid.SetRow(HoverBridge, OpensDown ? 0 : 1);
        Details.VerticalAlignment = OpensDown ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        Details.Margin = OpensDown ? new Thickness(10, 3, 10, 10) : new Thickness(10, 10, 10, 3);
        CapsuleShell.VerticalAlignment = OpensDown ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        CapsuleShell.HorizontalAlignment = HoverBridge.HorizontalAlignment = _iconOnLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        HoverBridge.Margin = OpensDown ? new Thickness(10, 10, 10, 0) : new Thickness(10, 0, 10, 10);
        Width = placement.Width; Height = placement.Height; FitDetails(); UpdateLayout();
        if (flip) { Refresh(); if (!IsCompact) Motion.Disclose(Details, RevealOrigin); QueueRecentScroll(); }
        else RestoreScroll(follow, anchor);
    }
    private ScrollViewer? FileScroll() => FindScroll(Files);
    private static ScrollViewer? FindScroll(DependencyObject node)
    {
        if (node is ScrollViewer scroll) return scroll;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindScroll(VisualTreeHelper.GetChild(node, i)) is { } found) return found;
        return null;
    }
    private bool AtRecentEnd() => FileScroll() is not { } scroll ||
        (OpensDown ? scroll.VerticalOffset <= 2 : scroll.ScrollableHeight - scroll.VerticalOffset <= 2);
    private sealed record ScrollAnchor(string Path, double Y);
    private ScrollAnchor? ReadScrollAnchor()
    {
        foreach (var row in _rows)
            if (Files.ItemContainerGenerator.ContainerFromItem(row) is FrameworkElement element)
            {
                var y = element.TranslatePoint(new Point(), Files).Y;
                if (y >= 0 && y < Files.ActualHeight) return new(row.File.Path, y);
            }
        return null;
    }
    private void QueueRecentScroll() => RestoreScroll(true, null);
    private void RestoreScroll(bool recent, ScrollAnchor? anchor)
    {
        var revision = ++_scrollRevision;
        Dispatcher.BeginInvoke(() => {
            if (_disposing || revision != _scrollRevision || _busy || _origin.HasValue || Files.IsMouseCaptureWithin || _rows.Count == 0) return;
            if (recent)
            {
                Files.ScrollIntoView(OpensDown ? _rows[0] : _rows[^1]); Files.UpdateLayout();
                if (FileScroll() is { } scroll) scroll.ScrollToVerticalOffset(OpensDown ? 0 : scroll.ScrollableHeight);
            }
            else if (anchor is not null && _rows.FirstOrDefault(r => r.File.Path == anchor.Path) is { } row)
            {
                Files.ScrollIntoView(row); Files.UpdateLayout();
                if (FileScroll() is { } scroll && Files.ItemContainerGenerator.ContainerFromItem(row) is FrameworkElement element)
                    scroll.ScrollToVerticalOffset(scroll.VerticalOffset + element.TranslatePoint(new Point(), Files).Y - anchor.Y);
            }
        }, DispatcherPriority.Loaded);
    }
}
