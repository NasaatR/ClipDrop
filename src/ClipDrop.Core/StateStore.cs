using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipDrop.Core;

public sealed class StateStore
{
    private readonly string _path;
    private bool _recovered;
    private bool _loaded;
    private readonly object _gate = new();
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
            try { var state = Read(_path); _loaded = true; return state; }
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
            var state = Read(_path + ".bak");
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

    private static ShelfState Read(string path)
    {
        var contents = File.ReadAllText(path);
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
        if (state.SchemaVersion != ShelfState.CurrentSchema || state.Settings is null || state.Items is null)
            throw new InvalidDataException("Invalid shelf structure.");
        if (state.Items.Any(i => i is null || !Guid.TryParse(i.Id, out _) || i.Title is null ||
            i.Content is null || !Enum.IsDefined(i.Kind)))
            throw new InvalidDataException("Invalid shelf item.");
        if (state.Items.Select(i => i.Id).Distinct().Count() != state.Items.Count)
            throw new InvalidDataException("Duplicate shelf identities.");
        if (state.Settings.SavedStyles is null || state.Settings.SavedStyles.Count > 20 ||
            state.Settings.SavedStyles.Any(s => s is null || !Guid.TryParse(s.Id, out _) || string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 40 || s.Style is null) ||
            state.Settings.SavedStyles.Select(s => s.Id).Distinct().Count() != state.Settings.SavedStyles.Count)
            throw new InvalidDataException("Invalid saved styles.");
        return state;
    }

    public void Save(ShelfState state)
    {
        lock (_gate)
        {
            if (!_loaded) throw new InvalidOperationException("Load the shelf before saving it.");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Json);
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
