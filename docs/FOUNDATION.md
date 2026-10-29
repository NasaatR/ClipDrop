# Foundation and current boundary

Version 0.11.0. Source: NasaatR/ClipDrop, main. Authority: build a Windows shelf
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

All cards persist. Clear moves unpinned cards to persistent Trash. Undo/Restore survives restart.
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

Automatic edge docking; target-app compatibility from real reports;
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

Historical stage list from 0.2.0 (superseded by [ROADMAP.md](ROADMAP.md)):

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


## 0.5.0: actions and media, with bounded next stages

The user's latest feedback replaces the retro colored/emoji icon direction with
one modern vector family. Preserve the WeavyClips action model and shared dock
preview, with context actions bound to the right-clicked clip and stable captured
multi-selection. Video remains an ordinary original file for clipboard and drag;
media classification changes its presentation, not its saved data type.

Native thumbnail extraction uses IShellItemImageFactory on two dedicated STA
workers, a bounded queue and a three-second caller timeout. HBITMAP ownership
ends with DeleteObject; COM references are released on their owning worker.
Frozen results are shared across dock/Library/preview, invalidated by file length
and write time. Missing/unsupported frames have vector fallbacks, never blank pins.
WPF MediaElement playback is explicit, muted and manually controlled; an eight-
second open timeout exposes Open in app. No external decoder is bundled.

Sources: [Microsoft shell image factory](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemimagefactory-getimage),
[WPF multimedia](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/multimedia-overview).
Provider/codec availability is machine-dependent; test logs distinguish native
success from the fallback path.

Current and subsequent scope is in [ROADMAP.md](ROADMAP.md). Two-monitor mirroring
is designed there, with common state, independent placements, reconnect policy,
DPI acceptance and schema migration. It is not implemented or claimed by 0.5.0.


## 0.6.0: emoji/PNG, stable pins and mirrored dock preview

This supersedes the historical 0.5 icon choice: all app action and file-fallback
icons use emoji, or a per-role PNG chosen in Settings. Installed Windows emoji
COLR/CPAL layers render as frozen drawings; no system font or external icon art
is redistributed. The bounded reader only handles this fixed single-codepoint
role list, not general emoji shaping. WPF text is the compatibility fallback.
PNG imports are decoded/validated, bounded, re-encoded and stored by content hash.

PinOrder preserves old displayed order once, then new saves/re-pins append right.
A virtualized strip reuses buttons and guards thumbnail context recycling.
Appearance resources skip unchanged tokens. DockCoordinator owns monitor views,
while the process retains one store/session, foreground tracker, clipboard,
tray/hotkey source, Library and Settings. Closed views ignore the remainder of
an already captured Changed delegate invocation list.

Display identities use EnumDisplayDevices monitor interface names (GDI name
fallback for virtual drivers). Native physical window placement plus local DIP
offsets avoids globally scaling desktop origins; WPF handles target HWND DPI.
Display/resume events debounce; missing records remain and a temporary fallback
keeps a dock reachable. Physical mixed-DPI acceptance remains open: this is a
monitor preview. See ROADMAP for the next stabilization and saved-panel slices.

Schema 3 migrates 1/2 and preserves an immutable pre-migration metadata copy
before saving, in addition to the rotating .bak. Old schema-2 builds refuse it.
Sources: Microsoft EnumDisplayDevicesW/GetDpiForWindow and OpenType COLR/CPAL
specifications. Emoji.Wpf was inspected as an alternative; its general Unicode
shaping/font parser/shader dependencies were not adopted for 22 fixed roles.


## 0.7.0: recoverable one-use workflow

The user’s 2026-10-28 request supersedes historical keep-after-drag behavior.
Use cleanup follows a dispatched paste / target-acknowledged OLE Copy, and only
moves unprotected references to persistent Trash. Copies, failed/cancelled actions
and internal reorder keep clips. `KeepAfterUse` is separate from `IsPinned` dock
membership. Library/group menus and the hover-visible/persistent-on corner pin
operate on the same flag. Original files and managed clipboard/image blobs remain.

Selection is transient and per dock view, with plain-click select mode and
Ctrl/Shift range/toggle alternatives. A group is removed as one undoable batch.
Internal OLE uses one session-owned active token and an acknowledgement flag;
returning Copy for reorder does not invoke use cleanup. Drop position is a slot
in the original visible order; removal of selected predecessors is accounted for.
Group order, per-clip flags and new captures’ append-right behavior persist.

Schema 4 preserves protection against older builds ignoring and overwriting it.
Migrate 1/2/3 with an immutable before-v4 copy; only 1/2 receive the old order
migration. Primary API basis: Microsoft WPF DoDragDrop / Drop documentation.
An OLE effect acknowledges a target operation; SendInput acknowledges submitted
input. Neither proves that another application saved a document. Recoverable
Trash and unchanged originals are the recovery route.


## 0.8.0: low-distraction presence and reliable dismissal

IdleDockOpacity is a visual preference (default 20), separate from the existing
background-only opacity. The whole native WPF window fades while away from the
pointer. Busy interactions and a brief explicit-show pulse hold it visible.
Transition timers stop after settling; motion-off applies values immediately.
Settings preview uses the same DockStrip at the selected idle opacity.

A non-activating window can leave the previous external HWND foreground, so an
outside click may not generate a foreground change and Escape may still route
to that external thread. PopupDismiss observes pointer-down/foreground and Escape
only for an open owned menu. Outside input passes through; Escape closes the menu.
It records no text/clipboard content and detaches all native observers on close,
hide or movement. More expands inline, retaining the original clip/group snapshot.

NotifyIcon's native MouseClick route replaces double-click-only activation.
WinForms suppresses the second MouseClick in its double-click sequence. The tray
controls all enabled docks, while the existing shortcut keeps target-screen scope.
Minimized is distinct from hidden; Show normalizes WindowState before revealing.
Background Undo uses the existing persistent deletion batch, not a new history.
Data schema stays 4; this iteration adds no new content/history model.

Primary sources: Microsoft LowLevelMouseProc/SetWindowsHookEx, ContextMenu.StaysOpen,
MenuItem.StaysOpenOnClick, and dotnet/winforms NotifyIcon implementation. Physical
mixed-DPI monitor acceptance and real target-app gestures retain earlier limits.


## 0.9.0: responsiveness and recoverable named panels

Library reconciles cached ItemRows: unchanged refreshes emit no collection edits,
small edits preserve existing containers, large filter changes emit one Reset.
Selection/drag cues notify only their properties. Row bindings perform no disk
stats; realized rows request bounded background availability checks. Search uses
a short final-query debounce. Dock resources are reused and the blurred effect
is removed from the layered/translucent surface. Fallback emoji uses a Viewbox
around its full measured line box; images keep an inset and hover never enlarges
the cell. Existing saved size/spacing choices remain respected.

StateStore encodes an immutable snapshot while state has its UI owner, then
serializes atomic disk writes on TaskScheduler.Default. ShelfSession tracks edit
and persisted revisions: older completion cannot mark newer edits saved. Explicit
save/quit drains the queue. No live WPF/COM clipboard objects cross threads.

Schema 5 PanelSets own ordered unique IDs, not payload copies. IsPinned/PinOrder
project the one active set for existing views and transfers. Change/save captures
that projection; switching applies the chosen membership. Trash IDs retain their
slots for restoration. Closing a set retains Library clips; closing the last open
set creates empty Main. Recently closed sets persist. Combine creates a new stable
union and retains sources. Maximum is 100 including closed, with no silent purge.
Clear protects clips in all saved sets. Schema 1–4 migrate into Main with exact
original bytes backed up before-v5; newer/malformed structures never reset.

Saved panels has create/snapshot/selection/name/open/combine/close/restore in one
manager. Mirrored displays still share the active set. Independent simultaneous
set windows and history are later stages; physical mixed-DPI acceptance stays open.


## 0.10.0: a content-first dock and direct Library collection

The user explicitly replaces the permanent utility toolbar with Save plus clips.
Movement stays on free background; a compact right-click menu and free-background
double-click provide navigation without a grip/eye/gear/Library/panels toolbar.
Selected-count and Done are contextual text controls. A larger content viewport
holds up to 12 clips and keeps horizontal virtualization.

Library intake explicitly passes pin=false; dock/tray capture keeps pin=true.
Duplicates retain existing membership. Newly added IDs are selected as one action-
bar update, without saving selection state or auto-pinning. Checkboxes use native
ListBoxItem selection; Ctrl/Shift, row-group drag and keyboard actions remain.
The counted Drag N source shares the same session/OLE token and accepted/cancelled
transfer contract as row dragging. Internal dock-to-Library drops acknowledge the
already saved references and suppress use cleanup. No clipboard content conversion.

The existing Dropshelf official README/release notes informed collection-first
and group-transfer UX; its repository is issue-only, so no private implementation
or code was used. References:
https://github.com/williamckha/dropshelf-repo
https://github.com/williamckha/dropshelf-repo/releases

The user's Send_to_Figma source was inspected read-only at
2c10acf148d44d9493f03c6ab250606a448121a5. ROADMAP records the optional adapter,
ordered queue, authenticated local transport, stable destinations and ACK/retry
requirements, with real Figma Desktop execution as a future gate. The current
ClipDrop release starts no helper/listener and adds no network client. Schema 5
and previous clipboard/native transfer/hardware boundaries remain unchanged.

## 0.11.0: compact presence and independent named-set views

Idle presence collapses the painted surface after 650 ms, retains hidden content
layout/virtualization and uses alpha-zero pixels outside the capsule for native
input pass-through. No native per-frame resize/reposition is required on hover,
so the Save anchor and Library attachment remain stable. Hover/drop/menu/drag/
selection reveal content immediately; brief surface/fade feedback respects motion.
CollapseWhenIdle is a behavior preference, separate from style presets/opacity.
The shared DockStrip implements both live and compact Settings previews.

Per-pixel hit testing basis:
https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows
The platform contract is exercised by real Windows input into an underlying
test window; visual masking alone is insufficient evidence of pass-through.

Schema 6 adds one optional independent window record per set (maximum 12 open),
using display-relative layout, visibility and closed timestamp. IsPinned/PinOrder
remain the main active-set projection; explicit scoped operations update another
set without switching that projection. A revision cache shares item/pin lookups.
All views retain one session/store/clipboard/thumbnail owner.

Coordinator reconciliation owns both mirrored main docks and independent set
HWNDs. Close window is separate from closing a logical saved set; Recently closed
can restore the independent window/layout. Global show/hide includes open windows.
Missing-display fallbacks never overwrite saved geometry; explicit movement can
retarget an independent window. Library adopts the invoking set for membership/
filter actions. Core/native checks and real hardware/target-app checks retain
distinct boundaries; physical mixed-DPI acceptance remains open.

Migrate schemas 1–5, preserving existing sets/pins/order/styles/layout and exact
before-v6 metadata. Old builds refuse schema 6 rather than erasing window records.
Backup/version UI, opt-in clipboard monitoring and Send to Figma remain separate.
