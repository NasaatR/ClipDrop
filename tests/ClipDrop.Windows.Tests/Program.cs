using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class Program
{
    private static int _passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); _passed++; }
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--paste-target") return PasteProbe.RunTarget(args[1]);
        PasteProbe.PolicyChecks(Check);
        var root = Path.Combine(Path.GetTempPath(), "clipdrop-windows-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/qa");
        Directory.CreateDirectory(output);
        try
        {
            var app = new App(); app.InitializeComponent();
            var assets = new AssetStore(Path.Combine(root, "assets"));
            var transfers = new TransferService(assets);
            var note = ShelfItem.FromText("Hello\r\nПривет — Bonjour 👋");
            var textData = transfers.Build(new[] { note });
            Check(textData.GetText() == note.Content && textData.GetFileDropList().Count == 1, "A text card exposes Unicode text and a real file simultaneously");
            Check(File.ReadAllText(textData.GetFileDropList()[0]!) == note.Content, "Text drag file contains original contents");
            var linkData = transfers.Build(new[] { ShelfItem.FromText("https://example.com") });
            Check(linkData.GetDataPresent("UniformResourceLocatorW") && linkData.GetText() == "https://example.com", "Links expose URL and Unicode representations");
            var pixels = new byte[] { 0, 0, 255, 128, 0, 255, 0, 255 };
            var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
            var png = ImageFiles.Encode(bitmap);
            var incoming = new DataObject(); incoming.SetData("PNG", new MemoryStream(png), false); incoming.SetText("https://example.com/image.png");
            var image = transfers.Import(incoming).Single();
            Check(image.Kind == ItemKind.Image && image.OwnedAsset, "Browser PNG wins over its accompanying URL");
            var exportedImage = transfers.Build(new[] { image });
            var pngStream = exportedImage.GetData("PNG", false) as Stream
                ?? throw new Exception("PNG stream missing from image output");
            var returned = ImageFiles.ReadStream(pngStream);
            Check(returned.SequenceEqual(png), "Original PNG bytes survive import and export unchanged");
            var decoded = new PngBitmapDecoder(new MemoryStream(returned), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            var converted = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0); var actual = new byte[8]; converted.CopyPixels(actual, 8, 0);
            Check(actual[3] == 128 && actual[7] == 255, "PNG alpha survives the transfer pipeline");
            var source = Path.Combine(root, "source.txt"); File.WriteAllText(source, "kept");
            var file = ShelfItem.FromPath(source);
            var mixed = transfers.Build(new[] { file, image, note });
            Check(mixed.GetFileDropList().Count == 3, "Mixed multi-selection provides every file payload");
            Check(File.ReadAllText(source) == "kept", "Transfer preparation preserves original files");
            try { transfers.Build(new[] { new ShelfItem { Kind = ItemKind.File, FilePath = Path.Combine(root, "missing") } }); throw new Exception("Missing path was accepted"); }
            catch (FileNotFoundException) { Check(true, "Missing originals fail explicitly before drag begins"); }
            // A real OLE clipboard roundtrip on the Windows runner; no external app automation is implied.
            Clipboard.SetDataObject(textData, true);
            Check(Clipboard.GetText() == note.Content && Clipboard.ContainsFileDropList(), "System clipboard roundtrip preserves text and file formats");
            Check(transfers.Import(Clipboard.GetDataObject() ?? throw new Exception("Clipboard unavailable")).Single().Content == note.Content,
                "Copying and pasting a ClipDrop text card keeps it a text card");
            Clipboard.Clear();
            var store = new StateStore(root); var state = store.Load();
            state.Items.AddRange(new[] { note, image, file, ShelfItem.FromText("A long Cyrillic reference: Проверка очень длинного названия изображения для рабочего проекта") });
            state.Items[0].IsPinned = true;
            var window = new MainWindow(store, state, false); window.Show();
            Render(window, Path.Combine(output, "shelf-dark.png"));
            var dock = new DockWindow(window.Session, window.ShowShelf, window.OpenSettings);
            dock.ShowDock();
            Render(dock, Path.Combine(output, "dock-dark.png"));
            Check(dock.ActualHeight < 90, "The floating dock stays compact with real pins");
            PasteProbe.Run(dock, root, note.Content, Check);
            UiProbe.Placement(window, dock, Check);
            var clipboardNote = ShelfItem.FromText("Shared session checkpoint");
            window.Session.Add(new[] { clipboardNote });
            Check(state.Items.Any(i => i.Id == clipboardNote.Id && i.IsPinned), "Dock and Library share one collection and pin new saves");
            window.Session.Remove(new System.Collections.Generic.List<ShelfItem> { clipboardNote });
            window.Session.UndoRemove();
            Check(state.Items.Any(i => i.Id == clipboardNote.Id), "Library removal remains undoable through the shared session");
            window.Width = 320; window.Height = 360;
            Render(window, Path.Combine(output, "shelf-narrow.png"));
            state.Settings.Theme = "Light"; window.Session.Change();
            Render(dock, Path.Combine(output, "dock-light.png"));
            Render(window, Path.Combine(output, "shelf-light.png"));
            var settings = new SettingsWindow(state.Settings, () => window.Session.Change(), () => true, () => "test report", root) { Owner = window };
            settings.Show(); Render(settings, Path.Combine(output, "settings-open.png"));
            UiProbe.Styles(settings, window, dock, Check);
            UiProbe.Pump(250);
            Render(dock, Path.Combine(output, "dock-independent.png"));
            Render(window, Path.Combine(output, "library-midnight.png"));
            Render(settings, Path.Combine(output, "settings-styles.png"));
            var stylePicker = UiProbe.Find<ComboBox>(settings, c => AutomationProperties.GetName(c) == "Style preset");
            stylePicker.IsDropDownOpen = true; UiProbe.Pump();
            var popup = (Popup)stylePicker.Template.FindName("PART_Popup", stylePicker);
            RenderElement((FrameworkElement)popup.Child, Path.Combine(output, "settings-preset-menu.png"));
            stylePicker.IsDropDownOpen = false;
            settings.Close();
            Check(window.ActualWidth >= 320 && window.ActualHeight >= 300, "Real WPF surface renders at its minimum supported size");
            dock.Hide();
            window.Session.TrySave();
            app.Shutdown();
            Console.WriteLine($"{_passed} Windows checks passed. UI captures: {output}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    private static void Render(Window window, string path)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        RenderElement(content, path, window.Background);
    }
    private static void RenderElement(FrameworkElement content, string path, Brush? background = null)
    {
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var surface = new DrawingVisual();
        using (var drawing = surface.RenderOpen())
        {
            var bounds = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
            drawing.DrawRectangle(background, null, bounds);
            drawing.DrawRectangle(new VisualBrush(content), null, bounds);
        }
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
