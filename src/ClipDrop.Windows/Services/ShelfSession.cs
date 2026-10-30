using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

// One in-process owner for dock, library and settings. Views never save independent copies.
public sealed partial class ShelfSession : IDisposable
{
    private readonly StateStore _store;
    private readonly DispatcherTimer _save;
    private readonly NativeClipboard _clipboard;
    private bool _dirty;
    private bool _disposed;
    private long _revision, _persistedRevision;
    private Task? _saving;
    private long _pinRevision = -1;
    private Dictionary<string, ShelfItem> _itemIndex = new();
    private readonly Dictionary<string, ShelfItem[]> _pinCache = new();
    public ShelfState State { get; }
    public TransferService Transfer { get; }
    public Diagnostics Diagnostics { get; } = new();
    public string Root => _store.Root;
    public string Status { get; private set; } = "Ready";
    public string StorageStatus { get; private set; } = "Saved";
    public DesktopPaste? Paster { get; set; }
    private ClipDrag? _activeDrag;
    public bool IsPasting { get; private set; }
    public event Action? Changed;
    public event Action<string>? StatusChanged;
    public event Action<string>? Notification;
    public event Action? QuitRequested;
    public event Action<IReadOnlyList<ShelfItem>?>? PanelsRequested;

    public ShelfSession(StateStore store, ShelfState state, BrowserImageLoader? browserImages = null)
    {
        _store = store; State = state; IconAssets.Initialize(Root);
        _browserImages = browserImages ?? new BrowserImageLoader();
        var assets = new AssetStore(Path.Combine(Root, "assets"));
        Transfer = new TransferService(assets); _clipboard = new NativeClipboard(assets);
        _save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _save.Tick += async (_, _) => await AutoSave();
        if (store.RecoveryNotice is not null) Status = store.RecoveryNotice;
    }

    public void Change(bool refresh = true)
    {
        if (_disposed) return;
        if (refresh) PanelSets.CaptureActive(State);
        _revision++;
        _dirty = true; StorageStatus = "Saving"; SetStatus("Saving…");
        _save.Stop(); _save.Start();
        if (refresh) Changed?.Invoke();
    }

    public bool TrySave()
    {
        _save.Stop();
        if (!_dirty) return true;
        try
        {
            // An explicit save/quit drains queued work. Debounced saves never
            // wait for disk on the dispatcher; they use AutoSave below.
            _store.Save(State); _persistedRevision = _revision; _dirty = false;
            StorageStatus = "Saved"; SetStatus("Saved"); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Diagnostics.Record("Save library", ex); StorageStatus = "Unsaved changes retained";
            Notify("Save failed — changes kept open. Retry in Settings."); return false;
        }
    }
    private async Task AutoSave()
    {
        _save.Stop();
        if (_disposed || !_dirty || _saving is { IsCompleted: false }) return;
        var revision = _revision; Task? task = null;
        try
        {
            _saving = task = _store.SaveAsync(State);
            await task;
            if (_disposed) return;
            _persistedRevision = Math.Max(_persistedRevision, revision);
            _dirty = _persistedRevision < _revision;
            if (_dirty) _save.Start();
            else { StorageStatus = "Saved"; SetStatus("Saved"); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (_disposed || _persistedRevision >= revision) return;
            Diagnostics.Record("Save library", ex); StorageStatus = "Unsaved changes retained";
            Notify("Save failed — changes kept open. Retry in Settings.");
        }
        finally { if (ReferenceEquals(_saving, task)) _saving = null; }
    }

    public void SetStatus(string text) { Status = text; StatusChanged?.Invoke(text); }
    public void Notify(string text) { SetStatus(text); Notification?.Invoke(text); }
    public void Error(string operation, Exception error)
    {
        Diagnostics.Record(operation, error);
        var detail = error is FileNotFoundException ? "original file unavailable" :
            error is InvalidOperationException ? error.Message :
            error is System.Runtime.InteropServices.ExternalException ? "clipboard is busy; try again" : "retry or copy diagnostics in Settings";
        Notify(operation + " failed — " + detail);
    }

    public ShelfItem[] Pins(string? panelId = null)
    {
        if (_pinRevision != _revision || _itemIndex.Count != State.Items.Count)
        { _pinRevision = _revision; _itemIndex = State.Items.ToDictionary(i => i.Id); _pinCache.Clear(); _tileCache.Clear(); }
        var id = panelId ?? State.ActivePanelId;
        if (_pinCache.TryGetValue(id, out var cached)) return cached;
        var panel = State.Panels.SingleOrDefault(p => p.Id == id && p.ClosedUtc is null) ?? throw new InvalidOperationException("Panel unavailable.");
        return _pinCache[id] = id == State.ActivePanelId ? State.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).ToArray() :
            panel.ClipIds.Where(_itemIndex.ContainsKey).Select(key => _itemIndex[key]).ToArray();
    }
    public string PanelName(string? panelId = null) => State.Panels.Single(p => p.Id == (panelId ?? State.ActivePanelId)).Name;
    public int Add(IEnumerable<ShelfItem> incoming, bool pin = true, string? panelId = null)
    {
        var added = 0; var promoted = false; var full = false;
        var scoped = pin && panelId is not null && panelId != State.ActivePanelId;
        if (scoped && !State.Panels.Any(p => p.Id == panelId && p.ClosedUtc is null)) throw new InvalidOperationException("Panel unavailable.");
        var members = new List<string>();
        var nextOrder = PinOrdering.Next(State);
        foreach (var item in incoming)
        {
            var existing = State.Items.FirstOrDefault(i => i.SameContent(item));
            if (existing is not null)
            {
                if (scoped) members.Add(existing.Id);
                else if (pin && !existing.IsPinned) { existing.IsPinned = true; existing.PinOrder = nextOrder++; promoted = true; }
                continue;
            }
            if (State.Items.Count >= 5000) { full = true; break; }
            item.IsPinned = pin && !scoped; item.PinOrder = nextOrder++; State.Items.Add(item); added++;
            if (scoped) members.Add(item.Id);
        }
        if (scoped) promoted |= PanelSets.SetMembership(State, panelId!, members, true);
        if (added > 0 || promoted) Change();
        if (full) Notify("Library limit reached. Some items were not added.");
        return added;
    }

    public void SaveClipboard() => SaveClipboard(true);
    public void SaveClipboard(bool pin, string? panelId = null)
    {
        try
        {
            var sequence = NativeClipboard.GetClipboardSequenceNumber();
            var data = Clipboard.GetDataObject();
            if (data is null) { Notify("Clipboard is empty"); return; }
            List<ShelfItem> items;
            try { items = Transfer.Import(data); }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidDataException)
            { items = new(); Diagnostics.Record("Clipboard preview", ex); }
            var snapshot = data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent("FileGroupDescriptorW", false) || items.Any(i => i.Kind is ItemKind.File or ItemKind.Folder) ? null : _clipboard.Capture();
            if (NativeClipboard.GetClipboardSequenceNumber() != sequence) { Notify("Clipboard changed · save again"); return; }
            if (items.Count == 0 && snapshot is not null) items.Add(new ShelfItem { Kind = ItemKind.Clipboard, Title = "Clipboard item" });
            if (items.Count == 1) items[0].Clipboard = snapshot;
            if (items.Count == 0) { Notify("No supported clipboard content"); return; }
            var count = Add(items, pin, panelId);
            if (TrySave()) Notify(snapshot?.Partial == true ? "Saved · some app-only formats unavailable" : count > 0 ? (pin ? "Saved to dock" : "Saved to Library") + " · original clipboard formats kept" : "Already in your library");
        }
        catch (Exception ex) { Error("Save clipboard", ex); }
    }

    public bool Capture(IDataObject data, bool pin = true, string? panelId = null, int? insertion = null, string? mergeTarget = null)
    {
        var items = Transfer.Import(data, browserDrop: true);
        if (items.Count == 0) { Notify("No supported files, image or text"); return false; }
        var count = Add(items, pin, panelId);
        var accepted = items.Select(source => State.Items.FirstOrDefault(i => i.SameContent(source))).OfType<ShelfItem>().Distinct().ToArray();
        if (pin && accepted.Length > 0)
        {
            if (mergeTarget is not null) Stack(accepted, mergeTarget, panelId);
            else if (insertion is int index) Reorder(accepted, index, panelId);
        }
        if (TrySave()) Notify(count > 0 ? $"Saved {count} {(count == 1 ? "item" : "items")} · " + (pin ? "pinned to dock" : "in Library") : "Already in your library");
        if (State.Settings.FetchDroppedImages)
            foreach (var pending in accepted.Where(i => i.SourceUrl is not null && i.Kind == ItemKind.Link)) FetchImage(pending);
        return accepted.Length > 0;
    }

    public void ToggleKeep(IReadOnlyList<ShelfItem> items)
    {
        var current = State.Items.Where(i => items.Any(s => s.Id == i.Id)).ToArray();
        if (current.Length == 0) return;
        var keep = !current.All(i => i.KeepAfterUse);
        foreach (var item in current) item.KeepAfterUse = keep;
        Change(); Notify(keep ? "Kept after use" : "Move to Trash after use");
    }
    public void Reorder(IReadOnlyList<ShelfItem> items, int insertionIndex, string? panelId = null)
    {
        if (PanelSets.Move(State, panelId ?? State.ActivePanelId, items.Select(i => i.Id), insertionIndex)) { Change(); Notify("Panel updated"); }
    }
    public ClipDrag BeginDrag(IReadOnlyList<ShelfItem> items)
    {
        if (_activeDrag is not null) throw new InvalidOperationException("A transfer is already active.");
        return _activeDrag = new ClipDrag(this, items);
    }
    public ClipDrag? OwnDrag(IDataObject data) => _activeDrag is { } drag && data.GetDataPresent(ClipDrag.TokenFormat, false) &&
        data.GetData(ClipDrag.TokenFormat, false) is string token && token == drag.Token ? drag : null;
    internal void EndDrag(ClipDrag drag) { if (ReferenceEquals(_activeDrag, drag)) _activeDrag = null; }
    public void CompleteDrag(ClipDrag drag, DragDropEffects effect)
    {
        if (!ReferenceEquals(_activeDrag, drag)) return;
        if (drag.InternalHandled) return;
        if ((effect & DragDropEffects.Copy) != 0) ArchiveUsed(drag.Items, "Transfer finished");
        else Notify("Transfer cancelled · clips kept");
    }
    private void ArchiveUsed(IReadOnlyList<ShelfItem> items, string action)
    {
        try
        {
            var count = LibraryOperations.ArchiveUsed(State, items.Select(i => i.Id));
            if (count > 0) { Change(); if (!TrySave()) return; }
            Notify(action + (count > 0 ? $" · {count} moved to Trash · undo in Library" : " · clips kept"));
        }
        catch (InvalidOperationException ex) { Diagnostics.Record("Archive used clips", ex); Notify(action + " · Trash full; clips kept"); }
    }
    public void TogglePin(IReadOnlyList<ShelfItem> items, string? panelId = null)
    {
        if (items.Count == 0) return;
        try
        {
            var target = panelId ?? State.ActivePanelId; var members = Pins(target).Select(i => i.Id).ToHashSet();
            var pin = !items.All(i => members.Contains(i.Id));
            if (PanelSets.SetMembership(State, target, items.Select(i => i.Id), pin)) Change();
        }
        catch (Exception ex) { Error("Panel membership", ex); }
    }

    public void Remove(List<ShelfItem> items)
    {
        if (items.Count == 0) return;
        try { if (LibraryOperations.Remove(State, items.Select(i => i.Id)) > 0) { Change(); Notify("Moved to Trash · recover any time in Library"); } }
        catch (Exception ex) { Error("Remove", ex); }
    }

    public void RestoreTrash(IReadOnlyList<ShelfItem> items)
    {
        try { if (LibraryOperations.Restore(State, items.Select(i => i.Id)) > 0) { PanelSets.ApplyActive(State); Change(); Notify("Items restored"); } }
        catch (Exception ex) { Error("Restore", ex); }
    }
    public void UndoRemove()
    {
        try { if (LibraryOperations.UndoRemove(State) > 0) { PanelSets.ApplyActive(State); Change(); Notify("Items restored"); } }
        catch (Exception ex) { Error("Restore", ex); }
    }
    public void Rename(ShelfItem item, string title, string group)
    {
        try { LibraryOperations.Rename(item, title, group); Change(); Notify("Item updated"); }
        catch (Exception ex) { Error("Rename", ex); }
    }
    private void WriteClipboard(IReadOnlyList<ShelfItem> items)
    {
        if (items.Count == 1 && items[0].Clipboard is { } original) _clipboard.Restore(original);
        else Clipboard.SetDataObject(Transfer.Build(items), true);
    }

    public bool Copy(IReadOnlyList<ShelfItem> items, bool plainText = false)
    {
        if (items.Count == 0) return false;
        try
        {
            if (plainText) Clipboard.SetText(string.Join(Environment.NewLine, items.Select(i => i.FilePath ?? i.Content)));
            else WriteClipboard(items);
            Notify(items.Count == 1 && items[0].Clipboard?.Partial == true ? "Copied available formats · some app-only formats unavailable" : "Copied · ready to paste"); return true;
        }
        catch (Exception ex) { Error("Copy", ex); return false; }
    }

    public async void Paste(IReadOnlyList<ShelfItem> items) => await PasteAsync(items);
    public async System.Threading.Tasks.Task PasteAsync(IReadOnlyList<ShelfItem> items)
    {
        items = items.ToArray();
        if (IsPasting || items.Count == 0) return;
        IsPasting = true;
        try
        {
            var target = Paster?.CaptureTarget();
            // The copied payload remains available on every fallback; never restore on a timer.
            WriteClipboard(items);
            var result = Paster is null ? PasteOutcome.NoTarget : await Paster.Paste(target);
            Diagnostics.Note("Paste outcome", result.ToString());
            if (result == PasteOutcome.Requested) ArchiveUsed(items, "Paste requested");
            else Notify(result switch {
                PasteOutcome.TargetChanged => "Copied · destination changed; paste manually",
                PasteOutcome.KeysHeld => "Copied · release modifier keys and paste manually",
                _ => "Copied · focus your app and press Ctrl+V"
            });
        }
        catch (Exception ex) { Error("Paste", ex); }
        finally { IsPasting = false; }
    }

    public string Report() => Diagnostics.Report(State.Items.Count, StorageStatus);
    public byte[] BackupSnapshot() => _store.CaptureSnapshot(State);
    public void ShowPanels(IReadOnlyList<ShelfItem>? clips = null) => PanelsRequested?.Invoke(clips);
    public void PanelAction(Action action, string message)
    {
        if (_activeDrag is not null || IsPasting) { Notify("Finish the current transfer before changing panels"); return; }
        try { action(); Change(); Notify(message); }
        catch (Exception ex) { Error("Panel", ex); }
    }
    public void RequestQuit() => QuitRequested?.Invoke();
    public void Dispose()
    {
        if (_disposed) return;
        _imageStop.Cancel();
        _save.Stop(); if (_dirty) TrySave(); _disposed = true; _clipboard.Dispose();
    }
}
