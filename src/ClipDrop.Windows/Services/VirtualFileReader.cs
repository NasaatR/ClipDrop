using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using ClipDrop.Core;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace ClipDrop.Windows.Services;

public static class VirtualFileReader
{
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr handle);

    public static List<ShelfItem> Read(System.Windows.IDataObject data, string root)
    {
        var result = new List<ShelfItem>();
        if (!data.GetDataPresent("FileGroupDescriptorW", false) || data is not ComDataObject com) return result;
        var descriptor = data.GetData("FileGroupDescriptorW", false) as Stream;
        if (descriptor is null) return result;
        var bytes = ImageFiles.ReadStream(descriptor);
        if (bytes.Length < 4) return result;
        var count = BitConverter.ToUInt32(bytes, 0);
        const int descriptorSize = 592;
        if (count > 64 || bytes.Length < 4L + count * descriptorSize)
            throw new InvalidDataException("This virtual file group is too large or incomplete.");
        for (var i = 0; i < (int)count; i++)
        {
            var offset = 4 + i * descriptorSize;
            var attributes = BitConverter.ToUInt32(bytes, offset + 36);
            if ((attributes & 0x10) != 0) continue;
            var name = System.Text.Encoding.Unicode.GetString(bytes, offset + 72, 520).Split('\0')[0];
            var extension = Path.GetExtension(Path.GetFileName(name));
            if (extension.Length > 16 || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) extension = ".bin";
            var format = new FORMATETC { cfFormat = unchecked((short)DataFormats.GetDataFormat("FileContents").Id),
                dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = i, tymed = TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL };
            com.GetData(ref format, out var medium);
            byte[] content;
            try
            {
                if (medium.tymed == TYMED.TYMED_ISTREAM)
                {
                    var stream = (IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
                    try
                    {
                        using var output = new MemoryStream();
                        var buffer = new byte[81920];
                        var readPointer = Marshal.AllocCoTaskMem(4);
                        try
                        {
                            while (true)
                            {
                                stream.Read(buffer, buffer.Length, readPointer);
                                var read = Marshal.ReadInt32(readPointer);
                                if (read == 0) break;
                                if (read < 0 || read > buffer.Length || output.Length + read > ImageFiles.MaximumBytes)
                                    throw new InvalidDataException("Virtual file exceeds the 64 MB import limit.");
                                output.Write(buffer, 0, read);
                            }
                        }
                        finally { Marshal.FreeCoTaskMem(readPointer); }
                        content = output.ToArray();
                    }
                    finally { Marshal.ReleaseComObject(stream); }
                }
                else if (medium.tymed == TYMED.TYMED_HGLOBAL)
                {
                    var length = GlobalSize(medium.unionmember).ToUInt64();
                    if (length > ImageFiles.MaximumBytes) throw new InvalidDataException("Virtual file exceeds the 64 MB import limit.");
                    var pointer = GlobalLock(medium.unionmember);
                    if (pointer == IntPtr.Zero) throw new IOException("Could not read the virtual file.");
                    try { content = new byte[(int)length]; Marshal.Copy(pointer, content, 0, content.Length); }
                    finally { GlobalUnlock(medium.unionmember); }
                }
                else throw new InvalidDataException("Unsupported virtual file transfer format.");
            }
            finally { ReleaseStgMedium(ref medium); }
            var path = Path.Combine(root, Guid.NewGuid().ToString("N") + extension);
            File.WriteAllBytes(path, content);
            var item = ShelfItem.FromPath(path);
            item.Title = Path.GetFileName(name);
            item.OwnedAsset = true;
            result.Add(item);
        }
        return result;
    }
}
