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

The compact dock opens first with only **Save** and clips. Click Save to capture the
clipboard, or drop content onto the dock. Click a clip to request paste into your
previous app, or drag it out. Right-click Save/free space → **Library**, or double-
click free space, to open the searchable collection. Library intake stays in
Library; new captures are selected for **Drag N**. Checkboxes or Ctrl/Shift choose
a group; drag its counted control or any selected row. **Paste** / Enter also uses it.
Transfers copy; originals remain. After a dispatched paste or accepted external drop, unprotected clips move to Trash. Use the corner pin to keep a clip after use. Copy and cancelled/fallback transfers keep clips. **Clear** removes only clips outside all saved panels. **Ctrl+Z** restores removed cards
from Trash, including after restarting.

## Features in 0.11.0

- Compact idle Save capsule with immediate hover/drop expansion and click-through
  empty space; existing transparency remains adjustable separately.
- Independent named-set windows, scoped collection/order and closed-window restore
  alongside the main dock/mirrors. One shared Library and original payload store.

- Save-only resting dock, contextual utility menu and double-click Library access.
- Library-only intake, automatic selection of new clips, checkboxes and a counted
  group-drag control; no need to add everything to the dock first.
- Simpler Library navigation and selection-only action bar.

- Saved named panels: create empty, save the dock or Library selection, rename,
  switch, combine without duplicates, close and restore from Recently closed.
- Large-Library stability: cached rows, minimal collection updates, debounced
  search, bounded background availability checks and queued background autosave.
- Full-bound emoji/PNG fitting, enough toolbar padding, zero-gap-safe feedback
  and a lighter translucent dock without a blurred subtree effect.

- One-use clips: dispatched paste / accepted outgoing Copy drop moves unprotected cards
  to persistent Trash; a corner pin keeps a clip after use. Originals stay intact.
- Dock select mode from its menu, Ctrl/Shift selection, batch removal and group context menus.
- Drag inside a linked dock to reorder one clip or a selected group, with an insertion
  line and edge scrolling. Drag out to transfer. Reordering never consumes clips.
- Low-distraction dock: 20% whole-panel idle visibility by default, full on hover,
  menu or transfer. Settings → Dock changes idle visibility with a live preview.
- Compact four-row clip menu: Delete, Keep/Use once, Copy, More. Outside click,
  Escape, hiding or moving the owner dismisses it. More reveals secondary actions.
- Single left tray click shows/hides enabled docks together; double-click does not
  double-toggle. Hide/available Undo are in the dock menu, with direct minimized
  restore. Ctrl+Alt+Space keeps its per-display behavior.
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

## Saved panels and combining

The dock context menu → **Saved panels**, or Library → **Panels**, opens one manager. **New empty** starts
an empty set; **Save dock** takes the current live pin order into another set.
To save a Library selection, use **Panels** or clip **More → Save as panel…**,
then **Save selection** in the manager. The Library keeps original clips.

Select two or more open sets with Ctrl/Shift, enter a name and choose **Combine
selected**. It creates and opens a new set, keeping the source sets; first-seen
order is preserved and shared clip IDs appear once. Rename/Open work on one set.
**Close panel** puts it in **Recently closed**; **Restore & open** returns it,
including after restart. Closing the last open panel creates an empty Main.
Up to 100 open/recently closed sets are retained; nothing is silently pruned.

Mirrored docks share the active set. Membership/order is separate per set, while
payloads and Trash remain shared: deleting or consuming an unprotected clip
removes it from every visible set until restored. Use the corner keep pin for
repeated use. **Clear** preserves clips belonging to any open or closed saved set.
Choose **Open window** for a separate floating dock for that set; **Use in dock**
switches the main dock/mirrors. One window per set, up to 12 open. Windows display
their set name; Save/drop and membership/order actions affect that set. Internal
cross-set drops add target membership while retaining source clips.

Dock menu → **Close window** keeps its saved set and position. **Recently closed →
Restore window** returns that window without switching the main dock. Hide keeps
it open; tray Show/Hide all includes it. Drag independent windows across displays;
temporary missing-display placement preserves the original layout for reconnect.

## Collect directly in Library

Drop into Library, use **Add files**, or save its clipboard with Save / Ctrl+V.
These new clips stay in Library instead of filling the active dock. New captures
are selected immediately; **Drag N** transfers them together. Check/uncheck rows
to adjust the group without holding Ctrl. Dragging any selected row also works.
Infrequent actions are in the header **More** and selection **More** menus.

Dock Save/drop still adds to the active dock. Re-saving a duplicate into Library
does not unpin an existing dock clip. Dropping your dock clips back into Library
acknowledges that they are already saved; it does not consume or reimport them.
Clipboard formats, copy-only original file references, protection and recoverable
use cleanup are the same from either surface.

## Visibility and compact actions

By default the dock becomes a small Save capsule after the pointer leaves, while
fading to **20% visibility**. Hover expands immediately; supported drops, menus,
selection and movement hold it open. Blank space beside the capsule passes input
to other apps, and Save stays at the same position.
Menus and transfers keep it visible. **Settings → Dock → Idle visibility** controls
this (5–100%); **Preview idle visibility** shows it in Settings.
**Settings → Dock → Collapse when idle** can keep the whole row visible.
**Preview compact state** shows the actual capsule; appearance presets preserve
the collapse preference. **Opacity** remains
background-only. The dock menu → **Hide dock** fully hides it; one tray click restores it.
A minimized dock also restores directly. **Ctrl+Alt+Space** toggles the target screen.

Right-click a clip for Delete, Keep/Use once, Copy and More. Advanced actions expand
only when requested. Outside click and Esc close the menu. Right-click a free
area or Save for Library, saved panels, selection, settings and Hide; **Undo removal**
appears when Trash has entries. Undo returns the last deletion batch.

## Temporary clips and selection

- Click a clip to paste. A dispatched request moves it into Trash; click its corner
  pin first to keep it after use. The pin appears on hover and stays visible when on.
- Copy, a failed/cancelled transfer and clipboard-only fallback keep the clip.
- Dock menu → **Select clips** enters selection mode: plain clicks toggle clips. Ctrl toggles
  individual members and Shift selects a range. Use the selected-count menu → Delete to remove
  the group; **Done** returns to click-paste. Right-click acts on the group.
- Drag a clip/group inside a dock to change its position; the line shows where it
  will land. All linked docks share the order. An external accepted drop sends
  unprotected members to Trash. Use Esc during dragging to cancel.
- Manual deletion can remove a kept clip too. **Library → Trash → Restore** or
  Ctrl+Z in Library recovers a batch, including after restart, with protection intact.

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

Click the tray icon once to show/hide enabled docks. Double-click performs one toggle. Its menu opens Library/Settings and provides **Show all docks**, **Hide all docks** and **Quit ClipDrop**.
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
the content. The request archives one-use clips into recoverable Trash. OLE cleanup
uses the target’s Copy acknowledgement; it does not delete source files. It leaves the clipboard ready for manual Ctrl+V and never restores it
on a short timer. Ctrl+Alt+L provides a keyboard-accessible Library alternative.

Metadata schema 6 migrates schema-1/2/3/4/5 libraries and keeps an immutable `shelf.json.schema-<old>-before-v6.bak` before the first write. Old releases refuse newer metadata instead of resetting it.

Core owns models/persistence; Windows owns WPF and native adapters. Add transfer
formats in TransferService, not card UI handlers. Do not delete originals or time-delete exported payloads. Accepted transfers archive
unprotected clip references to recoverable Trash; internal/cancelled drags keep them.
Research and manual acceptance: [foundation notes](docs/FOUNDATION.md).
Incremental stages and the two-monitor mirror design: [roadmap](docs/ROADMAP.md).
Monitor mirroring is a preview. Native window checks and simulated topologies pass
through CI; physical mixed-DPI dual-monitor, sleep/reconnect testing remains open.
Drag a main/mirrored dock within its assigned display; choose a different display
in Settings. Independent set windows can be moved between displays.
Backup/version UI and an optional Send to Figma image adapter remain planned;
the latter is not enabled in this release. PNG icons: ≤5 MB, ≤2048 × 2048;
managed copies render at bounded size. Missing assets fall back to emoji.
Video frames/playback depend on installed Windows providers/codecs. No FFmpeg or codec installer is bundled.

MIT · Maintained at <https://github.com/NasaatR/ClipDrop>.
