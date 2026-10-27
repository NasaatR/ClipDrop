using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Interop;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

// Preserve opaque HGLOBAL clipboard bytes without deserializing source-app objects.
// Handle/process-bound formats are not persisted. Images use portable DIB/PNG alternatives.
public sealed class NativeClipboard : IDisposable
{
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern uint EnumClipboardFormats(uint format);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClipboardFormatName(uint format, StringBuilder name, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr handle);
    private readonly AssetStore _assets;
    private readonly HwndSource _owner = new(new HwndSourceParameters("ClipDrop clipboard") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
    private const long Limit = 64L * 1024 * 1024;
    private static readonly HashSet<string> Infrastructure = new(StringComparer.OrdinalIgnoreCase) {
        "Ole Private Data", "DataObject", "Preferred DropEffect", "Performed DropEffect", "Logical Performed DropEffect", "Paste Succeeded", "InShellDragLoop", "DragWindow", "FileGroupDescriptorW", "FileGroupDescriptor", "FileContents"
    };
    public NativeClipboard(AssetStore assets) { _assets = assets; }
    public ClipboardSnapshot? Capture()
    {
        var snapshot = new ClipboardSnapshot();
        var captured = new List<(ClipboardFormatBlob Format, byte[] Bytes)>();
        if (!OpenClipboard(_owner.Handle)) throw new ExternalException("Clipboard is busy.");
        try
        {
            long total = 0; uint format = 0;
            for (var count = 0; count < 128 && (format = EnumClipboardFormats(format)) != 0; count++)
            {
                var registered = format >= 0xC000;
                var name = "";
                if (registered) { var buffer = new StringBuilder(256); GetClipboardFormatName(format, buffer, buffer.Capacity); name = buffer.ToString(); }
                if (Infrastructure.Contains(name) || name.StartsWith("NasaatR.ClipDrop.", StringComparison.Ordinal)) continue;
                // Only documented memory formats and registered HGLOBAL payloads; never persist process handles.
                if (!registered && format is not (1 or 4 or 5 or 6 or 7 or 8 or 13 or 15 or 16 or 17 or 129))
                { if (format is not (2 or 9)) snapshot.Partial = true; continue; }
                if (registered && name.Length == 0) { snapshot.Partial = true; continue; }
                var handle = GetClipboardData(format);
                var length = handle == IntPtr.Zero ? 0 : GlobalSize(handle).ToUInt64();
                if (length == 0 || length > (ulong)Limit || total + (long)length > Limit) { snapshot.Partial = true; continue; }
                var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero) { snapshot.Partial = true; continue; }
                var bytes = new byte[(int)length];
                try { Marshal.Copy(pointer, bytes, 0, bytes.Length); } finally { GlobalUnlock(handle); }
                captured.Add((new ClipboardFormatBlob { StandardId = registered ? 0 : format, Name = name, Length = bytes.Length }, bytes));
                total += bytes.Length;
            }
            if (format != 0) snapshot.Partial = true;
        }
        finally { CloseClipboard(); }
        foreach (var part in captured) { part.Format.Hash = _assets.WriteBlob(part.Bytes); snapshot.Formats.Add(part.Format); }
        if (snapshot.Formats.Count == 0) return null;
        snapshot.Identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", snapshot.Formats.Select(f => $"{f.StandardId}:{f.Name}:{f.Hash}")))));
        return snapshot;
    }
    public void Restore(ClipboardSnapshot snapshot)
    {
        if (snapshot.Formats.Count is < 1 or > 128 || snapshot.Formats.Sum(f => (long)f.Length) > Limit) throw new InvalidDataException("Invalid clipboard snapshot.");
        var prepared = new List<(uint Format, IntPtr Memory)>();
        try
        {
            // Fully read and allocate before emptying the live clipboard.
            foreach (var blob in snapshot.Formats)
            {
                var bytes = _assets.ReadBlob(blob);
                var format = blob.StandardId != 0 ? blob.StandardId : RegisterClipboardFormat(blob.Name);
                if (format == 0 || (blob.StandardId != 0 && format is not (1 or 4 or 5 or 6 or 7 or 8 or 13 or 15 or 16 or 17 or 129)) || Infrastructure.Contains(blob.Name)) throw new InvalidDataException("Invalid clipboard format.");
                var memory = GlobalAlloc(2, (UIntPtr)bytes.Length);
                if (memory == IntPtr.Zero) throw new OutOfMemoryException();
                prepared.Add((format, memory));
                var pointer = GlobalLock(memory);
                if (pointer == IntPtr.Zero) throw new ExternalException("Clipboard allocation failed.");
                try { Marshal.Copy(bytes, 0, pointer, bytes.Length); } finally { GlobalUnlock(memory); }
            }
            if (!OpenClipboard(_owner.Handle)) throw new ExternalException("Clipboard is busy.");
            try
            {
                if (!EmptyClipboard()) throw new ExternalException("Could not open clipboard for copy.");
                for (var i = 0; i < prepared.Count; i++)
                {
                    var part = prepared[i];
                    if (SetClipboardData(part.Format, part.Memory) == IntPtr.Zero) throw new ExternalException("A clipboard format could not be restored.");
                    prepared[i] = (part.Format, IntPtr.Zero); // Windows owns accepted allocations.
                }
            }
            finally { CloseClipboard(); }
        }
        finally { foreach (var part in prepared) if (part.Memory != IntPtr.Zero) GlobalFree(part.Memory); }
    }
    public void Dispose() => _owner.Dispose();
}
