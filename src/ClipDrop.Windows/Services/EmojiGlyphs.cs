using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace ClipDrop.Windows.Services;

// WPF TextBlock does not paint COLR palettes. For this fixed set of single-codepoint
// emoji, compose the installed Segoe UI Emoji outlines with their own CPAL colors.
// No Microsoft font or third-party artwork is copied into the application.
public static class EmojiGlyphs
{
    private static readonly Dictionary<string, ImageSource?> Cache = new();
    private static GlyphTypeface? _font;
    private static byte[]? _colr, _cpal;
    private static bool _initialized;
    private static int U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
    private static int U32(byte[] data, int offset) => checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4)));
    private static void Initialize()
    {
        if (_initialized) return; _initialized = true;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguiemj.ttf");
        if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024 * 1024) return;
        _font = new GlyphTypeface(new Uri(path));
        var bytes = File.ReadAllBytes(path); var count = U16(bytes, 4);
        for (var i = 0; i < count; i++)
        {
            var entry = 12 + i * 16;
            var tag = System.Text.Encoding.ASCII.GetString(bytes, entry, 4);
            if (tag is not ("COLR" or "CPAL")) continue;
            var offset = U32(bytes, entry + 8); var length = U32(bytes, entry + 12);
            var table = bytes.AsSpan(offset, length).ToArray();
            if (tag == "COLR") _colr = table; else _cpal = table;
        }
    }
    public static ImageSource? Get(string emoji)
    {
        if (Cache.TryGetValue(emoji, out var cached)) return cached;
        ImageSource? result = null;
        try
        {
            Initialize();
            if (_font is not null && _colr is { } colr && _cpal is { } cpal &&
                _font.CharacterToGlyphMap.TryGetValue(char.ConvertToUtf32(emoji, 0), out var glyph))
            {
                var bases = U32(colr, 4); var layers = U32(colr, 8);
                var palette = U32(cpal, 8); var firstColor = U16(cpal, 12); var entries = U16(cpal, 2);
                for (var i = 0; i < U16(colr, 2); i++)
                {
                    var record = bases + i * 6;
                    if (U16(colr, record) != glyph) continue;
                    var first = U16(colr, record + 2); var count = U16(colr, record + 4);
                    if (count > 512 || first + count > U16(colr, 12)) break;
                    var drawing = new DrawingGroup();
                    for (var j = 0; j < count; j++)
                    {
                        var layer = layers + (first + j) * 4;
                        var index = U16(colr, layer + 2); var color = Colors.Gray;
                        if (index != 0xFFFF)
                        {
                            if (index >= entries) throw new InvalidDataException("Emoji palette index out of range.");
                            var p = palette + (firstColor + index) * 4;
                            color = Color.FromArgb(cpal[p + 3], cpal[p + 2], cpal[p + 1], cpal[p]);
                        }
                        var brush = new SolidColorBrush(color); brush.Freeze();
                        var shape = _font.GetGlyphOutline((ushort)U16(colr, layer), 96, 96); shape.Freeze();
                        drawing.Children.Add(new GeometryDrawing(brush, null, shape));
                    }
                    if (drawing.Children.Count > 0) { drawing.Freeze(); var image = new DrawingImage(drawing); image.Freeze(); result = image; }
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException or IndexOutOfRangeException or System.Runtime.InteropServices.COMException) { }
        Cache[emoji] = result; return result;
    }
}
