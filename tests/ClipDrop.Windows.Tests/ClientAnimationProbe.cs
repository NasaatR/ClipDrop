using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

// Test host only. WPF caches this setting, so enable it before creating any WPF
// controls. CI session state is restored without writing the user profile.
internal static class ClientAnimationProbe
{
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool ReadParameter(uint action, uint parameter, out int value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool WriteParameter(uint action, uint parameter, IntPtr value, uint flags);
    private static bool Read()
    {
        if (!ReadParameter(0x1042, 0, out var value, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Read client animation setting");
        return value != 0;
    }
    public static int RunProgram(Func<int> action)
    {
        var original = Read(); var changed = false; var result = 1;
        Exception? failure = null;
        try
        {
            if (!original && Environment.GetEnvironmentVariable("CI") == "true")
            {
                if (!WriteParameter(0x1043, 0, new IntPtr(1), 2)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Enable temporary CI client animation");
                changed = true;
                if (!Read()) throw new Exception("Native CI client animation flag did not enable");
            }
            result = action();
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            if (changed)
            {
                Exception? cleanup = null;
                try
                {
                    if (!WriteParameter(0x1043, 0, IntPtr.Zero, 2)) cleanup = new Win32Exception(Marshal.GetLastWin32Error(), "Restore CI client animation");
                    else if (Read() != original) cleanup = new Exception("Native CI client animation preference did not restore");
                }
                catch (Exception ex) { cleanup = ex; }
                if (cleanup is not null) failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
                else Console.WriteLine("PASS The isolated CI session animation setting is restored without a profile write.");
            }
        }
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        return result;
    }
    public static void Run(Action action, Action<bool, string> check)
    {
        action();
        if (Environment.GetEnvironmentVariable("CI") == "true")
            check(Read(), "Enabled motion checks preserve the isolated CI session animation setting");
    }
}
