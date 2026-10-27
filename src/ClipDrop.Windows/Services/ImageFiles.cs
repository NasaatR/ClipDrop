using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace ClipDrop.Windows.Services;

public static class ImageFiles
{
    public const int MaximumBytes = 64 * 1024 * 1024;

    public static BitmapSource Load(byte[] bytes, int decodeWidth = 0)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Image exceeds the 64 MB import limit.");
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
