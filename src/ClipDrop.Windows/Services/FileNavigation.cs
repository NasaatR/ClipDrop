using System;
using System.Diagnostics;
using System.IO;

namespace ClipDrop.Windows.Services;

public static class FileNavigation
{
    public static ProcessStartInfo ContainingFolder(string path, bool windowsExplorer = false)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path)) throw new FileNotFoundException("The downloaded file is no longer available.");
        var folder = Path.GetDirectoryName(path) ?? throw new DirectoryNotFoundException();
        // Let the shell use the configured default folder verb/handler. Calling
        // explorer.exe or SHOpenFolderAndSelectItems bypasses replacements.
        if (!windowsExplorer) return new ProcessStartInfo(folder) { UseShellExecute = true };
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        start.ArgumentList.Add("/select,"); start.ArgumentList.Add(path); return start;
    }
}
