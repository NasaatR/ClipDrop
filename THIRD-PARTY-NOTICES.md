# Third-party notices

ClipDrop's application code is an original MIT implementation. Public projects
and documentation used as references are listed in [docs/FOUNDATION.md](docs/FOUNDATION.md).
No source implementation or asset from those shelf applications is copied or bundled.

Self-contained packages include Microsoft .NET / Windows Desktop runtime
components. Packaging includes `DOTNET-LICENSE.txt` and
`DOTNET-THIRD-PARTY-NOTICES.txt`. See <https://github.com/dotnet/runtime>.

The installer is generated using Inno Setup, a separate build tool. The installer
engine's license: <https://github.com/jrsoftware/issrc/blob/main/license.txt>.
Windows system fonts/icons come from the OS, not redistributed asset files.

Raster decoding includes SkiaSharp 4.154.0 (MIT), Microsoft/Xamarin, and its
Win32 native Skia dependencies. Distributions include the exact package license
as `SKIASHARP-LICENSE.txt` and native dependencies' notices as
`SKIA-THIRD-PARTY-NOTICES.txt`. Source/version:
<https://www.nuget.org/packages/SkiaSharp/4.154.0> and <https://github.com/mono/SkiaSharp>.
