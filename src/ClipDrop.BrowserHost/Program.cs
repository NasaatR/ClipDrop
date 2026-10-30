using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ClipDrop.Core;

// Chrome launches this small GUI-subsystem host with inherited binary pipes.
// No WPF, console allocation, single-instance wake, network or executable paths
// from the request. Nothing except framed JSON is written to stdout.
if (args.Length == 0 || args[0] != BrowserDownloads.Origin) return 2;
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
try
{
    var bytes = await BrowserDownloads.ReadFrame(Console.OpenStandardInput(), timeout.Token);
    var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NasaatR", "ClipDrop", "browser-downloads");
    // Serialize cross-profile admission and per-profile revision checks across
    // short-lived hosts. The catalog never shares the app's metadata lock.
    using var mutex = new Mutex(false, "Local\\NasaatR.ClipDrop.BrowserDownloads");
    bool owned;
    try { owned = mutex.WaitOne(3000); } catch (AbandonedMutexException) { owned = true; }
    if (!owned) throw new IOException("Browser catalog busy.");
    bool changed;
    try { changed = BrowserDownloads.Save(folder, bytes); } finally { mutex.ReleaseMutex(); }
    var reply = JsonSerializer.SerializeToUtf8Bytes(new { ok = true, changed });
    await BrowserDownloads.WriteFrame(Console.OpenStandardOutput(), reply, timeout.Token);
    return 0;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or OperationCanceledException)
{
    try { await BrowserDownloads.WriteFrame(Console.OpenStandardOutput(), JsonSerializer.SerializeToUtf8Bytes(new { ok = false, error = "Downloads could not be saved. Reconnect ClipDrop and try again." }), CancellationToken.None); }
    catch (Exception sendError) when (sendError is IOException or InvalidDataException) { }
    return 1;
}
