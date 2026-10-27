using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using Forms = System.Windows.Forms;

internal static class PasteProbe
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, MouseInput[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public uint Type; public MouseUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct MouseUnion { [FieldOffset(0)] public Mouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }

    public static int RunTarget(string root)
    {
        using var form = new Forms.Form { Text = "ClipDrop paste probe", Width = 500, Height = 240, StartPosition = Forms.FormStartPosition.CenterScreen };
        var editor = new Forms.RichTextBox { Dock = Forms.DockStyle.Fill };
        editor.TextChanged += (_, _) => File.WriteAllText(Path.Combine(root, "received.txt"), editor.Text);
        form.Controls.Add(editor);
        form.Shown += (_, _) => { editor.Focus(); File.WriteAllText(Path.Combine(root, "ready.txt"), form.Handle.ToInt64().ToString()); };
        using var timeout = new Forms.Timer { Interval = 20000 };
        timeout.Tick += (_, _) => form.Close(); timeout.Start();
        Forms.Application.Run(form); return 0;
    }

    private static void Until(Func<bool> condition, string message, int milliseconds = 5000)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            try { if (condition()) return; } catch (IOException) { }
            if (watch.ElapsedMilliseconds > milliseconds) throw new Exception(message);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Thread.Sleep(20);
        }
    }
    private static T? Descendant<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T value) return value;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (Descendant<T>(VisualTreeHelper.GetChild(node, i)) is T found) return found;
        return null;
    }
    public static void Run(DockWindow dock, string root, string expected, Action<bool, string> check)
    {
        var targetRoot = Path.Combine(root, "native-target"); Directory.CreateDirectory(targetRoot);
        var info = new ProcessStartInfo(Environment.ProcessPath ?? throw new Exception("Test executable unavailable")) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(typeof(PasteProbe).Assembly.Location);
        info.ArgumentList.Add("--paste-target"); info.ArgumentList.Add(targetRoot);
        using var child = Process.Start(info) ?? throw new Exception("Could not start paste target");
        using var paste = new DesktopPaste();
        dock.Session.Paster = paste;
        try
        {
            var hwnd = IntPtr.Zero;
            Until(() => {
                if (!File.Exists(Path.Combine(targetRoot, "ready.txt")) || !long.TryParse(File.ReadAllText(Path.Combine(targetRoot, "ready.txt")), out var handle)) return false;
                hwnd = new IntPtr(handle); return true;
            }, "Native receiver did not start");
            Until(() => GetForegroundWindow() == hwnd, "Receiver did not get foreground focus");
            var target = paste.CaptureTarget();
            check(target?.Window == hwnd, "Foreground tracker identifies the external receiver");
            dock.ShowDock(); dock.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            check(GetForegroundWindow() == hwnd, "Showing the non-activating dock preserves receiver focus");
            var beforeDrag = dock.Left;
            UiProbe.Drag(dock, new Point(3, dock.ActualHeight / 2), 28, -20);
            check(Math.Abs(dock.Left - beforeDrag) > 15 && GetForegroundWindow() == hwnd, "Dragging dock padding moves it without stealing editor focus");
            var items = dock.Pins;
            var container = items.ItemContainerGenerator.ContainerFromIndex(0) ?? throw new Exception("Dock pin was not rendered");
            var button = Descendant<Button>(container) ?? throw new Exception("Dock pin button missing");
            var point = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
            SetCursorPos((int)point.X, (int)point.Y);
            var mouse = new[] {
                new MouseInput { Type = 0, Data = new MouseUnion { Mouse = new Mouse { Flags = 2 } } },
                new MouseInput { Type = 0, Data = new MouseUnion { Mouse = new Mouse { Flags = 4 } } }
            };
            check(SendInput(2, mouse, Marshal.SizeOf<MouseInput>()) == 2, "Real mouse click is delivered to the dock pin");
            var received = Path.Combine(targetRoot, "received.txt");
            Until(() => File.Exists(received) && File.ReadAllText(received).Replace("\r\n", "\n") == expected.Replace("\r\n", "\n"), "External receiver did not receive the dock paste");
            check(true, "Clicking a dock pin pastes Unicode into a separate Windows process");
            check(GetForegroundWindow() == hwnd, "Dock click leaves the receiving editor focused");
        }
        finally { dock.Session.Paster = null; if (!child.HasExited) { child.Kill(); child.WaitForExit(3000); } }
    }

    public static void PolicyChecks(Action<bool, string> check)
    {
        var target = new PasteTarget(new IntPtr(1), 10, 20);
        void Scenario(Fake platform, PasteOutcome expected, string name)
        {
            using var service = new DesktopPaste(platform);
            var result = service.Paste(target).GetAwaiter().GetResult();
            check(result == expected && platform.Sends == (expected == PasteOutcome.Requested || expected == PasteOutcome.InputBlocked ? 1 : 0), name);
        }
        Scenario(new Fake { Available = false }, PasteOutcome.NoTarget, "Closed destination never receives input");
        Scenario(new Fake { Foreground = new IntPtr(2) }, PasteOutcome.TargetChanged, "Changed external foreground cancels paste without sending input");
        Scenario(new Fake { Sent = 1 }, PasteOutcome.InputBlocked, "Partial input is reported and never retried");
        Scenario(new Fake(), PasteOutcome.Requested, "A stable target gets exactly one paste request");
    }
    private sealed class Fake : IPastePlatform
    {
        public bool Available = true;
        public uint Sent = 4;
        public int Sends;
        public bool Valid(PasteTarget target) => Available;
        public bool OwnWindow(IntPtr window) => false;
        public IntPtr Foreground { get; set; } = new IntPtr(1);
        public bool KeysHeld => false;
        public void Focus(PasteTarget target) { }
        public uint SendPaste(PasteTarget target) { Sends++; return Sent; }
        public void ReleaseInjectedKeys(uint sent) { }
    }
}
