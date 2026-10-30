using System;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using SkiaSharp;

namespace ClipDrop.Windows.Services;

public static class ImageFiles
{
    public const int MaximumBytes = 64 * 1024 * 1024;

    public static BitmapSource Load(byte[] bytes, int decodeWidth = 0)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Image exceeds the 64 MB import limit.");
        if (bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP")
            return LoadSkia(bytes, decodeWidth);
        try { return LoadWpf(bytes, decodeWidth); }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException)
        { return LoadSkia(bytes, decodeWidth); }
    }
    private static BitmapSource LoadWpf(byte[] bytes, int decodeWidth)
    {
        using var input = new MemoryStream(bytes, false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
        image.StreamSource = input;
        image.EndInit();
        if ((long)image.PixelWidth * image.PixelHeight > 64_000_000)
            throw new InvalidDataException("Image exceeds the 64 megapixel limit.");
        image.Freeze();
        return image;
    }

    private static BitmapSource LoadSkia(byte[] bytes, int decodeWidth)
    {
        using var input = new MemoryStream(bytes, false);
        using var codec = SKCodec.Create(input) ?? throw new InvalidDataException("Image format is unavailable or invalid.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 64_000_000)
            throw new InvalidDataException("Image exceeds the 64 megapixel limit.");
        if (decodeWidth > 0 && info.Width > decodeWidth)
        {
            var scaled = codec.GetScaledDimensions(decodeWidth / (float)info.Width);
            info = info.WithSize(scaled.Width, scaled.Height);
        }
        info = info.WithColorType(SKColorType.Bgra8888).WithAlphaType(SKAlphaType.Premul);
        using var pixels = new SKBitmap(info);
        if (codec.GetPixels(info, pixels.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("Image decoding did not complete.");
        if (decodeWidth > 0 && pixels.Width > decodeWidth)
        {
            var height = Math.Max(1, (int)Math.Round(pixels.Height * decodeWidth / (double)pixels.Width));
            using var small = new SKBitmap(new SKImageInfo(decodeWidth, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(small))
                canvas.DrawBitmap(pixels, new SKRect(0, 0, decodeWidth, height), new SKSamplingOptions(SKFilterMode.Linear));
            return Freeze(small);
        }
        return Freeze(pixels);
    }
    private static BitmapSource Freeze(SKBitmap pixels)
    {
        var result = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Pbgra32, null,
            pixels.GetPixels(), checked(pixels.RowBytes * pixels.Height), pixels.RowBytes);
        result.Freeze(); return result;
    }
    public static string Extension(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, false); using var codec = SKCodec.Create(input);
        return codec?.EncodedFormat.ToString().ToLowerInvariant() switch {
            "jpeg" => ".jpg", "webp" => ".webp", "gif" => ".gif", "bmp" => ".bmp", "ico" => ".ico", "avif" => ".avif", "heif" => ".heif", _ => ".png"
        };
    }

    public static byte[] Encode(BitmapSource bitmap)
    {
        if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 64_000_000)
            throw new InvalidDataException("Image exceeds the 64 megapixel limit.");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        if (output.Length > MaximumBytes) throw new InvalidDataException("Image exceeds the 64 MB import limit.");
        return output.ToArray();
    }

    public static byte[] ReadStream(Stream stream)
    {
        var position = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                if (output.Length + count > MaximumBytes) throw new InvalidDataException("Image exceeds the 64 MB import limit.");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        finally { if (stream.CanSeek) stream.Position = position; }
    }
}
