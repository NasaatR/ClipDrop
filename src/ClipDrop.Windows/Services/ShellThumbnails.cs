using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
namespace ClipDrop.Windows.Services;

// Two STA workers contain slow shell providers. A timed-out request never blocks
// the dispatcher or creates an unbounded replacement thread/provider process.
public static class ShellThumbnails
{
    private sealed record Request(string Path, TaskCompletionSource<BitmapSource?> Completion);
    private static readonly BlockingCollection<Request> Queue = new(32);
    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Width, Height; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageFactory { [PreserveSig] int GetImage(Size size, uint flags, out IntPtr bitmap); }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IImageFactory? factory);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    static ShellThumbnails()
    {
        for (var i = 0; i < 2; i++)
        {
            var thread = new Thread(Work) { IsBackground = true, Name = "ClipDrop thumbnail " + i };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
        }
    }
    public static async Task<BitmapSource?> Get(string path)
    {
        var result = new TaskCompletionSource<BitmapSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Queue.TryAdd(new Request(path, result))) return null;
        try { return await result.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException) { result.TrySetCanceled(); return null; }
    }
    private static void Work()
    {
        var initialized = CoInitializeEx(IntPtr.Zero, 2) >= 0;
        try
        {
            foreach (var request in Queue.GetConsumingEnumerable())
            {
                if (request.Completion.Task.IsCompleted) continue;
                IImageFactory? factory = null; var bitmap = IntPtr.Zero;
                try
                {
                    var iid = typeof(IImageFactory).GUID;
                    if (SHCreateItemFromParsingName(request.Path, IntPtr.Zero, ref iid, out factory) < 0 || factory is null) { request.Completion.TrySetResult(null); continue; }
                    // THUMBNAILONLY: a generic file icon is not presented as a video frame.
                    if (factory.GetImage(new Size { Width = 160, Height = 160 }, 0x8 | 0x1, out bitmap) < 0 || bitmap == IntPtr.Zero) { request.Completion.TrySetResult(null); continue; }
                    BitmapSource image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if ((long)image.PixelWidth * image.PixelHeight > 16000000) { request.Completion.TrySetResult(null); continue; }
                    var scale = Math.Min(1d, Math.Min(160d / image.PixelWidth, 160d / image.PixelHeight));
                    if (scale < 1) image = new TransformedBitmap(image, new ScaleTransform(scale, scale));
                    image = new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0);
                    image.Freeze(); request.Completion.TrySetResult(image);
                }
                catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException or System.IO.IOException)
                { request.Completion.TrySetResult(null); }
                finally { if (bitmap != IntPtr.Zero) DeleteObject(bitmap); if (factory is not null) Marshal.ReleaseComObject(factory); }
            }
        }
        finally { if (initialized) CoUninitialize(); }
    }
}
