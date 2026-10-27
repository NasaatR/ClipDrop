# Foundation and current boundary

Version 0.1.1. Source: NasaatR/ClipDrop, main. Authority: build a Windows shelf
foundation and push directly to the default branch.

## Decisions and sources

Original implementation, not a fork/rebrand. WPF gives direct Windows data
objects without an additional web/native UI bridge. Models/persistence are
isolated from WPF for future adapters. References inspected 2026-10-27:

- [Microsoft WPF data objects](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/data-and-data-objects): multiple offered formats, target choice.
- [Microsoft shell clipboard formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard): file paths, virtual file streams, ownership.
- [Microsoft PNG imaging](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-encode-and-decode-a-png-image).
- [StashIt, 25125f7](https://github.com/kobaltgit/StashIt/tree/25125f74e9543a174ab1df922308710c6f21d93b), MIT: dual text/file output; timed cleanup and ignored drag result deliberately not adopted.
- [Holdem, 35644fe](https://github.com/iamzubin/holdem/tree/35644fe0f1c4eeda4c9bd4deb02bb7b8b4b6b734), MIT: OLE/virtual input, primarily file-based output.
- [Edge-Drop, 1940aad](https://github.com/Deepender25/Edge-Drop/tree/1940aade0a9c0f90ac67fcb87c5d23194cd1bef1), Apache-2.0: shelf/history UX; its text output stages .txt files.
- [Tokri](https://github.com/ysuraj1999/tokri), MIT: Qt reference; copying ordinary inputs is avoided here.
- [DropPoint](https://github.com/GameGodS3/DropPoint), GPL-3.0: file shelf reference; no GPL source incorporated.
- [Dropshelf](https://github.com/williamckha/dropshelf-repo), issue-only: commercial UX reference; private code unavailable.

No shelf implementation/assets copied. Check licenses before future reuse.

## Architecture

Core owns versioned data, lossless text, path references, atomic saves, backup
and corruption/newer-schema boundaries. Windows owns STA transfers, bounded
virtual streams, PNG preference, lazy thumbnails, tray/hotkey. UI is compact,
resizable, dark/light, with typography/spacing/radius controls and English labels.

All cards persist. Clear removes unpinned cards. Undo is session-local.
Removal never deletes files. No telemetry, clipboard monitor or HTTP downloader.

## Verification

The CI result for the exact SHA is authoritative for compilation, core/Windows
checks, WPF captures, inspected packages and installer/uninstaller smoke checks.
Workflow configuration alone does not prove these ran. Captures are internal
artifacts, not README media.

Manual acceptance remains open:

| Journey | Expected result | Status |
| --- | --- | --- |
| Explorer → shelf → Explorer | Copy all selected files; keep originals | Pending manual test |
| Browser image → shelf → Photoshop | Image/alpha if provided by source | Pending manual test |
| Browser image → shelf → Figma desktop/web | Supported image imported | Pending manual test |
| Text → shelf → editor | Unicode/whitespace intact | Pending manual test |
| Escape / unsupported target | Cards and originals remain | Pending manual test |
| Virtual attachment → shelf | Stream becomes a managed file | Pending manual test |
| Mixed DPI / monitors / sleep | Shelf remains reachable | Pending manual test |

## Deferred

Automatic edge docking; target-app compatibility from real reports; HTML/RTF;
configurable shortcut; explicit safe cache reclamation; more appearance overrides;
ARM64; signing and authenticated recoverable updates. macOS needs a new native
UI/adapter: WPF itself is not portable.
