using System;
using System.Collections.Generic;
using System.Linq;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;
public sealed partial class ShelfSession
{
    private void ConfigureClipboardHistory()
    {
        if (_disposed) return;
        if (!State.Settings.AutoSaveClipboard) { _historyMonitor?.Dispose(); _historyMonitor = null; return; }
        if (_historyMonitor is not null) return;
        try { _historyMonitor = new ClipboardHistoryMonitor(() => CaptureClipboard(false, null, false, true)); }
        catch (System.ComponentModel.Win32Exception ex)
        { State.Settings.AutoSaveClipboard = false; Error("Start clipboard history", ex); }
    }
    public void RestoreHistoryForDrop(IReadOnlyList<ShelfItem> clips)
    {
        if (LibraryOperations.Restore(State, clips.Select(i => i.Id)) == 0) return;
        PanelSets.ApplyActive(State); Change();
    }
    public void ForgetHistory(IEnumerable<string> ids)
    {
        if (ClipHistory.Forget(State, ids) == 0) return;
        Change(); Notify("Removed from History · saved clips and panels kept");
    }
    public void AddHistoryToPanel(IReadOnlyList<ShelfItem> clips, string? panelId = null)
    {
        try
        {
            if (ClipHistory.AddToPanel(State, clips.Select(i => i.Id), panelId).Length == 0) return;
            Change(); Notify("Added to panel · kept in History");
        }
        catch (Exception ex) { Error("Add from History", ex); }
    }
}
