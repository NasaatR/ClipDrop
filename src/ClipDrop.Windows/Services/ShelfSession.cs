using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

// One in-process owner for dock, library and settings. Views never save independent copies.
public sealed class ShelfSession : IDisposable
{
    private readonly StateStore _store;
    private readonly DispatcherTimer _save;
    private readonly Stack<List<ShelfItem>> _removed = new();
    private bool _dirty;
    public ShelfState State { get; }
    public TransferService Transfer { get; }
    public Diagnostics Diagnostics { get; } = new();
    public string Root => _store.Root;
    public string Status { get; private set; } = "Ready";
    public string StorageStatus { get; private set; } = "Saved";
    public DesktopPaste? Paster { get; set; }
    public bool IsPasting { get; private set; }
    public event Action? Changed;
    public event Action<string>? StatusChanged;
    public event Action<string>? Notification;
    public event Action? QuitRequested;

    public ShelfSession(StateStore store, ShelfState state)
    {
        _store = store; State = state;
        Transfer = new TransferService(new AssetStore(Path.Combine(Root, "assets")));
        _save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _save.Tick += (_, _) => TrySave();
        if (store.RecoveryNotice is not null) Status = store.RecoveryNotice;
    }

    public void Change(bool refresh = true)
    {
        _dirty = true; StorageStatus = "Saving"; SetStatus("Saving…");
        _save.Stop(); _save.Start();
        if (refresh) Changed?.Invoke();
    }

    public bool TrySave()
    {
        _save.Stop();
        if (!_dirty) return true;
        try { _store.Save(State); _dirty = false; StorageStatus = "Saved"; SetStatus("Saved"); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Diagnostics.Record("Save library", ex); StorageStatus = "Unsaved changes retained";
            Notify("Save failed — changes kept open. Retry in Settings."); return false;
        }
    }

    public void SetStatus(string text) { Status = text; StatusChanged?.Invoke(text); }
    public void Notify(string text) { SetStatus(text); Notification?.Invoke(text); }
    public void Error(string operation, Exception error)
    {
        Diagnostics.Record(operation, error);
        var detail = error is FileNotFoundException ? "original file unavailable" :
            error is System.Runtime.InteropServices.ExternalException ? "clipboard is busy; try again" : "retry or copy diagnostics in Settings";
        Notify(operation + " failed — " + detail);
    }

    public int Add(IEnumerable<ShelfItem> incoming, bool pin = true)
    {
        var added = 0; var promoted = false; var full = false;
        foreach (var item in incoming)
        {
            var existing = State.Items.FirstOrDefault(i => i.SameContent(item));
            if (existing is not null)
            {
                if (pin && !existing.IsPinned) { existing.IsPinned = true; promoted = true; }
                continue;
            }
            if (State.Items.Count >= 5000) { full = true; break; }
            item.IsPinned = pin; State.Items.Add(item); added++;
        }
        if (added > 0 || promoted) Change();
        if (full) Notify("Library limit reached. Some items were not added.");
        return added;
    }

    public void SaveClipboard()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null) { Notify("Clipboard is empty"); return; }
            Capture(data);
        }
        catch (Exception ex) { Error("Save clipboard", ex); }
    }

    public bool Capture(IDataObject data)
    {
        var items = Transfer.Import(data);
        if (items.Count == 0) { Notify("No supported files, image or text"); return false; }
        var count = Add(items);
        if (TrySave()) Notify(count > 0 ? $"Saved {count} {(count == 1 ? "item" : "items")} · pinned to dock" : "Already in your library");
        return true;
    }

    public void TogglePin(IReadOnlyList<ShelfItem> items)
    {
        if (items.Count == 0) return;
        var pin = !items.All(i => i.IsPinned);
        foreach (var item in items) item.IsPinned = pin;
        Change();
    }

    public void Remove(List<ShelfItem> items)
    {
        if (items.Count == 0) return;
        _removed.Push(items);
        if (_removed.Count > 20)
        {
            var batches = _removed.Take(20).Reverse().ToArray(); _removed.Clear();
            foreach (var batch in batches) _removed.Push(batch);
        }
        foreach (var item in items) State.Items.Remove(item);
        Change(); Notify("Removed · Ctrl+Z in Library to undo");
    }

    public void UndoRemove()
    {
        if (!_removed.TryPop(out var items)) return;
        State.Items.AddRange(items.Where(i => State.Items.All(current => current.Id != i.Id)));
        Change(); Notify("Items restored");
    }

    public bool Copy(IReadOnlyList<ShelfItem> items, bool plainText = false)
    {
        if (items.Count == 0) return false;
        try
        {
            if (plainText) Clipboard.SetText(string.Join(Environment.NewLine, items.Select(i => i.FilePath ?? i.Content)));
            else Clipboard.SetDataObject(Transfer.Build(items), true);
            Notify("Copied · ready to paste"); return true;
        }
        catch (Exception ex) { Error("Copy", ex); return false; }
    }

    public async void Paste(IReadOnlyList<ShelfItem> items)
    {
        if (IsPasting || items.Count == 0) return;
        IsPasting = true;
        try
        {
            var target = Paster?.CaptureTarget();
            // The copied payload remains available on every fallback; never restore on a timer.
            Clipboard.SetDataObject(Transfer.Build(items), true);
            var result = Paster is null ? PasteOutcome.NoTarget : await Paster.Paste(target);
            Diagnostics.Note("Paste outcome", result.ToString());
            Notify(result == PasteOutcome.Requested ? "Paste requested · items kept" : result switch {
                PasteOutcome.TargetChanged => "Copied · destination changed; paste manually",
                PasteOutcome.KeysHeld => "Copied · release modifier keys and paste manually",
                _ => "Copied · focus your app and press Ctrl+V"
            });
        }
        catch (Exception ex) { Error("Paste", ex); }
        finally { IsPasting = false; }
    }

    public string Report() => Diagnostics.Report(State.Items.Count, StorageStatus);
    public void RequestQuit() => QuitRequested?.Invoke();
    public void Dispose() => _save.Stop();
}
