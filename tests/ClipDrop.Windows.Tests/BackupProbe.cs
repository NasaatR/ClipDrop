using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        Action? afterSnapshot = null;
        var section = new BackupSection(store.Root, () => {
            snapshotOnDispatcher = library.Dispatcher.CheckAccess(); var captured = store.CaptureSnapshot(state);
            var hook = afterSnapshot; afterSnapshot = null; hook?.Invoke(); return captured;
        });
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
            check(section.ComparisonText.Contains("0 new · 2 identical · 0 changed metadata · 0 already in Trash"), "Backup UI shows the verified comparison against a captured current-state snapshot");
            host.Height = 490; UiProbe.Pump();
            var comparisons = UiProbe.Find<ListBox>(section, list => list.Name == "Comparisons");
            var include = UiProbe.Find<CheckBox>(section, box => box.Name == "IncludeIdentical");
            check(comparisons.Items.Count == 0 && UiProbe.Find<TextBlock>(section, t => t.Name == "Empty").IsVisible,
                "Identical backups show a compact no-differences state by default");
            UiProbe.Click(include);
            check(comparisons.Items.Count == 2 && UiProbe.Find<TextBlock>(comparisons, t => t.Text == "Identical recorded metadata").IsVisible,
                "Including identical clips reveals real per-clip metadata rows");
            UiProbe.Click(include); state.Items[1].Content = "Changed after archive";
            var differences = section.InspectFrom(path);
            check(UiProbe.Until(() => differences.IsCompleted, 10000) && differences.GetAwaiter().GetResult() is not null && comparisons.Items.Count == 1,
                "Inspection filters the verified archive to changed clips");
            UiProbe.Pump();
            check(UiProbe.Find<TextBlock>(comparisons, t => t.Text == "Changed metadata · Text").IsVisible,
                "Inspection names the changed clip and exact metadata field without mutating the live text");
            check(state.Items[1].Content == "Changed after archive", "Read-only conflict details preserve the current edited clip");
            var choice = UiProbe.Find<ComboBox>(comparisons, _ => true);
            check(choice.SelectedIndex == 0 && UiProbe.Find<TextBlock>(section, t => t.Name == "PlanSummary").Text.Contains("0 replace"),
                "A conflicting clip defaults to keeping the current library version");
            UiProbe.Click(choice); UiProbe.CaptureWindow(host, Path.Combine(output, "backup-choice-open.png"));
            UiProbe.PressKey(0x23); UiProbe.PressKey(0x0D);
            check(choice.SelectedIndex == 1 && UiProbe.Find<TextBlock>(section, t => t.Name == "PlanSummary").Text.Contains("1 replace") && state.Items[1].Content == "Changed after archive",
                "Choosing the backup version updates only the preview plan through the real dropdown");
            check(UiProbe.Find<TextBlock>(choice, t => t.Text == "Use backup").IsVisible,
                "The closed restore selector displays its user-facing label instead of record implementation details");
            UiProbe.Click(include); UiProbe.Click(include);
            check(UiProbe.Find<ComboBox>(comparisons, _ => true).SelectedIndex == 1,
                "Filtering identical clips preserves the current preview choices");
            var checkPlanButton = UiProbe.Find<Button>(section, b => b.Content as string == "Check restore plan");
            UiProbe.Click(checkPlanButton);
            check(UiProbe.Until(() => !section.IsExporting, 10000) && section.StatusText.Contains("Plan checked") && state.Items[1].Content == "Changed after archive",
                "The real Check restore plan action verifies choices in the background without changing current content");
            UiProbe.CaptureWindow(host, Path.Combine(output, "backup-inspected-screen.png"));
            var currentChoice = UiProbe.Find<ComboBox>(comparisons, _ => true);
            afterSnapshot = () => host.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => currentChoice.SelectedIndex = 0));
            var changedChoices = section.CheckPlan();
            check(UiProbe.Until(() => changedChoices.IsCompleted, 10000) && changedChoices.GetAwaiter().GetResult() is null && section.StatusText.Contains("Choices changed"),
                "Changing choices during archive IO discards the obsolete successful plan result");
            afterSnapshot = () => state.Items[1].Content = "Changed during plan validation";
            var changedLive = section.CheckPlan();
            check(UiProbe.Until(() => changedLive.IsCompleted, 10000) && changedLive.GetAwaiter().GetResult() is null && section.StatusText.Contains("Library changed") && state.Items[1].Content == "Changed during plan validation",
                "A live edit after capture cannot leave a stale plan marked checked");
            var cancelPlan = section.CheckPlan(); section.Cancel();
            check(UiProbe.Until(() => cancelPlan.IsCompleted, 10000) && cancelPlan.GetAwaiter().GetResult() is null && section.StatusText.Contains("cancelled"),
                "Plan recheck cancellation releases controls and keeps the library unchanged");
            var bad = Path.Combine(root, "invalid-preview.clipdrop.zip"); File.WriteAllText(bad, "invalid archive");
            var invalid = section.InspectFrom(bad);
            check(UiProbe.Until(() => invalid.IsCompleted, 10000) && invalid.GetAwaiter().GetResult() is null && section.StatusText.Contains("failed"),
                "Invalid backup previews fail visibly without touching library data");
            check(comparisons.Items.Count == 0 && !comparisons.IsVisible, "A failed inspection cannot leave an old archive's conflict rows visible");
            var cancelledInspect = section.InspectFrom(path); section.Cancel();
            check(UiProbe.Until(() => cancelledInspect.IsCompleted, 10000) && cancelledInspect.GetAwaiter().GetResult() is null && section.StatusText.Contains("cancelled") && state.Items.Count == 2,
                "Cancelling an inspection restores ready controls without changing the library");
            var fresh = section.InspectFrom(path);
            check(UiProbe.Until(() => fresh.IsCompleted, 10000) && fresh.GetAwaiter().GetResult() is not null,
                "Another verified inspection creates a new session-only restore draft");
            UiProbe.Pump();
            check(UiProbe.Find<ComboBox>(comparisons, _ => true).SelectedIndex == 0 && UiProbe.Find<TextBlock>(section, t => t.Name == "PlanSummary").Text.Contains("0 replace"),
                "Reinspection cannot silently reuse a prior archive's overwrite choice");
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
