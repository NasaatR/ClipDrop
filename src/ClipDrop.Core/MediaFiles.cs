using System;
using System.Collections.Generic;
using System.IO;
namespace ClipDrop.Core;

public static class MediaFiles
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) {
        ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".avif", ".heic", ".heif", ".ico"
    };
    public static bool IsImage(string? path) => path is not null && ImageExtensions.Contains(Path.GetExtension(path));
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) {
        ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".mpg", ".mpeg", ".mts", ".m2ts"
    };
    public static bool IsVideo(string? path) => path is not null && VideoExtensions.Contains(Path.GetExtension(path));
}
