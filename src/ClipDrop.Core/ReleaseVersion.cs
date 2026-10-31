using System;

namespace ClipDrop.Core;

// Product release labels, not a dependency/API compatibility contract.
public static class ReleaseVersion
{
    public static string Label(string native)
    {
        var version = Version.Parse(native.Split('+')[0]);
        return version.Major == 0 ? version.ToString(3) : $"{version.Major}.{version.Minor}";
    }
    public static string Next(string native)
    {
        var version = Version.Parse(native);
        if (version.Major < 1 || version.Major > 255 || version.Major == 255 && version.Minor == 9 || version.Minor > 9 || version.Build != 0) throw new ArgumentException("Use a personal release version x.y.0 with x ≥ 1 and y from 0 to 9.");
        return version.Minor == 9 ? $"{version.Major + 1}.0.0" : $"{version.Major}.{version.Minor + 1}.0";
    }
}
