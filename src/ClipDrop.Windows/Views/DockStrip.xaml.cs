using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ClipDrop.Core;
using ClipDrop.Windows.Services;
namespace ClipDrop.Windows.Views;

// The exact same dock surface renders in the floating window and Settings preview.
public partial class DockStrip : UserControl
{
    public event RoutedEventHandler? SaveRequested, SettingsRequested, LibraryRequested, PinRequested, PinLoaded, ImageLoaded;
    public event MouseButtonEventHandler? PinDown, PinUp, PinMenu;
    public event MouseEventHandler? PinMove;
    public DockStrip() { InitializeComponent(); }
    public void Apply(ShelfSettings settings)
    {
        Appearance.Apply(settings, this, true);
        Resources["DockSize"] = Appearance.Bound(settings.DockButtonSize, 30, 48, 36);
        Resources["DockGap"] = new Thickness(Appearance.Bound(settings.DockSpacing, 0, 12, 4) / 2);
        Resources["DockRadius"] = new CornerRadius(Appearance.Bound(settings.DockRadius, 0, 24, 11));
        var bg = ((SolidColorBrush)FindResource("Surface")).Color;
        bg.A = (byte)Math.Round(255 * Appearance.Bound(settings.DockOpacity, 0, 100, 100) / 100);
        Resources["DockBackground"] = new SolidColorBrush(bg);
        var ink = ((SolidColorBrush)FindResource("Ink")).Color; ink.A = 28;
        Resources["DockOutline"] = new SolidColorBrush(ink);
        Body.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 3.25, Direction = 270, Opacity = .38 };
    }
    public void SetPins(IEnumerable<ShelfItem> pins, int capacity)
    {
        var all = pins.ToArray();
        PinnedItems.ItemsSource = all.Take(capacity).Select(i => new ItemRow(i)).ToArray();
        EmptyHint.Visibility = all.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        OverflowButton.Visibility = all.Length > capacity ? Visibility.Visible : Visibility.Collapsed;
        OverflowButton.Content = "+" + (all.Length - capacity);
    }
    private void OnSave(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(sender, e);
    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(sender, e);
    private void OnLibrary(object sender, RoutedEventArgs e) => LibraryRequested?.Invoke(sender, e);
    private void OnPin(object sender, RoutedEventArgs e) => PinRequested?.Invoke(sender, e);
    private void OnPinLoaded(object sender, RoutedEventArgs e) => PinLoaded?.Invoke(sender, e);
    private void OnImageLoaded(object sender, RoutedEventArgs e) => ImageLoaded?.Invoke(sender, e);
    private void OnPinMenu(object sender, MouseButtonEventArgs e) => PinMenu?.Invoke(sender, e);
    private void OnPinDown(object sender, MouseButtonEventArgs e) => PinDown?.Invoke(sender, e);
    private void OnPinUp(object sender, MouseButtonEventArgs e) => PinUp?.Invoke(sender, e);
    private void OnPinMove(object sender, MouseEventArgs e) => PinMove?.Invoke(sender, e);
}
