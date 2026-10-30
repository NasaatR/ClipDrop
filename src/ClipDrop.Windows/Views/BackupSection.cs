using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ClipDrop.Core;

namespace ClipDrop.Windows.Views;

public sealed class BackupSection : StackPanel, IDisposable
{
    private readonly string _root;
    private readonly Func<byte[]> _snapshot;
    private readonly Button _create = new() { Content = "Create backup" };
    private readonly Button _cancel = new() { Content = "Cancel", Visibility = Visibility.Collapsed };
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 4) };
    private CancellationTokenSource? _stop;
    private bool _disposed;
    public bool IsExporting => _stop is not null;
    public string StatusText => _status.Text;
    public BackupSection(string root, Func<byte[]> snapshot)
    {
        _root = root; _snapshot = snapshot;
        var actions = new WrapPanel(); actions.Children.Add(_create); actions.Children.Add(_cancel); Children.Add(actions);
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Children.Add(_status);
        var hint = new TextBlock { Text = "Includes clips, Trash, saved panels, clipboard content and styles. External files stay as references and are not copied. Each backup uses a new filename to keep earlier copies.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 4) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Children.Add(hint);
        _cancel.Click += (_, _) => _stop?.Cancel();
        _create.Click += async (_, _) => {
            var picker = new Microsoft.Win32.SaveFileDialog { Title = "Create new ClipDrop backup", Filter = "ClipDrop backup (*.clipdrop.zip)|*.clipdrop.zip", DefaultExt = ".clipdrop.zip", AddExtension = true,
                OverwritePrompt = false, FileName = "ClipDrop-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".clipdrop.zip" };
            if (picker.ShowDialog(Window.GetWindow(this)) == true) await ExportTo(picker.FileName);
        };
    }
    public async Task<BackupSummary?> ExportTo(string destination)
    {
        if (_disposed || IsExporting) return null;
        using var stop = new CancellationTokenSource(); _stop = stop;
        _create.IsEnabled = false; _cancel.Visibility = Visibility.Visible; _status.Text = "Preparing backup…";
        try
        {
            // Snapshot on the STA, stream and verify only immutable metadata/files
            // off-thread. Do not silently save a different on-disk revision first.
            var bytes = _snapshot();
            IProgress<BackupProgress> progress = new Progress<BackupProgress>(value => {
                if (!_disposed && ReferenceEquals(_stop, stop)) _status.Text = value.Stage == "Verifying" ? "Verifying archive…" : $"Creating backup · {value.Files}/{value.TotalFiles} assets";
            });
            var summary = await Task.Run(() => BackupArchive.Create(_root, bytes, destination, progress.Report, stop.Token), stop.Token);
            if (!_disposed) _status.Text = $"Backup created and verified · {summary.Clips} clips · {summary.Trashed} in Trash";
            return summary;
        }
        catch (OperationCanceledException) { if (!_disposed) _status.Text = "Backup cancelled · existing files kept"; return null; }
        catch (Exception ex)
        {
            if (!_disposed) _status.Text = "Backup failed · " + ex.Message;
            return null;
        }
        finally
        {
            if (ReferenceEquals(_stop, stop)) _stop = null;
            if (!_disposed) { _create.IsEnabled = true; _cancel.Visibility = Visibility.Collapsed; }
        }
    }
    public void Cancel() => _stop?.Cancel();
    public void Dispose() { if (_disposed) return; _disposed = true; _stop?.Cancel(); }
}
