using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;

// Test host only. Never changes a developer/user profile; CI session state is
// restored even on assertion failure, with primary/cleanup errors preserved.
internal static class ClientAnimationProbe
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, IntPtr value, uint flags);
    public static void Run(Action action, Action<bool, string> check)
    {
        var original = SystemParameters.ClientAreaAnimation;
        var changed = false;
        Exception? failure = null;
        try
        {
            if (!original && Environment.GetEnvironmentVariable("CI") == "true")
            {
                if (!SystemParametersInfo(0x1043, 0, new IntPtr(1), 2)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Enable temporary CI client animation");
                changed = true;
                if (!UiProbe.Until(() => SystemParameters.ClientAreaAnimation, 1500)) throw new Exception("CI client animation did not become available after WM_SETTINGCHANGE");
            }
            action();
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            if (changed)
            {
                Exception? cleanup = null;
                if (!SystemParametersInfo(0x1043, 0, IntPtr.Zero, 2)) cleanup = new Win32Exception(Marshal.GetLastWin32Error(), "Restore CI client animation");
                else if (!UiProbe.Until(() => SystemParameters.ClientAreaAnimation == original, 1500)) cleanup = new Exception("CI client animation preference did not restore");
                if (cleanup is not null) failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
            }
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        check(SystemParameters.ClientAreaAnimation == original, "The temporary CI animation preference is restored without a profile write");
    }
}
