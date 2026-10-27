using System;
using System.IO;
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
    var store = new StateStore(root);
    var state = store.Load();
    Check(state.Items.Count == 0, "Empty installation starts with a real empty shelf");
    state.Settings.RowSpacing = 0; state.Settings.AlwaysOnTop = false;
    var text = ShelfItem.FromText("  Bonjour\r\nПривет 👋  ");
    Check(text.Content == "  Bonjour\r\nПривет 👋  ", "Text whitespace and Unicode are lossless");
    Check(ShelfItem.FromText("https://example.com/path").Kind == ItemKind.Link, "HTTP links are recognized");
    Check(ShelfItem.FromText("javascript:alert(1)").Kind == ItemKind.Text, "Executable URL schemes remain plain text");
    state.Items.Add(text); store.Save(state);
    var restored = new StateStore(root).Load();
    Check(restored.Items[0].Content == text.Content && restored.Settings.RowSpacing == 0 && !restored.Settings.AlwaysOnTop,
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
    var assets = new AssetStore(Path.Combine(root, "assets"));
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
