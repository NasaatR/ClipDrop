using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class BackupProbe
{
    public static void Run(MainWindow library, DockWindow dock, string root, string output, Action<bool, string> check)
    {
        library.Hide(); dock.HideDock();
        var store = new StateStore(Path.Combine(root, "backup-ui")); var state = store.Load();
        var bytes = new byte[8 * 1024 * 1024]; new Random(12345).NextBytes(bytes);
        var assets = new AssetStore(Path.Combine(store.Root, "assets"));
        state.Items.Add(new ShelfItem { Kind = ItemKind.Image, OwnedAsset = true, FilePath = assets.WriteImage(bytes), Title = "Original payload" });
        state.Items.Add(ShelfItem.FromText("Current unsaved UI revision — Пример"));
        var snapshotOnDispatcher = false;
        var section = new BackupSection(store.Root, () => { snapshotOnDispatcher = library.Dispatcher.CheckAccess(); return store.CaptureSnapshot(state); });
        var host = new Window { Content = section, Width = 450, Height = 200, ShowInTaskbar = false, Left = 40, Top = 50 };
        host.SetResourceReference(Window.BackgroundProperty, "Surface"); host.SetResourceReference(Window.ForegroundProperty, "Ink"); section.Margin = new Thickness(14);
        host.Closed += (_, _) => section.Dispose();
        try
        {
            host.Show(); var path = Path.Combine(root, "ui-backup.clipdrop.zip");
            var export = section.ExportTo(path); var responsive = false; host.Dispatcher.BeginInvoke(() => responsive = true);
            check(section.IsExporting && UiProbe.Until(() => responsive), "Backup snapshots on the UI thread while archive work leaves the dispatcher responsive");
            check(UiProbe.Until(() => export.IsCompleted, 10000) && export.GetAwaiter().GetResult() is { Clips: 2 } && snapshotOnDispatcher && section.StatusText.Contains("verified"),
                "The backup UI acknowledges a verified archive of the current unsaved revision");
            check(!File.Exists(Path.Combine(store.Root, "shelf.json")) && BackupArchive.Verify(path).Clips == 2,
                "Creating a backup does not silently write source shelf metadata");
            UiProbe.CaptureWindow(host, Path.Combine(output, "backup-created-screen.png"));
            var inspect = section.InspectFrom(path); var inspectResponsive = false; host.Dispatcher.BeginInvoke(() => inspectResponsive = true);
            check(UiProbe.Until(() => inspectResponsive) && UiProbe.Until(() => inspect.IsCompleted, 10000) && inspect.GetAwaiter().GetResult() is { Clips: 2 } && section.StatusText.Contains("library unchanged"),
                "Inspect backup verifies integrity and previews counts off-thread without replacing the live library");
            check(!File.Exists(Path.Combine(store.Root, "shelf.json")) && state.Items.Count == 2, "Backup inspection never creates live metadata or changes clips");
            host.Height = 280; UiProbe.CaptureWindow(host, Path.Combine(output, "backup-inspected-screen.png"));
            var bad = Path.Combine(root, "invalid-preview.clipdrop.zip"); File.WriteAllText(bad, "invalid archive");
            var invalid = section.InspectFrom(bad);
            check(UiProbe.Until(() => invalid.IsCompleted, 10000) && invalid.GetAwaiter().GetResult() is null && section.StatusText.Contains("failed"),
                "Invalid backup previews fail visibly without touching library data");
            var cancelledInspect = section.InspectFrom(path); section.Cancel();
            check(UiProbe.Until(() => cancelledInspect.IsCompleted, 10000) && cancelledInspect.GetAwaiter().GetResult() is null && section.StatusText.Contains("cancelled") && state.Items.Count == 2,
                "Cancelling an inspection restores ready controls without changing the library");
            var original = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
            var cancelledPath = Path.Combine(root, "ui-backup-cancelled.clipdrop.zip");
            var cancel = section.ExportTo(cancelledPath); section.Cancel();
            check(UiProbe.Until(() => cancel.IsCompleted, 10000) && cancel.GetAwaiter().GetResult() is null && section.StatusText.Contains("cancelled") &&
                !File.Exists(cancelledPath) && System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(original),
                "Backup cancellation restores ready controls and preserves a previous destination");
            var closing = section.ExportTo(Path.Combine(root, "ui-backup-closed.clipdrop.zip")); host.Close();
            check(UiProbe.Until(() => closing.IsCompleted, 10000) && !section.IsExporting,
                "Closing Settings during export cancels and releases its operation without a late UI update");
        }
        finally { if (host.IsVisible) host.Close(); section.Dispose(); IconAssets.Initialize(root); dock.ShowDock(); library.ShowShelf(); }
    }
}
