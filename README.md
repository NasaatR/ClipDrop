# ClipDrop

A local floating Windows shelf for files, images, text and links. Collect content,
switch applications, then drag it out. Made by Nasaat.

[Инструкция на русском](README.ru.md)

## Download and start

[Windows preview downloads](https://github.com/NasaatR/ClipDrop/releases)

- `ClipDrop_<version>_windows-x64_setup.exe`: recommended per-user installer,
  optional desktop shortcut and start at sign-in; no administrator rights needed.
- `ClipDrop_<version>_windows-x64_portable.zip`: extract the entire versioned
  folder and run `ClipDrop.exe`. Keep the other runtime files alongside it.

Both include .NET. No developer tools or extra runtime installation required.
These are unsigned previews; Windows may show a publisher/reputation prompt.
Only download from this repository.

The compact dock opens first. Click **Save** to capture the clipboard, or drop
content onto it. New captures become pins. Click a pin to request paste into your
previous app, or drag it out. **Library** opens the full searchable collection;
Ctrl/Shift selects several items, and **Paste** / Enter uses the selection.
Transfers copy; originals and cards remain. **Clear** removes only unpinned cards. **Ctrl+Z** restores removed cards
from Trash, including after restarting.

## Features in 0.6.0

- Compact non-activating dock plus resizable Library, shared state and tray access.
- Click-paste into the validated previous app; safe copy-only fallback when focus/input is unavailable.
- Drag either surface by any empty background; buttons, cards, editors and resizing keep their gestures.
- Library opens next to the dock and follows it. Drag Library to detach; its link button reattaches.
- Four appearance presets, named saved styles, independent dock/Library palettes,
  custom RGB backgrounds/accent and live preview. Existing visual values are kept on update.
- Dock button size, spacing, corner radius and background-only opacity controls.
- Fast button feedback, new-pin entrance and panel reveal; respects Windows animation settings and an app toggle.
- File/folder references, Unicode text, links, PNG/bitmap and virtual-file inputs.
- Text and links stay text/URLs: copying or dragging never manufactures TXT/URL files.
- Save clipboard retains available native memory formats (Unicode, HTML, RTF,
  PNG/DIB and opaque registered formats); Copy/click-paste restores their bytes.
  Process/owner-bound formats cannot always be persisted; partial captures are reported.
- Original files remain file references, including TXT files; file-copy intent is copy-only.
- Multi-selection, search, pinning, preview, editable names/groups and persistent Trash with restore/undo after restart.
- Emoji icons with actual Windows font colors, optional managed PNG replacements per role,
  shared live dock preview and grouped Settings. Set them in Settings → Icons.
- New pins append at the right; duplicates stay put. Existing button instances are reused,
  with a virtualized horizontal row and wheel scrolling for longer shelves.
- Linked monitor docks (preview): Settings → Screens enables selected displays,
  shared clips, separate positions/visibility, display map, Show all / Hide all,
  reconnect fallback and singleton Library/Settings.
- Per-clip context menus in dock/Library, preserving the clicked target and multi-selection.
- Video thumbnails from Windows, plus an explicit muted player with pause/seek and an open-in-app fallback.
- Custom application/installer/tray icon.
- Dark/light themes, text size, row spacing from 0, corner radius.
- Coalesced autosave, atomic replacement, previous-save backup and corruption
  recovery without silently resetting unreadable/newer data.
- Experimental mouse-shake summon, off by default.
- Copyable diagnostics: version/environment and bounded sanitized error history,
  without recording item contents.
- Local operation; no accounts, subscription, telemetry, clipboard monitoring,
  app-initiated network requests or cloud storage.

## Shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+Alt+Space | Show/hide dock on the external app’s display, pointer fallback |
| Ctrl+Alt+L | Open Library |
| Enter | Paste Library selection into previous app |
| Ctrl+V / Ctrl+C | Paste into shelf / copy selection, outside text fields |
| Ctrl+O / Ctrl+F | Add files / search |
| Ctrl+A | Select visible cards |
| Space / double-click | Preview / open |
| Delete / Ctrl+Z | Remove cards / undo removal |
| Escape | Clear search, then hide |

Double-click the tray icon to show the target dock. Its menu opens Library/Settings and provides **Show all docks**, **Hide all docks** and **Quit ClipDrop**.
Shortcut conflicts are reported; tray access still works.

## Compatibility and limits

Target: Windows x64, primarily Windows 11. The installer accepts Windows 10
1809+, but Windows 10 is not manually verified; supported editions also depend
on Microsoft's .NET support policy. No macOS/Linux build.

This is a functional foundation preview. The destination must accept a provided
format. Browsers that supply only an image URL add a link: no URL downloading or
reuse of browser cookies. Bitmap-only sources may already have lost alpha.
Internal Photoshop layers / Figma nodes are not kept as editable objects.
Elevated apps, secure desktops and exclusive fullscreen have Windows restrictions.
Automatic edge docking, named/closed panel sets, clipboard history, content-version editing and backup import/export UI are not included. Shake can also trigger
during left-button text selection or window movement.

PNG/bitmap/virtual imports and inline image output: 64 MB. Decoded images:
64 megapixels. Up to 64 virtual files/drop and 5,000 cards. Ordinary large files
transfer by path without full copies. Mixed selections offer the original file paths and text representations; the
target chooses what it accepts. Copy a single saved clipboard item to retain
all its source formats. Native snapshots are bounded to 128 formats / 64 MB.

Data lives at `%LOCALAPPDATA%\NasaatR\ClipDrop`. `shelf.json` is current metadata;
`shelf.json.bak` is the previous save. Assets/exports remain on disk so delayed
readers and clipboard payloads stay valid. No automatic cache reclamation yet.
Original references break if files move. Uninstall preserves this data folder.
To delete ClipDrop data, quit and remove that folder; external originals remain.
Back it up before manual modifications.

The installer upgrades the existing per-user installation using its permanent AppId and previous directory; no uninstall is needed. Quit from the tray before updating. Portable ZIPs are extracted separately. No automatic
updater or signing service is configured.

## Build and verification

On Windows with .NET 10 SDK:

```powershell
dotnet run --project tests/ClipDrop.Core.Tests -c Release
dotnet run --project tests/ClipDrop.Windows.Tests -c Release -- artifacts/qa
dotnet run --project src/ClipDrop.Windows
```

`scripts/package.ps1` also requires Inno Setup 6. Actions runs core/recovery and
Windows/native clipboard checks, renders dark/light/narrow/open-settings WPF
views, builds self-contained packages, inspects the ZIP and smoke-tests silent
installation/uninstallation before creating a new preview release. Check the
[actual workflow result](https://github.com/NasaatR/ClipDrop/actions).
Native tests verify native format bytes through restart, text without file output, persistent Trash, group editing, the live preview, and drag dock backgrounds without activation, exercise attached/detached Library placement and style save/restore, and deliver a physical dock click into a separate Windows editor process.
They do not automate Photoshop/Figma/browser applications. The dock reports
"Paste requested" because input submission cannot prove that every app consumed
the content. It leaves the clipboard ready for manual Ctrl+V and never restores it
on a short timer. Ctrl+Alt+L provides a keyboard-accessible Library alternative.

Metadata schema 3 migrates schema-1/2 libraries and keeps an immutable `shelf.json.schema-<old>-before-v3.bak` before the first write. Old releases refuse newer metadata instead of resetting it.

Core owns models/persistence; Windows owns WPF and native adapters. Add transfer
formats in TransferService, not card UI handlers. Do not delete originals, clear
cards on drag completion, or time-delete exported payloads.
Research and manual acceptance: [foundation notes](docs/FOUNDATION.md).
Incremental stages and the two-monitor mirror design: [roadmap](docs/ROADMAP.md).
Monitor mirroring is a preview. Native window checks and simulated topologies pass
through CI; physical mixed-DPI dual-monitor, sleep/reconnect testing remains open.
Drag a dock within its assigned display; choose a different display in Settings.
Named and recently closed panel sets remain planned. PNG icons: ≤5 MB, ≤2048 × 2048;
managed copies render at bounded size. Missing assets fall back to emoji.
Video frames/playback depend on installed Windows providers/codecs. No FFmpeg or codec installer is bundled.

MIT · Maintained at <https://github.com/NasaatR/ClipDrop>.
