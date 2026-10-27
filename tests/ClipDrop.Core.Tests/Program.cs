using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClipDrop.Core;

var root = Path.Combine(Path.GetTempPath(), "clipdrop-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
void Throws<T>(Action action, string name) where T : Exception
{ try { action(); } catch (T) { Check(true, name); return; } throw new Exception("Expected failure: " + name); }
try
{
    LayoutChecks.Run(root, Check);
    var store = new StateStore(root);
    var state = store.Load();
    Check(state.Items.Count == 0, "Empty installation starts with a real empty shelf");
    state.Settings.RowSpacing = 0; state.Settings.AlwaysOnTop = false; state.Settings.DockSpacing = 0; state.Settings.DockOpacity = 0;
    var text = ShelfItem.FromText("  Bonjour\r\nПривет 👋  ");
    Check(text.Content == "  Bonjour\r\nПривет 👋  ", "Text whitespace and Unicode are lossless");
    Check(ShelfItem.FromText("https://example.com/path").Kind == ItemKind.Link, "HTTP links are recognized");
    Check(ShelfItem.FromText("javascript:alert(1)").Kind == ItemKind.Text, "Executable URL schemes remain plain text");
    Check(ShelfItem.FromText("https://example.com/\r\nIconFile=anything").Kind == ItemKind.Text, "Multiline clipboard text cannot become a URL shortcut");
    state.Items.Add(text); store.Save(state);
    var restored = new StateStore(root).Load();
    Check(restored.Items[0].Content == text.Content && restored.Settings.RowSpacing == 0 && !restored.Settings.AlwaysOnTop && restored.Settings.DockSpacing == 0 && restored.Settings.DockOpacity == 0,
        "Save and reload preserve text, zero and false settings");
    state.Items.Add(ShelfItem.FromText("Second revision")); store.Save(state);
    File.WriteAllText(Path.Combine(root, "shelf.json"), "{broken");
    var recovery = new StateStore(root);
    var previous = recovery.Load();
    Check(previous.Items.Count == 1 && recovery.RecoveryNotice is not null, "Corrupt current state recovers the last valid backup");
    recovery.Save(previous);
    Check(Directory.GetFiles(root, "*.corrupt-*").Length == 1, "Recovery preserves damaged bytes for inspection");
    var recoveredAgain = new StateStore(root).Load();
    Check(recoveredAgain.Items.Count == 1, "Recovered state can be saved and reopened");
    File.WriteAllText(Path.Combine(root, "shelf.json"), "{\"SchemaVersion\":99}");
    Throws<NotSupportedException>(() => new StateStore(root).Load(), "A future schema cannot silently load an older backup");
    Check(File.ReadAllText(Path.Combine(root, "shelf.json")).Contains("99"), "Future schema bytes are preserved");
    var isolated = new StateStore(Path.Combine(root, "isolated")); isolated.Load();
    File.WriteAllText(Path.Combine(isolated.Root, "shelf.json"), "invalid");
    Throws<InvalidDataException>(() => new StateStore(isolated.Root).Load(), "Unrecoverable corruption is never replaced with an empty shelf");
    File.WriteAllText(Path.Combine(isolated.Root, "shelf.json"), "{}");
    Throws<InvalidDataException>(() => new StateStore(isolated.Root).Load(), "An incomplete JSON object cannot masquerade as an empty saved shelf");
    var legacy = JsonSerializer.Deserialize<ShelfSettings>("{\"Theme\":\"Light\",\"DockOpacity\":0,\"DockSpacing\":0}")!;
    Check(legacy.Theme == "Light" && legacy.DockOpacity == 0 && legacy.DockSpacing == 0 && legacy.LibraryFollowsDock && legacy.AnimationsEnabled && legacy.SavedStyles.Count == 0,
        "Legacy appearance keeps explicit zero values and gains safe defaults");
    var styled = new ShelfSettings { Left = 321, DockTop = 456, LibraryFollowsDock = false, AnimationsEnabled = false, StartHidden = true, DockColor = "#224466", DockSpacing = 0, DockOpacity = 0 };
    var snapshot = styled.Snapshot();
    AppearanceStyle.BuiltIns[2].Style.CopyTo(styled);
    Check(styled.Theme == "Midnight" && styled.Left == 321 && styled.DockTop == 456 && !styled.LibraryFollowsDock && !styled.AnimationsEnabled && styled.StartHidden,
        "Style presets never reset behavior, animation preference or window placement");
    snapshot.CopyTo(styled);
    Check(styled.DockSpacing == 0 && styled.DockOpacity == 0 && styled.DockColor == "#224466", "Custom style restores exact appearance including zero values");
    styled.SavedStyles.Add(new SavedStyle { Name = "My style", Style = snapshot });
    var stylesStore = new StateStore(Path.Combine(root, "styles")); var stylesState = stylesStore.Load(); stylesState.Settings = styled; stylesStore.Save(stylesState);
    var stylesReloaded = new StateStore(stylesStore.Root).Load().Settings;
    Check(stylesReloaded.SavedStyles.Single().Style.DockOpacity == 0 && stylesReloaded.SavedStyles.Single().Name == "My style" && !stylesReloaded.LibraryFollowsDock && !stylesReloaded.AnimationsEnabled,
        "Named styles and detached/motion preferences survive restart");
    Check(AppearanceStyle.TryColor(" #ab09fF ", out var color) && color == "#AB09FF" && AppearanceStyle.TryColor("", out _) && !AppearanceStyle.TryColor("#GG0000", out _) && !AppearanceStyle.TryColor("red", out _),
        "Custom colors accept normalized RGB or automatic, reject invalid input");
    Check(MediaFiles.IsVideo("C:\\Media\\clip.MP4") && MediaFiles.IsVideo("clip.mov") && MediaFiles.IsVideo("clip.webm") && !MediaFiles.IsVideo("notes.mp4.txt") && !MediaFiles.IsVideo(null),
        "Video detection is case-insensitive without treating text files as movies");
    var trashStore = new StateStore(Path.Combine(root, "trash")); var trashState = trashStore.Load();
    var keep = ShelfItem.FromText("Keep me after restart"); trashState.Items.Add(keep);
    LibraryOperations.Rename(keep, "Saved reference", "Design");
    LibraryOperations.Remove(trashState, new[] { keep.Id }); trashStore.Save(trashState);
    var trashReload = new StateStore(trashStore.Root).Load();
    Check(trashReload.Items.Count == 0 && trashReload.Trash.Single().Item.Content == keep.Content && trashReload.Trash.Single().Item.Group == "Design", "Trash retains content, name and group after restart");
    Check(LibraryOperations.UndoRemove(trashReload) == 1 && trashReload.Items.Single().Id == keep.Id && trashReload.Trash.Count == 0, "Undo removal restores the persistent deletion batch after restart");
    var legacyRoot = Path.Combine(root, "legacy-library"); Directory.CreateDirectory(legacyRoot);
    File.WriteAllText(Path.Combine(legacyRoot, "shelf.json"), "{\"SchemaVersion\":1,\"Items\":[],\"Settings\":{\"DockOpacity\":0,\"DockSpacing\":0}}");
    var legacyStore = new StateStore(legacyRoot); var migrated = legacyStore.Load(); legacyStore.Save(migrated);
    Check(migrated.SchemaVersion == ShelfState.CurrentSchema && migrated.Trash.Count == 0 && migrated.Settings.DockOpacity == 0 && File.ReadAllText(Path.Combine(legacyRoot, "shelf.json.bak")).Contains("\"SchemaVersion\":1"), "Schema-1 upgrade retains explicit preferences and an untouched old backup");
    var assets = new AssetStore(Path.Combine(root, "assets"));
    var blobBytes = new byte[] { 0, 7, 128, 255 };
    var hash = assets.WriteBlob(blobBytes); var blob = new ClipboardFormatBlob { Name = "Test", Hash = hash, Length = 4 };
    Check(assets.ReadBlob(blob).SequenceEqual(blobBytes), "Clipboard format blobs retain exact binary content");
    blob.Hash = "../escape";
    Throws<InvalidDataException>(() => assets.ReadBlob(blob), "Clipboard assets reject paths outside their managed directory");
    var exported = assets.ExportText(text);
    Check(File.ReadAllText(exported) == text.Content, "Exported text is lossless");
    Check(assets.ExportText(text) == exported && File.Exists(exported), "Repeated export keeps a stable file alive");
    var path = Path.Combine(root, "original.psd"); File.WriteAllText(path, "original");
    var file = ShelfItem.FromPath(path);
    Check(file.SameContent(ShelfItem.FromPath(path)) && !file.OwnedAsset, "Original files are referenced and can be deduplicated");
    state.Items.Clear(); state.Items.Add(file); state.Items.Remove(file);
    Check(File.ReadAllText(path) == "original", "Removing a shelf reference never removes its original");
    text.Id = "../../escape";
    Throws<InvalidDataException>(() => assets.ExportText(text), "Export identities cannot escape the asset directory");
    Console.WriteLine($"{passed} core checks passed.");
    return 0;
}
finally { Directory.Delete(root, true); }
