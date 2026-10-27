using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ClipDrop.Windows.Services;

public sealed class Diagnostics
{
    private readonly Queue<string> _events = new();
    public static string Version => typeof(Diagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion.Split('+')[0] ?? "unknown";

    public void Record(string operation, Exception error)
    {
        var message = error.Message.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        message = Regex.Replace(message, @"https?://\S+", "[URL]");
        message = Regex.Replace(message, @"[A-Za-z]:\\[^\r\n\""<>|]*", "[path]");
        if (message.Length > 500) message = message[..500];
        _events.Enqueue($"{DateTimeOffset.UtcNow:O} | {operation} | {error.GetType().Name} | 0x{error.HResult:X8} | {message}");
        while (_events.Count > 30) _events.Dequeue();
    }

    public string Report(int count, string state) =>
        $"ClipDrop {Version}\nMade by Nasaat\nOS: {RuntimeInformation.OSDescription}\n" +
        $"Architecture: {RuntimeInformation.ProcessArchitecture}\nRuntime: {RuntimeInformation.FrameworkDescription}\n" +
        $"Items: {count}\nStorage: {state}\nUTC: {DateTimeOffset.UtcNow:O}\n\n" +
        "Recent errors (no clipboard or item contents are recorded):\n" + string.Join("\n", _events) +
        "\n\nReproduction steps: [add the actions that led to the issue]\n";
}
