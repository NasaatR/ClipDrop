using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClipDrop.Core;

public sealed class StateStore
{
    private readonly string _path;
    private bool _recovered;
    private bool _loaded;
    private int? _migrationVersion;
    private string? _migrationSource;
    private readonly object _gate = new();
    private readonly object _queue = new();
    private Task _writeTail = Task.CompletedTask;
    private static readonly JsonSerializerOptions Json = new() {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };
    public string Root { get; }
    public string? RecoveryNotice { get; private set; }

    public StateStore(string root)
    {
        Root = Path.GetFullPath(root);
        _path = Path.Combine(Root, "shelf.json");
    }

    public ShelfState Load()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Root);
            if (!File.Exists(_path))
            {
                if (File.Exists(_path + ".bak")) return Recover();
                _loaded = true;
                return new ShelfState();
            }
            try { var state = LoadFile(_path); _loaded = true; return state; }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                return Recover();
            }
        }
    }

    private ShelfState Recover()
    {
        try
        {
            var state = LoadFile(_path + ".bak");
            _recovered = true;
            _loaded = true;
            RecoveryNotice = "Recovered the previous saved shelf. The damaged file will be kept for recovery.";
            return state;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            throw new InvalidDataException("The shelf could not be read. Its files have been preserved. Back up the data folder before attempting recovery.", ex);
        }
    }

    private ShelfState LoadFile(string path)
    {
        var state = Read(path, out var originalVersion);
        if (originalVersion < ShelfState.CurrentSchema) { _migrationVersion = originalVersion; _migrationSource = path; }
        return state;
    }

    private static ShelfState Read(string path, out int originalVersion)
    {
        return ReadContents(File.ReadAllText(path), out originalVersion);
    }
    public static ShelfState ReadSnapshot(byte[] bytes) => ReadContents(System.Text.Encoding.UTF8.GetString(bytes), out _);
    private static ShelfState ReadContents(string contents, out int originalVersion)
    {
        using var document = JsonDocument.Parse(contents);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("SchemaVersion", out _) ||
            !document.RootElement.TryGetProperty("Items", out _) ||
            !document.RootElement.TryGetProperty("Settings", out _))
        {
            // Still report future schemas explicitly even when their structure differs.
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("SchemaVersion", out var version) &&
                version.TryGetInt32(out var number) && number > ShelfState.CurrentSchema)
                throw new NotSupportedException("This shelf requires a newer ClipDrop.");
            throw new InvalidDataException("The saved shelf structure is incomplete.");
        }
        var state = JsonSerializer.Deserialize<ShelfState>(contents, Json)
            ?? throw new InvalidDataException("The saved shelf is empty.");
        // A newer schema must never silently fall back to an older backup.
        if (state.SchemaVersion > ShelfState.CurrentSchema)
            throw new NotSupportedException("This shelf was saved by a newer ClipDrop. Install that version to open it.");
        originalVersion = state.SchemaVersion;
        if (state.SchemaVersion == 1) state.SchemaVersion = ShelfState.CurrentSchema;
        else if (!document.RootElement.TryGetProperty("Trash", out _)) throw new InvalidDataException("The saved trash structure is incomplete.");
        if (originalVersion is 1 or 2 or 3 or 4 or 5 or 6) state.SchemaVersion = ShelfState.CurrentSchema;
        if (state.SchemaVersion != ShelfState.CurrentSchema || state.Settings is null || state.Items is null || state.Trash is null)
            throw new InvalidDataException("Invalid shelf structure.");
        if (state.Trash.Any(t => t is null || t.Item is null || !Guid.TryParse(t.Batch, out _))) throw new InvalidDataException("Invalid trash entry.");
        var all = state.Items.Concat(state.Trash.Select(t => t.Item)).ToArray();
        if (all.Any(i => i is null || !Guid.TryParse(i.Id, out _) || i.Title is null || i.Group is null || i.Group.Length > 40 || i.Content is null || !Enum.IsDefined(i.Kind)))
            throw new InvalidDataException("Invalid shelf item.");
        if (originalVersion is 1 or 2) PinOrdering.Migrate(state);
        if (all.Select(i => i.Id).Distinct().Count() != all.Length) throw new InvalidDataException("Duplicate shelf identities.");
        foreach (var item in all)
        {
            if (item.SourceUrl is not null && (item.SourceUrl.Length > 8192 || !Uri.TryCreate(item.SourceUrl, UriKind.Absolute, out var source) || source.Scheme is not ("http" or "https")))
                throw new InvalidDataException("Invalid image source URL.");
            if (item.Kind == ItemKind.File && MediaFiles.IsImage(item.FilePath)) item.Kind = ItemKind.Image;
        }
        if (originalVersion < 5) PanelSets.Migrate(state);
        else if (!document.RootElement.TryGetProperty("Panels", out _) || !document.RootElement.TryGetProperty("ActivePanelId", out _))
            throw new InvalidDataException("The saved panel structure is incomplete.");
        var identities = all.Select(i => i.Id).ToHashSet();
        if (state.Panels is null || state.Panels.Count is < 1 or > PanelSets.Maximum ||
            state.Panels.Any(p => p is null || !Guid.TryParse(p.Id, out _) || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 60 || p.ClipIds is null ||
                p.ClipIds.Count > identities.Count || p.ClipIds.Distinct().Count() != p.ClipIds.Count || p.ClipIds.Any(id => !identities.Contains(id))) ||
            state.Panels.Select(p => p.Id).Distinct().Count() != state.Panels.Count || !state.Panels.Any(p => p.Id == state.ActivePanelId && p.ClosedUtc is null))
            throw new InvalidDataException("Invalid saved panels.");
        if (originalVersion >= 7 && document.RootElement.GetProperty("Panels").EnumerateArray().Any(p => !p.TryGetProperty("Stacks", out _)))
            throw new InvalidDataException("The saved clip stacks are incomplete.");
        if (state.Panels.Any(p => p.Stacks is null || p.Stacks.Count > p.ClipIds.Count / 2 ||
            p.Stacks.Any(s => s is null || !Guid.TryParse(s.Id, out _) || identities.Contains(s.Id) || s.ClipIds is null || s.ClipIds.Count < 2 || s.ClipIds.Any(id => !p.ClipIds.Contains(id))) ||
            p.Stacks.Select(s => s.Id).Distinct().Count() != p.Stacks.Count ||
            p.Stacks.SelectMany(s => s.ClipIds).Distinct().Count() != p.Stacks.Sum(s => s.ClipIds.Count)))
            throw new InvalidDataException("Invalid clip stacks.");
        if (state.Panels.Count(p => p.Window?.IsOpen == true) > PanelSets.MaximumWindows ||
            state.Panels.Any(p => p.Window is { } w && (w.Layout is null || string.IsNullOrWhiteSpace(w.Layout.DisplayId) ||
                w.Layout.DisplayId.Length > 1024 || w.IsOpen && (w.ClosedUtc is not null || p.ClosedUtc is not null) ||
                w.Layout.OffsetX is double x && (!double.IsFinite(x) || Math.Abs(x) > 100000) ||
                w.Layout.OffsetY is double y && (!double.IsFinite(y) || Math.Abs(y) > 100000))))
            throw new InvalidDataException("Invalid independent panel windows.");
        PanelSets.ApplyActive(state);
        var downloads = state.Settings.DownloadsLayout;
        if (state.Settings.DownloadsSource is not ("Auto" or "Chrome" or "Folder") || state.Settings.DownloadsFolder is null || state.Settings.DownloadsFolder.Length > 32767 ||
            downloads is null || downloads.DisplayId is null || downloads.DisplayId.Length > 1024 ||
            downloads.OffsetX is double dx && (!double.IsFinite(dx) || Math.Abs(dx) > 100000) ||
            downloads.OffsetY is double dy && (!double.IsFinite(dy) || Math.Abs(dy) > 100000))
            throw new InvalidDataException("Invalid downloads preferences.");
        foreach (var item in all.Where(i => i.Clipboard is not null))
        {
            var snapshot = item.Clipboard!;
            if (snapshot.Formats is null || snapshot.Formats.Count is < 1 or > 128 || snapshot.Formats.Sum(f => f is null ? 0L : f.Length) > 64L * 1024 * 1024 ||
                snapshot.Formats.Any(f => f is null || f.Hash is null || f.Hash.Length != 64 || !f.Hash.All(Uri.IsHexDigit) || f.Length is < 1 or > 64 * 1024 * 1024 || f.Name is null || f.Name.Length > 255))
                throw new InvalidDataException("Invalid clipboard snapshot.");
        }
        if (state.Settings.SavedStyles is null || state.Settings.SavedStyles.Count > 20 ||
            state.Settings.SavedStyles.Any(s => s is null || !Guid.TryParse(s.Id, out _) || string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 40 || s.Style is null) ||
            state.Settings.SavedStyles.Select(s => s.Id).Distinct().Count() != state.Settings.SavedStyles.Count)
            throw new InvalidDataException("Invalid saved styles.");
        if (state.Settings.Docks is null || state.Settings.Docks.Count > 64 || state.Settings.HomeDisplayId is null ||
            state.Settings.Docks.Any(d => d is null || string.IsNullOrWhiteSpace(d.DisplayId) || d.DisplayId.Length > 1024 ||
                d.OffsetX is double x && (!double.IsFinite(x) || Math.Abs(x) > 100000) || d.OffsetY is double y && (!double.IsFinite(y) || Math.Abs(y) > 100000)) ||
            state.Settings.Docks.Select(d => d.DisplayId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != state.Settings.Docks.Count)
            throw new InvalidDataException("Invalid monitor layout.");
        foreach (var style in state.Settings.SavedStyles.Select(s => s.Style).Prepend(state.Settings))
            if (style.PngIcons is null || style.PngIcons.Count > 64 || style.PngIcons.Any(p => p.Key.Length is < 1 or > 40 || p.Value is null || p.Value.Length != 64 || !p.Value.All(Uri.IsHexDigit)))
                throw new InvalidDataException("Invalid icon preferences.");
        return state;
    }

    public void Save(ShelfState state) => SaveAsync(state).GetAwaiter().GetResult();

    public Task SaveAsync(ShelfState state)
    {
        // Encode while the caller owns mutable state. The disk worker only receives
        // immutable bytes, and the queue preserves call order even after a failure.
        var bytes = CaptureSnapshot(state);
        lock (_queue)
        {
            return _writeTail = _writeTail.ContinueWith(_ => SaveBytes(bytes),
                System.Threading.CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }
    public byte[] CaptureSnapshot(ShelfState state)
    {
        if (!_loaded) throw new InvalidOperationException("Load the shelf before capturing it.");
        PanelSets.CaptureActive(state);
        return JsonSerializer.SerializeToUtf8Bytes(state, Json);
    }
    private void SaveBytes(byte[] bytes)
    {
        lock (_gate)
        {
            if (!_loaded) throw new InvalidOperationException("Load the shelf before saving it.");
            // Keep an immutable pre-migration copy; ordinary .bak rotates on every save.
            if (_migrationVersion is int old && _migrationSource is string source)
            {
                var backup = _path + ".schema-" + old + "-before-v" + ShelfState.CurrentSchema + ".bak";
                if (!File.Exists(backup)) File.Copy(source, backup, false);
                _migrationVersion = null; _migrationSource = null;
            }
            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    output.Write(bytes);
                    output.Flush(true);
                }
                if (_recovered && File.Exists(_path))
                    File.Move(_path, _path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
                else File.Move(temporary, _path);
                _recovered = false;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
