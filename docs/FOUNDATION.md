# Foundation and current boundary

Version 0.4.0. Source: NasaatR/ClipDrop, main. Authority: build a Windows shelf
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
| Text → dock → editor | Unicode/whitespace intact; target keeps focus | Automated separate-process Windows receiver passed; user apps pending |
| Escape / unsupported target | Cards and originals remain | Pending manual test |
| Virtual attachment → shelf | Stream becomes a managed file | Pending manual test |
| Mixed DPI / monitors / sleep | Shelf remains reachable | Pending manual test |

## Deferred

Automatic edge docking; target-app compatibility from real reports; HTML/RTF;
configurable shortcut; explicit safe cache reclamation; more appearance overrides;
ARM64; signing and authenticated recoverable updates. macOS needs a new native
UI/adapter: WPF itself is not portable.

## 0.2.0: accepted dock stage

The first iteration of the user's WeavyClips-inspired plan is a compact dock,
clipboard save, pinned click/drag use and a separate existing Library. All views
share ShelfSession; it serializes changes on the WPF dispatcher and owns one
StateStore. Existing schema-1 libraries remain compatible; new dock preferences
have defaults and preserve explicit 0 values.

UX decisions, inspected 2026-10-27:

- [WeavyClips 2.5.1](https://github.com/NasaatR/WeavyClips/tree/d368314021db771e0014d4617505ab640154963a): compact pinned dock, separate Library, shared appearance model, focus-preserving actions, restrained settings. No JavaScript/CSS copied into WPF.
- [Dropover FAQ](https://dropoverapp.com/faq): pinned shelves and user-controlled copy behavior. Keep saved cards/originals after transfer.
- [Yoink](https://eternalstorms.at/yoink/mac/): nearby capture, clipboard shortcut, clear preview and unobtrusive shelf. Make save/library one-click actions and bound the visible pin row.
- [Dropshelf](https://github.com/williamckha/dropshelf-repo): floating holding area and saved collections; source is issue-only.
- [Windows non-activating toolbar](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles): WS_EX_NOACTIVATE plus WM_MOUSEACTIVATE preserve the destination while clicking the dock. Keyboard alternatives live in Library.
- [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) and [foreground restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow): revalidate target window/PID/thread, do not send to a changed app, wait briefly for held modifiers to release, report copy-only fallback, do not retry a document paste automatically.

Dock: Save, pinned previews/short labels, overflow to Library, Library, Settings,
Hide. New captures pin automatically; existing unpinned cards stay in Library.
Click requests native Ctrl+V into the previous external window. Drag uses native
copy-only payloads and suppresses the following click. Clipboard is left intact
for delayed readers/manual fallback, not restored after an arbitrary timer.
A foreground-window event hook records HWND/PID/thread only; it does not monitor
clipboard contents or keys. Modifier state is checked only for a requested paste.

Core checks cover data/recovery and explicit-zero appearance preferences. Windows
checks include a real mouse click on a dock pin, delivery into a separate receiver
process, unchanged receiver focus, fallback policy, real clipboard formats and UI
renders. CI also starts/quits the published executable before installer checks.
This does not establish Photoshop/Figma/browser-specific behavior; retain the
manual matrix above.

Verified build: [Windows run 40432637954](https://github.com/NasaatR/ClipDrop/actions/runs/40432637954),
commit `b34ffa38986bf5695fed9e65f4a9b00987296571`: 18 core checks and 24 Windows
checks passed, including a real dock click, external paste reception and focus
preservation. Published startup/shutdown and installation/uninstallation passed.
Dark/light dock, Library, minimum-width Library and Settings captures were
visually inspected. Issued [0.2.0 assets](https://github.com/NasaatR/ClipDrop/releases/tag/v0.2.0)
remain tied to that commit; later documentation changes do not replace them.

Remaining user-feedback stages:

1. Appearance stage shipped in 0.3.1: presets, independent palettes, RGB overrides, live preview and bounded motion.
2. Expanded reusable Library, naming/groups/content versions, persistent trash and backup UI.
3. Named panel sets, independent windows, persistent recently closed panels and restore.
4. Opt-in clipboard history, pause, protected-format exclusions, deduplication and retention.

Persistent trash, closed-panel history and automatic clipboard capture are not
implemented by 0.2.0. Current Undo remains session-local; communicate that clearly.


## 0.3.0: appearance and movement

User feedback: installed 0.2.0 works and transfers into/out of dock work. Main
problems were tiny movement targets, a detached-looking Library, Save text and
missing motion. Dock and Library now move from any non-interactive background;
card OLE gestures, input fields, buttons, sliders and resize grips are excluded.
Captured pointer movement does not activate the dock. Library follows its dock
by default, stays inside its monitor working area, detaches on manual movement
and reattaches via a visible link button. Detached geometry is retained.

AppearanceStyle is a visual-only snapshot: applying or resetting it never changes
startup, topmost, motion preference, attachment, original files or window positions.
Four built-ins and up to 20 named styles use the same values as the live UI.
Legacy schema-1 flat settings load unchanged, including explicit zero values;
new fields receive defaults. Invalid color text is kept in the editor without
replacing the saved value. Empty color restores the selected palette.

Dock and Library scope their resources independently. Surface foregrounds derive
from luminance; custom accent buttons have a derived foreground. Settings stays
on the app theme. No shell-wide resource mutation or bitmap icon font is used.

Motion budget: 110 ms button response, 160 ms panel reveal, 180 ms new-pin
arrival, with no layout-width animation or continuous loop. All entrance content
has a visible final baseline, interrupted button feedback replaces its clock,
and app/Windows motion preferences disable the spatial effects. See
[Microsoft ClientAreaAnimation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.clientareaanimation)
and [WPF FillBehavior](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.animation.timeline.fillbehavior).
This is native WPF adaptation of the restrained WeavyClips feedback, not copied
browser CSS or a new rendering engine.

The visual review found a Settings root background mismatch during live theme
changes. 0.3.1 binds the window and content background explicitly and adds a
regression assertion plus an open-preset-menu capture.


## 0.4.0: requested WeavyClips direction, native copy and recovery

The user explicitly prefers the supplied WeavyClips dock language over the
original monochrome icon family: colored floppy Save, gear Settings, three-line
Library, then clip emoji. Source inspected at d368314. Native original drawings
adapt those roles; DockStrip is shared by the real dock and the live Settings
preview. Settings uses compact grouped surfaces and section navigation. The
original ClipDrop icon is committed with scalable source and a reproducible
multi-size ICO renderer; no downloaded icon pack is bundled.

Transfer behavior supersedes the original foundation: text and links never
manufacture file payloads. File references stay files; clipboard-origin images
remain image formats. Save captures bounded HGLOBAL clipboard formats and stores
opaque bytes by SHA-256 in managed assets. Standard handle/owner-dependent formats
and OLE process metadata are not blindly replayed. File captures keep copy-only
intent rather than cut/delete negotiation. Snapshot restore reads and validates
all assets and allocates buffers before emptying the live clipboard. Windows
owns allocations accepted by SetClipboardData; rejected buffers are freed. No
untrusted .NET object deserialization, network request or clipboard monitoring.

[Microsoft clipboard operations](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations)
and [shell formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard)
define memory ownership, synthesized formats and move/delete negotiation.
Byte preservation does not establish portability for every app-private format.
Unavailable formats/limits produce a partial-capture message. Source captures
use a clipboard sequence guard; dropped content uses supported OLE typed formats.

Schema 2 adds persistent trash and group/name metadata. Schema 1 loads with
safe defaults and retains an untouched backup on first save. Newer schema
refusal remains explicit. Trash has no timer purge or original-file deletion.
Restore is checked against active-library capacity; failed restore keeps the
trash entry. Name/group edits affect metadata only, without changing original
files, text bytes or captured clipboard assets.

Remaining stages: content editing/version history and backup UI; named panel
sets and recently closed restoration; opt-in automatic clipboard history.
