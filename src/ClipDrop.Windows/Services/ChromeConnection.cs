using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public static class ChromeConnection
{
    public static string CatalogFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NasaatR", "ClipDrop", "browser-downloads");
    public static void Connect()
    {
        var root = AppContext.BaseDirectory;
        var host = Path.Combine(root, "ClipDrop.BrowserHost.exe");
        var extension = Path.Combine(root, "browser", "chrome");
        if (!File.Exists(host) || !File.Exists(Path.Combine(extension, "manifest.json"))) throw new FileNotFoundException("Use the complete ClipDrop installer or portable package to connect Chrome.");
        var manifest = Path.Combine(root, BrowserDownloads.HostName + ".json");
        if (!File.Exists(manifest)) throw new FileNotFoundException("The native host manifest is missing. Reinstall the complete ClipDrop package.");
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
        using var key = hive.CreateSubKey(@"Software\Google\Chrome\NativeMessagingHosts\" + BrowserDownloads.HostName);
        key.SetValue("", manifest, RegistryValueKind.String);
        Process.Start(new ProcessStartInfo(extension) { UseShellExecute = true });
        Process.Start(new ProcessStartInfo(Path.Combine(root, "browser", "CONNECT-CHROME.html")) { UseShellExecute = true });
    }
}
