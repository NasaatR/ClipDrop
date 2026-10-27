using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClipDrop.Windows.Services;
namespace ClipDrop.Windows.Views;

public sealed class VideoPreview : Grid, IDisposable
{
    private readonly string _path;
    private readonly MediaElement _media = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform, IsMuted = true };
    private readonly Image _poster = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _message = new() { Text = "Press Play to preview", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
    private readonly Button _play = new() { Content = "Play", MinWidth = 64 };
    private readonly Button _sound = new() { Content = "Sound off" };
    private readonly Slider _seek = new() { Minimum = 0, Maximum = 1, IsEnabled = false, Margin = new Thickness(10, 0, 10, 0) };
    private readonly TextBlock _time = new() { Text = "0:00", VerticalAlignment = VerticalAlignment.Center, MinWidth = 80, TextAlignment = TextAlignment.Right };
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _loading = new() { Interval = TimeSpan.FromSeconds(8) };
    private bool _opened, _playing, _scrubbing, _disposed;
    public MediaElement Media => _media;
    public bool HasOpened => _opened;
    public bool HasFailed { get; private set; }
    public VideoPreview(string path)
    {
        _path = path;
        RowDefinitions.Add(new RowDefinition()); RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var stage = new Grid { Background = Brushes.Black }; Children.Add(stage);
        _message.Foreground = Brushes.White;
        stage.Children.Add(_poster); stage.Children.Add(_media); stage.Children.Add(_message);
        var controls = new DockPanel { Margin = new Thickness(10) }; SetRow(controls, 1); Children.Add(controls);
        var open = new Button { Content = "Open in app", ToolTip = "Use your default video player" };
        DockPanel.SetDock(open, Dock.Right); controls.Children.Add(open);
        DockPanel.SetDock(_play, Dock.Left); controls.Children.Add(_play);
        DockPanel.SetDock(_sound, Dock.Left); controls.Children.Add(_sound);
        DockPanel.SetDock(_time, Dock.Right); controls.Children.Add(_time); controls.Children.Add(_seek);
        System.Windows.Automation.AutomationProperties.SetName(_seek, "Video position");
        _play.Click += (_, _) => Toggle();
        _sound.Click += (_, _) => { _media.IsMuted = !_media.IsMuted; _sound.Content = _media.IsMuted ? "Sound off" : "Sound on"; };
        open.Click += (_, _) => {
            try { Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true }); }
            catch (Exception) { _message.Text = "Original file unavailable"; _message.Visibility = Visibility.Visible; }
        };
        _media.MediaOpened += (_, _) => {
            _loading.Stop(); _opened = true; HasFailed = false; _poster.Visibility = Visibility.Collapsed; _message.Visibility = Visibility.Collapsed;
            _seek.IsEnabled = _media.NaturalDuration.HasTimeSpan && _media.NaturalDuration.TimeSpan.TotalSeconds > 0;
            if (_seek.IsEnabled) _seek.Maximum = _media.NaturalDuration.TimeSpan.TotalSeconds;
            UpdateTime();
        };
        _media.MediaFailed += (_, _) => Failed();
        _media.MediaEnded += (_, _) => { _playing = false; _play.Content = "Replay"; _tick.Stop(); UpdateTime(); };
        _seek.PreviewMouseLeftButtonDown += (_, _) => _scrubbing = true;
        _seek.PreviewMouseLeftButtonUp += (_, _) => { _scrubbing = false; Seek(); };
        _seek.LostMouseCapture += (_, _) => { if (_scrubbing) { _scrubbing = false; Seek(); } };
        _seek.PreviewKeyUp += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) Seek(); };
        _tick.Tick += (_, _) => UpdateTime();
        _loading.Tick += (_, _) => Failed();
        IsVisibleChanged += (_, _) => { if (!IsVisible && _playing) { _media.Pause(); _playing = false; _play.Content = "Play"; _tick.Stop(); _loading.Stop(); } };
        Unloaded += (_, _) => Dispose();
        Loaded += async (_, _) => {
            var image = await ThumbnailCache.Shared.Get(_path);
            if (_disposed || _opened) return;
            _poster.Source = image;
        };
    }
    public void Toggle()
    {
        if (_disposed) return;
        if (!File.Exists(_path)) { Failed("Original file unavailable"); return; }
        try
        {
            if (_playing) { _media.Pause(); _playing = false; _play.Content = "Play"; _tick.Stop(); _loading.Stop(); return; }
            if (!_opened) { HasFailed = false; _message.Text = "Loading preview…"; _message.Visibility = Visibility.Visible; _media.Source = new Uri(Path.GetFullPath(_path)); _loading.Start(); }
            else if (_seek.IsEnabled && _media.Position.TotalSeconds >= _seek.Maximum - .1) _media.Position = TimeSpan.Zero;
            _media.Play(); _playing = true; _play.Content = "Pause"; _tick.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException or IOException) { Failed(); }
    }
    private void Seek()
    {
        if (!_opened || !_seek.IsEnabled || _disposed) return;
        try { _media.Position = TimeSpan.FromSeconds(_seek.Value); UpdateTime(); }
        catch (InvalidOperationException) { Failed(); }
    }
    private static string Stamp(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
    private void UpdateTime()
    {
        if (_disposed || !_opened) return;
        if (!_scrubbing) _seek.Value = Math.Clamp(_media.Position.TotalSeconds, 0, _seek.Maximum);
        _time.Text = Stamp(_media.Position.TotalSeconds) + (_seek.IsEnabled ? " / " + Stamp(_seek.Maximum) : "");
    }
    private void Failed(string text = "Preview unavailable for this codec. Use Open in app.")
    {
        HasFailed = true; _loading.Stop(); _tick.Stop(); _playing = false; _opened = false; _seek.IsEnabled = false;
        _media.Close(); _play.Content = "Retry"; _poster.Visibility = Visibility.Visible; _message.Text = text; _message.Visibility = Visibility.Visible;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _loading.Stop(); _tick.Stop(); _media.Close(); _media.Source = null;
    }
}
