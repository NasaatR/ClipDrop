using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;

namespace ClipDrop.Core;

public static partial class BackupArchive
{
    // Bound untrusted directory metadata before ZipArchive materializes entry/name
    // objects. Read the same locked handle used by Verify; no extraction or TOCTOU.
    // ZIP/ZIP64 record layout: https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT
    private static void PreflightDirectory(FileStream file, CancellationToken cancellation)
    {
        static InvalidDataException Invalid() => new("Backup ZIP directory is invalid or exceeds its inspection limits.");
        static ushort U16(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(b[offset..]);
        static uint U32(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
        static ulong U64(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(b[offset..]);
        if (file.Length < 22) throw Invalid();
        var tail = new byte[(int)Math.Min(file.Length, 65557)];
        file.Position = file.Length - tail.Length; file.ReadExactly(tail);
        var end = -1;
        for (var i = tail.Length - 22; i >= 0; i--)
        {
            if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            if (U32(tail, i) != 0x06054b50) continue;
            // Match the runtime's nearest-signature selection. Do not skip an
            // invalid later footer and validate a different earlier directory.
            if (i + 22 + U16(tail, i + 20) != tail.Length) throw Invalid();
            end = i; break;
        }
        if (end < 0 || U16(tail, end + 4) != 0 || U16(tail, end + 6) != 0) throw Invalid();
        ulong count = U16(tail, end + 10), size = U32(tail, end + 12), offset = U32(tail, end + 16);
        if (U16(tail, end + 8) != count) throw Invalid();
        var footer = file.Length - tail.Length + end;
        Span<byte> locator = stackalloc byte[20];
        if (footer >= 20) { file.Position = footer - 20; file.ReadExactly(locator); }
        if (footer >= 20 && U32(locator, 0) == 0x07064b50)
        {
            var zip64 = U64(locator, 8);
            if (footer < 76 || U32(locator, 4) != 0 || U32(locator, 16) != 1 || zip64 > (ulong)(footer - 20 - 56)) throw Invalid();
            Span<byte> record = stackalloc byte[56]; file.Position = (long)zip64; file.ReadExactly(record);
            var recordSize = U64(record, 4);
            if (U32(record, 0) != 0x06064b50 || recordSize < 44 || recordSize != (ulong)(footer - 20) - zip64 - 12 ||
                U32(record, 16) != 0 || U32(record, 20) != 0 || U64(record, 24) != U64(record, 32)) throw Invalid();
            var fullCount = U64(record, 32); var fullSize = U64(record, 40); var fullOffset = U64(record, 48);
            if (count != ushort.MaxValue && count != fullCount || size != uint.MaxValue && size != fullSize || offset != uint.MaxValue && offset != fullOffset) throw Invalid();
            count = fullCount; size = fullSize; offset = fullOffset; footer = (long)zip64;
        }
        if (count is < 2 or > MaximumEntries + 1 || size > 64UL * 1024 * 1024 || offset > (ulong)footer || size != (ulong)footer - offset) throw Invalid();
        file.Position = (long)offset; var stop = footer; var actual = 0;
        Span<byte> header = stackalloc byte[46];
        while (file.Position < stop)
        {
            cancellation.ThrowIfCancellationRequested();
            if (++actual > MaximumEntries + 1 || stop - file.Position < header.Length) throw Invalid();
            file.ReadExactly(header);
            if (U32(header, 0) != 0x02014b50 || U16(header, 34) != 0) throw Invalid();
            var variable = U16(header, 28) + U16(header, 30) + U16(header, 32);
            if (U16(header, 28) is 0 or > 32768 || variable > stop - file.Position) throw Invalid();
            file.Position += variable;
        }
        if ((ulong)actual != count) throw Invalid();
        cancellation.ThrowIfCancellationRequested();
    }
}
