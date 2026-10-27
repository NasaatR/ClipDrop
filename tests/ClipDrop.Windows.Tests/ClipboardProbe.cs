using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

internal static class ClipboardProbe
{
    public static void Run(string root, Action<bool, string> check)
    {
        var folder = Path.Combine(root, "native-snapshot"); var store = new StateStore(folder); var state = store.Load();
        using (var session = new ShelfSession(store, state))
        {
            var data = new DataObject(); data.SetText("  Привет\r\nBonjour 👋  ");
            data.SetData(DataFormats.Rtf, @"{\rtf1\ansi\b Bold\b0}");
            data.SetData(DataFormats.Html, "Version:0.9\r\n<!--StartFragment--><b>Bold</b><!--EndFragment-->");
            data.SetData("ClipDrop.Test.Opaque", new MemoryStream(new byte[] { 0, 1, 34, 255, 0, 7 }), false);
            Clipboard.SetDataObject(data, true);
            using var native = new NativeClipboard(new AssetStore(Path.Combine(folder, "assets")));
            Console.WriteLine("CHECKPOINT native capture source");
            var original = native.Capture() ?? throw new Exception("Native clipboard snapshot missing");
            Console.WriteLine("CHECKPOINT Save clipboard snapshot");
            session.SaveClipboard();
            check(state.Items.Count == 1 && state.Items[0].Clipboard?.Formats.Any(f => f.Name == "ClipDrop.Test.Opaque") == true, "Save captures original registered clipboard bytes alongside Unicode");
            session.TrySave();
            Clipboard.SetText("Replace clipboard");
            check(session.Copy(state.Items), "Copy restores a saved native clipboard item");
            Console.WriteLine("CHECKPOINT native capture replay");
            var after = native.Capture() ?? throw new Exception("Restored snapshot missing");
            check(original.Formats.All(f => after.Formats.Any(a => a.Name == f.Name && a.StandardId == f.StandardId && a.Hash == f.Hash)), "Native copy preserves HTML, RTF, opaque bytes and text byte-for-byte");
            check(Clipboard.GetText() == "  Привет\r\nBonjour 👋  " && !Clipboard.ContainsFileDropList(), "Native copy keeps Unicode whitespace and never creates TXT output");
        }
        var loaded = new StateStore(folder); var recovered = loaded.Load();
        using (var session = new ShelfSession(loaded, recovered))
        {
            Clipboard.SetText("Changed again");
            check(session.Copy(recovered.Items) && Clipboard.ContainsData("ClipDrop.Test.Opaque") && Clipboard.ContainsData(DataFormats.Rtf), "Original clipboard formats survive application restart");
        }
        var path = Path.Combine(folder, "original.txt"); File.WriteAllText(path, "Original file content");
        var service = new TransferService(new AssetStore(Path.Combine(folder, "assets")));
        var fileData = service.Build(new[] { ShelfItem.FromPath(path) });
        check(fileData.GetFileDropList().Cast<string>().Single() == path && !fileData.GetDataPresent(DataFormats.UnicodeText), "A copied TXT file remains the original file, without converting its contents");
        var owned = ShelfItem.FromPath(path); owned.OwnedAsset = true;
        check(service.Build(new[] { owned }).GetFileDropList().Cast<string>().Single() == path,
            "Materialized virtual documents retain their file payload");
        Clipboard.Clear();
    }
}
