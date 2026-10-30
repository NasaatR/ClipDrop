# ClipDrop — incremental delivery plan

Updated 2026-10-30. Windows first. Each stage ends with a runnable installer,
portable package, targeted checks and user feedback before expanding its scope.
Main is the working branch. This plan supersedes the older future-stage lists
in FOUNDATION.md; those sections also document historical releases.

| Stage | Scope | Boundary |
| --- | --- | --- |
| 1 · Daily actions | Per-clip actions, modern compact surfaces | Delivered in 0.5; emoji/PNG revised in 0.6 |
| 2 · Basic video | Background video thumbnails, explicit Play/Pause, seek, muted start, open-in-app fallback | Delivered in 0.5 |
| 3a · Icons and linked monitors | Emoji/PNG roles, append-right pins, one linked dock per selected screen | 0.6 preview; automated acceptance below |
| 3c · Temporary shelf workflow | Use → Trash, keep-after-use pin, selection/batch removal, drag reorder | Delivered in 0.7 |
| 3d · Low-distraction daily use | Idle visibility, compact/dismissible menus, single-click tray, hide/restore and quick undo | Delivered in 0.8 |
| 3e · Responsiveness | Stable Library rows, bounded background file checks, background autosave, fitted icons | Delivered in 0.9 |
| 3f · Collect and transfer | Save-only dock, contextual commands, direct Library intake, checkbox selection and group drag | Delivered in 0.10 |
| 3g · Compact idle state | Save capsule, immediate hover/drop expansion, click-through empty area and preview | Delivered in 0.11 |
| 3b · Hardware feedback | Physical dual-monitor scaling, sleep/reconnect, user feedback | Next stabilization slice |
| 4a · Saved panels | Create/name/switch, save selection, combine sets, close/restore | Delivered in 0.9 |
| 4b · Independent views | Simultaneous windows showing different sets, positions and closed-window recovery | Delivered in 0.11 |
| 4c · Downloads stack | Separate hover capsule, recent ready files, native group transfers and explicit Library save | Delivered in 0.12 |
| 4d · Images and clip stacks | WebP decoding, browser image source intake, center-stack/edge-insert gestures and recovery | Delivered in 0.13 |
| 4e · Chrome downloads and consistent Keep | All save folders through an optional companion, direct row drag and target-owned protection | Delivered in 0.14 |
| 4f · Compact Downloads | Dense one-line rows, contextual actions and reliable transient dismissal | Delivered in 0.15 |
| 5a1 · Backup export | Current metadata and referenced payloads, streamed and verified to a fresh archive | Delivered in 0.15 |
| 5a2 · Backup restore | Preview, conflict-safe import/merge and recovery boundaries | Next separate increment |
| 5 · Library depth | Content editing/version restore, backup export/import, explicit cache cleanup | Separate changes with migration/recovery checks |
| 6 · Clipboard history | Opt-in monitoring, pause, exclusions, deduplication and retention | Separate privacy/storage stage |
| 7 · Send to Figma | Optional local image delivery through the existing helper/receiver | Planned; no integration in 0.14 |

## Stage 1: complete daily actions

Use the WeavyClips interaction principles with a consistent current visual
language: emoji by user request in 0.6, optional per-role PNG replacements, restrained
surfaces and visible hover/focus. Keep existing themes,
custom palettes, saved styles and the shared live preview.

Right-click acts on the clicked clip. Right-clicking an unselected Library row
selects it; right-clicking a selected row keeps the multi-selection. Menu actions
capture that selection, so later focus changes cannot delete or paste a different
item. Include Copy, Paste, plain text, Preview, Open in app, Show in Explorer,
Pin/Unpin, Rename/group and Move to Trash; Trash offers Restore. Actions never
remove originals. Closing a menu or right-clicking a pin must not trigger paste.
Keyboard menus remain available in Library. Do not add modal confirmation to
reversible removal; recovery is in persistent Trash.

Acceptance: real mouse right-click on dock/Library; correct target and selection;
copy/paste without TXT conversion; remove/restore only the requested clip;
keyboard/focus, light/dark menus, narrow Library and existing saves intact.

## Stage 2: basic video preview

Identify videos by file type while retaining their original file representation.
Request native Windows thumbnail providers off the dispatcher, with bounded
workers/queue, request timeout, a frozen bitmap cache and file-change invalidation.
Keep an emoji video symbol if no frame is available. Image decode failures also
keep a visible fallback. Use the same preview pipeline in dock and Library.

Space, double-click or Preview opens a player. Playback starts only on Play,
muted; expose pause, position/duration and seeking. Closing/hiding stops playback
and releases the file. Missing files, unsupported codecs and stalled opening have
an explicit fallback to the user's default app. No transcoding, codec installer,
FFmpeg bundle, network download, hover playback or rewrite of originals.

Acceptance: a synthetic MP4 fixture exercises native thumbnail/playback when the
Windows runner provides them; otherwise require a bounded, visible fallback.
Verify original file bytes, no autoplay, close/release and missing/invalid input.
Codec-specific compatibility remains manual. Background provider timeouts must
not replace hung workers with unlimited new threads.

## Stage 3a: icons, stable order and two-monitor mirror — 0.6 preview

**Product choice:** a mirrored dock shows the same pinned clips on both screens.
Saving/removing/pinning on either screen updates both. This is a shared shelf,
not two separately saved libraries. Independent named panel contents belong to
stage 4, so the two behaviors are not mixed.

Delivered implementation:

- Emoji defaults in toolbar, menus and file/card fallbacks. System font COLR/CPAL
  layers provide colors without bundling fonts or artwork. Missing layers use the
  system text glyph. Settings → Icons selects each role, imports a bounded PNG,
  previews it live or resets to emoji. Managed copies survive source moves;
  saved styles deep-copy choices. Native app/tray/install ICO remains Windows branding.
- Explicit pin order: preserve existing visual order on migration, append new saves
  and re-pins on the right, deduplicate without reordering. A virtualized horizontal
  row keeps existing button instances and scrolls to newly appended pins.
- The following boundaries are implemented; physical hardware acceptance remains
  open for the preview, rather than being inferred from simulated topologies:


- Add a DockCoordinator that owns view instances and display assignments. Keep
  exactly one ShelfSession/StateStore, clipboard service, foreground tracker,
  thumbnail cache, tray and hotkey registration in the process.
- Default remains one dock. Settings adds selected-screen mirroring with screen
  labels and previews; users explicitly enable the second view.
- Each view stores its own position, visibility and screen identity. Migrate the
  existing DockLeft/Top into the primary record. Use stable display identity and
  work-area-relative placement, not a monitor list index.
- One Library and one Settings window remain. Library attaches to the most
  recently invoked dock; detach subscriptions from the previous anchor before
  attaching another one. Manual Library movement still detaches it.
- The global shortcut toggles the dock for the active external app's screen,
  falling back to the pointer screen. Tray provides explicit Show all / Hide all.
  Hiding a single dock never hides another or quits the application.
- Handle negative desktop coordinates, per-monitor DIP/physical transforms and
  mixed DPI. Clamp after size/display changes, with debounced display events.
- On disconnect, retain the missing-screen layout record, suppress its view and
  keep a reachable dock on the remaining screen. Reconnect restores a view only
  when mirroring is enabled. Avoid duplicate views after sleep/reconnect.
- Valuable new display/panel records require schema 3 with preserved migration
  backup and explicit refusal by older schema-2 builds, rather than ignored data.

Stage 3b acceptance gate before removing the monitor-preview label:

| Setup/action | Expected result |
| --- | --- |
| Two screens, 100% / 150% scale | Both docks remain sharp, clickable and reachable |
| Secondary left/above primary | Negative origins do not shift or lose panels |
| Save on A, copy/remove on B | One shared item/change and one Trash entry |
| Move/resize one view | Other view keeps its own position |
| Open Library from either | Correct anchor; no duplicated event handlers |
| Disconnect/reconnect/sleep | No lost layout, duplicate window or invisible-only dock |
| Changed foreground during paste | Copy-only fallback; no paste into an unintended app |

Core layout/migration checks use negative origins, mixed-DPI projection, repeated
reconnect and immutable pre-upgrade backup. Windows checks instantiate two real
WPF dock HWNDs against logical monitor assignments on the runner's one display,
exercise shared changes/Trash, separate visibility, shortcut routing and anchor
subscription cleanup, and verify the native placement boundary. These do not
prove physical two-screen/DPI behavior; the table above needs a real acceptance pass.

## Stage 3c: temporary shelf workflow — 0.7

User priority on 2026-10-28 supersedes the old "always keep cards after drag" rule.
The clipboard/original file payload remains copy-only; only ClipDrop references
move into its recoverable Trash.

1. A dispatched click-paste or accepted external OLE Copy drop archives unprotected
   clips immediately as one persistent deletion batch. A paste request is input
   submission, not proof the destination saved its document. A target's OLE Copy
   result is its acknowledgement. Do not infer acceptance from pointer release.
2. Copy, clipboard-only paste fallback, focus/input failure, Esc/cancel, failed drag
   construction and internal drag keep clips. Originals/managed payload files are
   never deleted or reclaimed on a timer. Full Trash keeps clips and reports it.
3. `KeepAfterUse` is independent of `IsPinned` (membership in the dock). A small
   corner pin appears on hover and stays visible while protection is on. It does
   not paste or start a drag. Library context menus support the same flag for a
   selection. Explicit manual deletion can remove a kept clip; restore retains it.
4. Select button enters a mode where plain clicks toggle clips without inserting.
   Ctrl toggles members; Shift selects a contiguous range. A visible count, one
   Trash action and Exit selection make batch removal available without a keyboard.
   Right-click acts on the selected group; unselected right-click switches target.
5. Drag within any linked dock inserts at a visible line, preserving group order.
   Scroll near the edge for long rows. A session-scoped active token distinguishes
   internal reorder from an outgoing transfer, and suppresses use cleanup. Dragging
   from Library into the dock can insert previously unpinned clips at that slot.
   Selection is per view; order and protection are shared across mirrored views.
6. Schema 4 preserves the new protection flag and prevents old builds from erasing
   it. Migrate schemas 1/2/3 with an immutable pre-upgrade copy, retaining schema-3
   monitor records and explicit order. Do not remigrate schema-3 pin ordering.

Acceptance: real native click-paste to an external process; native OLE copy,
protected drop, Esc cancellation, Ctrl/Shift selection, corner pin without paste,
group reorder/no archive, batch deletion + restore, persistent protection and
migration backup. Repeat on linked dock views. Continue physical DPI feedback as
stage 3b; do not bundle saved-set/history work into this interaction iteration.

## Stage 3d: low-distraction daily use — 0.8

- Whole dock fades to 20% visibility away from the pointer; hover, menu, source
  dragging and incoming drops hold it fully visible. Short interrupted fades obey
  motion preferences; no perpetual polling for hover. Explicit show briefly reveals
  the dock. Idle visibility (5–100%) is a saved appearance value with live preview;
  background opacity remains a separate control. Fully hide via the dock/tray.
- Clip context menu starts with Delete, Keep/Use once, Copy, More. Secondary actions
  expand only on request; omit unavailable rows. Preserve captured targets/groups.
  A temporary pointer-down observer closes a menu even if the external foreground
  window never changed. Release observers on close/hide/move; one popup at a time.
- Single left tray click toggles enabled docks together; native double-click does
  not cause a second toggle. Hide/show intent includes disconnected layouts. Right-click retains tray commands. Shortcut targets
  remain per active external display. Restore minimized/hidden docks directly.
- Small Hide control and background menu (Hide, Undo removal, Library) provide
  quick exits and recovery without new persistent content models.

Acceptance: real outside click into an already-used external receiver, Escape,
More expansion, no lingering observers, active/idle/menu opacity, live preview,
native NotifyIcon callback sequences, minimized restore and quick dock undo.
Single-display CI does not establish physical mixed-DPI acceptance. Saved sets and
clipboard history stay separate; this iteration stabilizes the current shelf.

## 0.9 implementation slice: responsiveness and saved panels

Use the existing C#/WPF surfaces. Keep one Library and one shared active set
across mirrored docks; do not introduce independent-window state in this slice.

- [x] Reproduce unchanged refresh/selection churn on a 5,000-clip Library and
  constrained emoji/PNG layouts. Keep row/container identity and scroll/selection;
  notify only changed selection properties. Reuse unchanged dock resources.
- [x] Move debounced disk flushes off the dispatcher using an immutable encoded
  state snapshot; serialize writes and drain pending work on explicit save/quit.
  Keep atomic replacement, recovery and pre-migration backup behavior.
- [x] Fit fallback emoji by their measured full text bounds; leave hover/press
  feedback within zero-spacing card bounds. Inspect actual WPF captures.
- [x] Add `PanelSet`/`PanelSets` in Core. Each set owns ordered unique clip IDs;
  Library/Trash keep the original clip payloads. Schema 5 migrates schema 1–4
  pins into Main with an immutable backup; reject malformed/newer data.
- [x] Add a compact Saved panels manager from dock/Library. Create empty or save
  current/selected clips, rename, open, combine two or more into a new set in
  stable order without duplicates; leave source sets available. Close is reversible
  in Recently closed, separate from clip Trash. Restore after restart.
- [x] Run core, native Windows gesture/render, 5,000-row stability and upgrade
  checks. Review captures and publish a new version only after they pass.

Review focus: an autosave finishing after a newer edit must not mark it saved;
merge must not rewrite content or discard source panels; closing the active/last
panel must leave a reachable open panel; Trash restore must respect the active
set membership; minimum icon size/spacing and long names must remain usable.

## Saved-panel data boundary

Build named sets as one separate release: a set owns an
ordered list of clip IDs while the Library owns clips. Closing a set creates a
persistent recently-closed entry, not Trash. Restore returns its membership and
layout. Start with create/name/switch/close/restore in one dock; add independent
simultaneous sets only after those data/migration journeys pass. Keep mirrored
views of one set distinct from views of independent sets.

## Validation and next slice

The large-Library regression first failed on 0.8: even unchanged refreshes rebuilt
rows and lost realized containers. The 0.9 preflight retains row/container/selection
identity with 5,000 clips and passes native combine/close/restore gestures, tiny
fallback rendering, background snapshots and installer upgrade. Full workflow
results and actual UI captures remain in Actions; timings describe that runner,
not a universal performance guarantee.

User feedback prioritizes 3f before 4b: keep only Save and clip content permanently
visible in the dock. Remove the move grip, eye and separate settings/library/panel/
selection buttons. A compact contextual menu provides Library, saved panels,
selection, clipboard-to-Library, settings, hide and available undo. Right-click
Save/background opens it; double-click free background opens Library. Keep ordinary
background dragging and non-activating click-paste. Selection-only text controls
appear only while choosing clips. The Settings preview must use the same surface.

Library intake (drop, Add files, clipboard) saves directly to the shared Library,
without appending every capture to the active dock. Existing membership is never
removed by re-saving a duplicate. Add checkboxes for selection without Ctrl and a
counted Drag selection control; dragging any selected row still transfers the group.
Keep original formats, copy-only external OLE, accepted-transfer cleanup, cancel/
fallback recovery and per-clip protection. Library menu holds infrequent commands;
footer actions appear only for a selection. An internal dock-to-Library drop is
acknowledged without importing/archiving the same clips again.

Acceptance: actual right-click navigation/dismissal, double-click free background,
unchanged drag/paste gestures, library-only text/files, checkbox toggles, native
multi-file group drag and cancellation, no originals changed, narrow/light/dark
captures, existing 5,000-row stability and installer upgrade. No schema change. Native Windows checks cover this slice, including an actual
OS-composited narrow/light Library capture; timings remain runner-specific.

Stage 4b follows in 0.11 below: independent simultaneous set windows, scoped
collection/order, per-window placement and close/restore. Continue the physical
two-screen acceptance table with user hardware feedback; its gate remains open.
User priority on 2026-10-30 puts stage 4c first: a Downloads stack. Stage 5a then
adds explicit backup export/import with manifest validation, a restore preview
and conflict-safe merge. Content editing/version restore follows as 5b;
opt-in clipboard monitoring stays in stage 6.

## Delivered 0.11 slice: compact presence and independent windows

- [x] Keep a stable native geometry/Save anchor and shrink the painted/hit-testable
  surface into a capsule after 650 ms away. Retain the virtualized content layout
  hidden in place; transparent pixels pass mouse input to underlying applications.
  Hover/drop/menu/source transfer/movement/selection reveals the content immediately.
  Avoid per-frame native resize/reposition and permanent input polling/hooks.
- [x] Save Collapse when idle as a behavior setting, default on. Style presets and
  appearance reset preserve it; provide actual shared compact/idle previews.
- [x] Add independent window records per named set (one per set, at most 12 open),
  with visibility, display-relative placement and recently-closed timestamp.
  Schema 6 migrates 1–5 without changing existing membership/order/layout, keeps
  immutable before-v6 bytes and prevents old releases from discarding window data.
- [x] Scope save/drop/membership/reorder to the invoked set without changing the
  main active set. Keep one shared Library, clipboard service, store and media cache.
  Cross-set internal drop copies membership into the destination, keeping sources;
  global Trash/use cleanup and KeepAfterUse remain shared payload behavior.
- [x] Saved panels offers Use in dock and Open window. Close window preserves the
  set/payload and its position; Recently closed restores the same independent view.
  Closing a logical set also closes its independent view. Mirroring still shows the
  shared main active set; tray Show/Hide all includes open independent views.
- [x] Reconcile missing/reconnected screens without duplicate views or overwriting
  the absent display layout. Permit explicit independent-window movement across
  screens; keep the existing assigned-display behavior for mirrored main docks.
- [x] Verify scoped order/membership, migration/restart/closed recovery, real
  click-through behind the collapsed area, hover and supported drops, no false
  paste while opening, menu/selection/drag holds, two independent HWNDs alongside
  mirrored views, native scoped/cross-set OLE, geometry stability and installer upgrade.

Windows preflight passed 89 Core and 180 native checks, published startup/shutdown
and previous-version installer upgrade/uninstall. Real screen captures cover
compact/expanded surfaces and named windows; native input confirms blank-area
click-through. These checks do not prove physical mixed-DPI or target-app gestures.

Physical mixed-DPI hardware feedback stays open. Backup/version UI, opt-in history
and Send to Figma remain later releases; do not add them to this data/UI slice.

## Delivered 0.12 slice: Downloads stack

- [x] Separate opt-in floating capsule. Dock/tray → Downloads opens it without
  adding another permanent button to the main dock. A short list sizes its painted
  surface to its content, leaving unused native pixels click-through. Hover expands recent files;
  a click holds it open. Keep original cursor/geometry and alpha-zero pass-through.
- [x] Resolve the Windows Downloads known folder (including redirection), or watch
  one explicitly chosen folder. Show up to 40 recent files sorted by creation/write
  metadata. Ignore temporary/empty/nested/hidden/reparse entries, defer fresh files
  and omit files with active Windows writers. This is a folder view, not proof of
  browser completion, browser history or coverage of all Save As destinations.
- [x] One bounded background scan at a time, debounced/coalesced watcher events,
  recovery on watcher error and hover/explicit refresh, cancellation on close and
  no permanent rescan timer. Retain unchanged rows, selection and preview cache.
- [x] Checkbox/range selection, native single/group Copy drags, Copy and explicit
  Save to Library. Incoming dock/Library transfers use ordinary FileDrop, not
  synthetic internal clip IDs. Download transfers never consume/delete originals
  or create Library/Trash entries without the explicit save/drop action.
- [x] Save enabled/folder/layout/visibility as behavior preferences; presets keep
  them. Default disabled means no folder watcher before opening/enabling it.
  Keep schema 6: the list is derived from disk and adds no new payload/history model.
- [x] Run native watcher/rename/selection/OLE/close/reopen/menu/collapse checks,
  inspect dark/light expanded and compact captures, then verify installer upgrade.

Preflight passed 100 Core and 200 Windows checks plus installer upgrade, with
actual expanded/compact/light screen captures. The final release adds explicit
content-size and blank-region input/latch checks.

Next slices: 5a backup/export/import; 5b content versions. Multiple watched folders,
filters/fan layouts, browser completion APIs and browser Save As coverage need a
separate design/compatibility slice after feedback. Clipboard monitoring and Send
to Figma retain their own stages. Physical mixed-DPI hardware acceptance stays open.

Implementation sources (reviewed 2026-10-30): Microsoft
[known folder IDs](https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid),
[SHGetKnownFolderPath](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shgetknownfolderpath),
[FileSystemWatcher events/error/buffer guidance](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0).
Code is original MIT implementation using existing ClipDrop components.

## Delivered 0.13 slice: browser images and clip stacks

User feedback on 2026-10-30 prioritizes image intake and clip-level grouping before
backup UI. Combining saved panels remains available; it is distinct from stacking
clips inside one panel.

- [x] Classify WebP and common raster extensions consistently in file import,
  virtual browser files, existing saved references, Downloads, Library and preview.
  Bundle pinned SkiaSharp 4.154.0 for WebP decoding/alpha and scaled previews without
  relying on a separately installed Windows WebP codec. Keep original file bytes;
  first-frame image previews are distinct from animated playback. AVIF/HEIF and
  uncommon formats still depend on available decoders; never claim every format.
- [x] Read actual HTML img/srcset or a direct raster-image URL on the STA, then
  fetch the public image off the dispatcher. Preserve a saved link until success;
  upgrade the same item/key without undoing later user removal. Limit bytes,
  pixels, redirects, queue, workers and time; no cookies/proxy/credentials or
  private/loopback DNS connections. Default on only for explicit drops, with a
  saved behavior switch. Ordinary clipboard saves retain their raw formats.
- [x] Add flat per-panel ClipStack references. A center drop combines cards; an
  edge drop inserts a separate card. Show a count and a distinct plus target cue.
  Click opens members, drag transfers all, menu splits without deleting payloads.
  Preserve scoped windows, order, protection, Trash restore and saved-panel copies.
  Keep a bounded member popup and reuse per-revision tile/index caches.
- [x] Schema 7 migrates 1–6 with immutable original bytes, promotes old local WebP
  references and prevents older builds from erasing stack metadata. Downloads
  preferences, closed windows, clipboard payloads and saved styles remain intact.
- [x] Verify native WebP pixels/alpha, previews, center/edge OLE, stack copy/drag,
  split/restore, pending-image row identity, cancellation/denial and late completion.
  Inspect real dark/light/popover captures and run published startup/installer checks.

Next stage remains 5a backup/export/import with preview and conflict-safe merge,
then 5b content versions. Do not combine these storage journeys with this fix.

Optional Chrome companion after feedback: use an image context-menu command (and
an optional hover action), currentSrc/srcset and explicitly granted image-origin
access to send bounded image bytes plus provenance to ClipDrop. Use a per-user
native messaging host restricted to the installed extension ID and a local IPC
handoff to the running app; no unauthenticated public/local HTTP receiver. Preserve
existing ordinary drag/copy and avoid automatic Pinterest page scraping, browser
cookie harvesting or assumptions that a pin-page URL identifies its image. Actual
Pinterest/Reddit browser-to-Windows execution remains a manual compatibility gate.
0.13 did not ship an extension or native messaging registration. 0.14 adds a
downloads-only companion; image-send actions remain a separate future slice.

Primary implementation evidence reviewed 2026-10-30:
[SkiaSharp package/version/license](https://www.nuget.org/packages/SkiaSharp/4.154.0),
[Chrome image context menu API](https://developer.chrome.com/docs/extensions/reference/api/contextMenus),
[Chrome native messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging).
The pinned NuGet API surface was compiled separately before the Windows build;
application and grouping code are original MIT implementation.

## Delivered 0.14 slice: all Chrome save locations and direct file drag

The user's downloads journey takes priority over 5a backup work for this slice:
Chrome download completed anywhere → hover Downloads → grab a row → native copy
to another app. No preliminary row click, checkbox or group action is required.
Checkbox/range selection remains an optional group path. Dragging an unselected
row transfers only that row even with a different group selected. Capture the
pressed row and immutable file refs before OLE, hold the floating window open,
release capture before starting, preserve originals on accepted/cancelled copies.

Compare the actual available sources:

| Source | Useful property | Decision |
| --- | --- | --- |
| Watch one folder | No browser setup, file events, existing implementation | Keep as explicit alternate mode; cannot know all Save As locations |
| Read/copy Chrome History SQLite | Existing history rows include target_path and completion/state | Reject for live product intake: Chromium takes an exclusive lock; copying the whole browsing database adds inconsistent-snapshot, privacy and schema cost |
| Chrome downloads API + native messaging | Browser completion state, actual absolute filename, event updates, old entries still in browser history | Selected; one-time manual extension setup per Chrome profile |

The original MIT MV3 companion requests only downloads/nativeMessaging/storage/
alarms. No content scripts, website permissions, History/cookie API, source URLs,
accounts, custom HTTP receiver or browser installation automation. incognito is
not allowed. It sends bounded metadata (up to 200 completed entries/profile,
512 KiB frame limit) to a small per-user Windows stdio host restricted to its
stable public-key-derived extension ID. The dock combines up to 16 profiles and
shows the latest 40 locally readable paths ordered by browser completion time.
Completed zero-byte files are valid; missing/blocked/interrupted items are omitted.

The host is packaged alongside the app, writes a separate atomic disposable
catalog under the data directory, serializes writers and rejects stale revisions.
It never touches shelf.json or opens/moves files from a message. No app wake or
tray window is started for an extension event. Catalog events/hover refresh one
bounded background reader, and unchanged rows/previews remain stable. The MV3
worker handles events and reconciles every five minutes through chrome.alarms;
it does not keep a permanent connection/process or poll from the app.

The installer registers HKCU32 native messaging and removes only its own current
registration on uninstall. Portable Connect Chrome registers its verified bundled
manifest and opens the folder/setup guide. Load unpacked is a manual, one-time
step; Chrome Web Store submission is separate. Use popup Pause to clear that
profile after acknowledgement. Removing the extension stops future updates but
the last local catalog stays until explicitly cleared. Existing custom-folder
preferences use the folder mode on upgrade; explicit source choice persists
outside appearance settings. Schema 7 is unchanged: these are additive settings.

Stack inheritance: determine Keep from the target before combining. Every member
of the resulting stack inherits it, including incoming stack members and archived
references retained for recovery. A legacy mixed target with any kept member is
normalized to kept. An unkept target makes its incoming clips unkept. Protection
belongs to shared underlying clips, so it also applies in other saved panels.
Do not change raw payload bytes, grouping identity, internal drop ordering or
accepted-use Trash semantics.

Acceptance includes pure frame/revision/profile and folder-independent ordering
checks, extension selection/privacy/reconnect behavior, real Windows row/cancel/
group drags, WebP in the Downloads view, protected stacking/use and published
WinExe stdin/stdout framing. A disposable pinned Chrome for Testing exercises a
real download outside Downloads → actual extension event → native host ACK →
local catalog, plus popup pause. This does not establish page-specific Pinterest/
Reddit drag behavior, physical mixed-DPI hardware or Photoshop/Figma compatibility.

Research reviewed 2026-10-30 (references, no upstream application code copied):
[official Chrome downloads API](https://developer.chrome.com/docs/extensions/reference/api/downloads),
[native messaging protocol/Windows registration](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging),
[Google's native messaging example](https://github.com/GoogleChrome/chrome-extensions-samples/tree/main/api-samples/nativeMessaging),
[Chromium History exclusive-lock source](https://chromium.googlesource.com/chromium/src/+/5aed270e6d3303ca91a02910b558f614bddba299/components/history/core/browser/history_database.cc),
[browser-history snapshot reader](https://github.com/browser-history/browser-history),
[BrowserDBParser downloads query](https://github.com/ThreeBlueZs/BrowserDBParser),
[MV3 example migration discussion](https://github.com/GoogleChrome/chrome-extensions-samples/issues/644).
The full-database forensic readers are reference alternatives, not dependencies.

Stage 5a1 exports retained opaque clipboard blobs in 0.15. Next is **5a2 restore
preview and conflict-safe import/merge**, then **5b content versions**. Follow-up
Chrome image-send actions can reuse the installed host identity only after a
separately versioned bounded image contract/permissions/IPC journey is implemented.
Store distribution, progress UI, longer-history browsing, moved-file tracking and
other browsers remain separate increments. Do not add them all to this release.

## 0.15 implementation plan: compact Downloads, then backup export

Goal: direct file pickup with more visible rows and predictable dismissal, followed
by a complete first backup increment. Native WPF/.NET 10, no new dependencies.
The user's requested sequence is implemented here in the current working tree;
backup import/merge remains a separate 5a2 step.

### Task 1: compact transient Downloads

- [ ] `DownloadsWindow.xaml/.cs`: 34 DIP one-line rows, 24 DIP thumbnails, full
  path/size/time on tooltip, no checkbox column or persistent action bar. Keep
  direct single/group copy-only drags; Ctrl/Shift and contextual Select all choose
  groups. Show source setup in empty/error state and keep actions contextual.
- [ ] `DockPresence` transient mode + `FlyoutDismiss`: no click-to-latch state;
  300 ms idle collapse after a 90 ms pointer settle, immediate outside-click/action
  dismiss. Observe outside input only while expanded, pass it through, dispose on
  compact/hide/close and hold during actual movement/press/OLE/menu interactions.
  Main dock presence keeps its existing preferences and timings.
- [ ] Native `DownloadsProbe`: prove no selector, at least ten visible short rows
  in the normal window, direct/cancel/group transfers, drag while foreground app
  remains unchanged, outside click, repeated open/close, menu and focus changes.
  Inspect light/dark/empty/long-name captures and keep existing Chrome tests.

### Task 2: stage 5a1 verified export

- [ ] `StateStore.CaptureSnapshot` / `ReadSnapshot`: capture the current UI-owned
  metadata once; persistence reuses the same immutable snapshot path.
- [ ] `BackupArchive.Create(root, snapshot, destination, progress, token)`: stream
  referenced owned assets, opaque clipboard blobs and current/saved-style PNGs
  into a versioned ZIP; include current metadata, Trash, panel/window state and
  original external file references. Do not copy external originals, orphaned
  caches, exports no longer referenced or the disposable Chrome catalog.
- [ ] `BackupArchive.Verify`: validate version/paths/required membership, lengths
  and SHA256 of every entry by bounded streaming. Reopen/verify the finished temp
  archive, then atomically publish to a fresh filename; failure/cancel keeps any existing destination
  and source metadata untouched. Never extract/restore in this increment.
- [ ] `BackupSection` in Settings: choose destination, background create/verify,
  honest completion/error, cancel and disposed-window cleanup. Each copy uses a new filename; explain external
  originals remain references. No controls added to the floating dock.
- [ ] Core backup checks: raw bytes/Trash/panels/zero settings/style icons,
  deduplication, changed/missing owned asset, corrupt archive/hash, traversal,
  cancellation and preserved destination. Native check: current unsaved metadata
  snapshot, responsive export UI and cancel. Finish with installer/Chrome CI.

Review focus: invisible layered pixels; deactivation during a menu/OLE operation;
missed mouse-up; broken/moved external references; current unsaved metadata and
shared hash assets; failure before atomic publication. All belong to the checks
above. 5a2 adds archive preview, conflict-safe import/merge and recovery boundaries;
5b adds content versions. Fan/grid layouts and more elaborate Shell drag images
stay after feedback on this simpler list.

Implementation finding: destination path strings cannot reliably detect aliases
of live metadata/external files. Export therefore **never overwrites an existing
file**, including an existing archive: default names include seconds and the final
move is atomically no-overwrite. A regression failed under the old replace path,
then passed with this rule; a local Linux directory-symlink reproduction also
preserves shelf.json. Windows CI checks the no-overwrite/cancel path; no physical
Windows junction claim is inferred from the Linux case.

Research for this slice (2026-10-30, source inspected; reference only):

| Reference | Evidence adopted | Boundary |
| --- | --- | --- |
| [Apple Dock folders](https://support.apple.com/guide/mac-help/mchl231f08fb/mac) | compact list/grid/fan choices, click-outside dismissal | retain ClipDrop hover; a fan uses more space, so defer it |
| [Downloads Stack for Windows Tray](https://github.com/Vivoxti/Downloads-Stack-For-Windows-Tray/tree/bfb316ae348347821b943d4108e2396ff989bd74) | MainWindow.xaml/.cs state transitions, compact icon/name rows, outside clicks with same foreground, drag interaction hold; ShellService.cs original-file Shell transfer | MIT; no source/assets copied; keep ClipDrop copy-only contract rather than offer Move |
| [DropStack](https://github.com/Blindside-Studios/DropStack/tree/9199a923ea6fddf58df31e971e742a9a777d63c4) | SimpleMode.xaml.cs light-dismiss and drag-completion close; Ctrl/Shift optional grouping, contextual actions | no repository license detected; inspected as behavior reference only |
| [Stacky](https://github.com/pawelt/stacky/tree/d6b0df180512ecd7884b65fc0bbb66aba803c511) | small Windows icon-list approach and minimal chrome | Apache-2.0; README explicitly says unmaintained; no code imported |

Original implementation uses existing themed surfaces/thumbnails and bounded
refresh. It avoids per-row blur/shadow pipelines, new frameworks, recursive disk
scans, permanent global observers or additional controls on the main dock.

Native preflight confirmed compact rows without checkboxes, at least ten visible
rows, original-file single/group/cancel transfers, contextual copy/save, immediate
outside-click pass-through, repeated capsule open/close, menu holds and disposal.
It also reproduced an asynchronous foreground notification closing a newly hovered
flyout; the observer now checks current foreground and pointer both before queueing
and before dismissal. Mouse outside-click intent still wins over a queued focus
notification. The focused case passed after that fix.

The backup native probe reproduced a direct STA call without an ambient UI
SynchronizationContext. Export entry now schedules its async core through the
view's Dispatcher so snapshot, progress and continuations all belong to the UI;
file compression/hashing remain in Task.Run. Final full Windows/package/Chrome
verification is the release gate. Local Core run passes151 checks (one actual
symlink case is Linux-only; Windows runs150). Node companion tests pass7/7.

## Future: optional Send to Figma integration

Inspected [NasaatR/Send_to_Figma](https://github.com/NasaatR/Send_to_Figma/tree/2c10acf148d44d9493f03c6ab250606a448121a5)
on 2026-10-29 (README, bridge/model.go, bridge/server.go, bridge/collection.go,
extension/native-client.js and plugin/main.js). Its current bridge has a persistent
ordered image queue, native messaging producer, authenticated loopback RPC and a
paired WebSocket Figma receiver. The plugin creates an ordered managed collection;
successful insertion returns an acknowledgement/node ID. Existing bounds are
20 MiB per image, 4096 px per side, 200 pending and 256 MiB queue. This source review
does not establish actual Figma Desktop execution; that repository also records
this manual verification gap.

1. Define a versioned optional producer adapter in ClipDrop. Reuse one installed
   helper and receiver rather than merging both UI/codebases or starting duplicate
   listeners. Negotiate capabilities/version, pair through an explicit local setup,
   protect tokens and preserve existing browser/native clients. Do not hardcode or
   copy another installed app's credentials into ClipDrop or releases.
2. First supported journey: selected local image clips → Send to Figma → chosen
   pinned collection → existing append/grid layout. Reserve IDs/sequence before
   upload, validate image type/size and preserve selection order. Plan explicit
   adaptation for local files/provenance; do not assume the browser-oriented
   reserve/upload contract is already a supported desktop-producer API. Bind a
   producer and destination per batch; never change the browser's global default
   receiver merely to send a ClipDrop selection elsewhere.
3. Show queued, waiting for receiver, confirmed and failed separately. Preserve
   operation IDs across retry; a submit/HTTP success is not insertion success.
   Only a matching receiver ACK may trigger normal recoverable use cleanup for
   unprotected clips. Do not discard payloads or replay ambiguous requests blindly.
4. Keep unsupported text/video/folder content available through ordinary native
   drag/copy. Do not silently fetch link URLs, convert arbitrary files or promise
   editable browser/Photoshop/Figma nodes. Clipboard monitoring remains separate.
5. Before delivery verify installed-helper discovery/version mismatch, receiver
   closed/reopened, wrong destination, lost ACK, retry deduplication, partial batch,
   restart and recovery. Require real browser → ClipDrop → Figma Desktop execution
   in an editable document before claiming end-to-end compatibility.

No network client, helper startup, pairing migration, Send-to-Figma repository edit
or Figma document write is included in the current UX release.

## Later complexity stays separate

- Advanced media: hover scrubbing, timeline filmstrips, HDR/ProRes-specific
  decoding, an optional licensed codec backend and audio waveforms.
- Backup/version work: conflict-safe import/merge, previews before restore,
  preservation of opaque clipboard formats and original external references.
- Clipboard history: explicit enable/pause, sensible limits and exclusion policy;
  no silent monitoring introduced by a design or multi-monitor update.
- macOS, ARM64, signing/updater and cloud sync are separate platform/product work.


## 0.16 stabilization plan: movable docks and linked Downloads

User priority: freely move the single clip dock between screens; independently
choose clip mirroring and Downloads mirroring. Downloads defaults to all attached
displays, clips retain the existing single-dock default. Existing explicit choices
and disconnected display positions must survive restart. One session/catalog,
copy-only OLE and original files remain unchanged.

Root cause found in source: main-dock SaveGeometry clamps to its old assignment
without retargeting; queued SizeChanged placement can also project the old layout
while the pointer is moving. Downloads already retargets on release. Positioning
must be suspended during capture/movement, then committed against the destination.

Approach: retain the native WPF views/coordinator; add a per-screen Downloads view
map over one shared feed, plus bounded per-screen layout records. Do not make a
second app/store/watcher or poll the desktop. One movable mode retargets the same
HWND. Mirrored mode keeps one copy per selected screen and moves each copy within
its own work area; switch to one movable mode for cross-screen relocation. Named
independent panel windows remain separately movable and are not multiplied.

- [ ] Reproduce the old single-dock snap-back with real mouse input and two
  non-overlapping logical work areas on the Windows runner. Prove the regression
  fails before the fix; separately test Core defaults and migration.
- [ ] Use native screen cursor coordinates for background dragging; suppress and
  coalesce size-driven placement during gestures, commit the destination/offset
  once on release. Preserve physical coordinates/negative origins, clamp only
  after the gesture, and retain the same HWND in single mode. Defer topology
  reconciliation during a live move and flush it afterward.
- [ ] Add MirrorDownloads=true and bounded DownloadsLayouts records, retaining
  legacy DownloadsLayout as the single-mode anchor. Preserve explicit false/0,
  missing-screen records, visibility and original metadata. Keep schema 7 because
  these are additive settings; backup export includes them through its snapshot.
- [ ] One shared DownloadsFeed, one view per connected screen in mirror mode;
  independent position/visibility and selection, all views disposed on disable,
  global hide/show includes every copy. No repeat reveal on unrelated changes.
- [ ] Settings → Screens: separate clip and Downloads mode choices, optional clip
  display selection, readable screen pickers/map, reset both sets of positions.
  Settings → Downloads links the mirror choice to the same persistent setting.
- [ ] Native checks: direct A→B→A move, width change mid-drag, named-window move,
  mirror on/off, shared file update/direct transfer, hide/show, disconnect/reconnect,
  repeated refresh/dispose, explicit false and saved layouts after restart.
- [ ] Run full Core/Node/native Windows, published host/real Chrome/install checks;
  inspect screen-settings captures and deliver a fresh version. Physical mixed-DPI
  monitors/sleep remain a user-hardware check, not inferred from logical areas.

Next: finish stage 3b hardware feedback before 5a2 backup restore preview and
conflict-safe import. Do not combine backup import with this monitor stabilization.

API references checked for this fix: [WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged)
(window scaling/position changes on monitor transitions),
[GetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos)
(screen coordinates), and [MonitorFromWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromwindow)
(largest intersection and nearest fallback). WPF retains its DPI-message handling;
ClipDrop must not undo it with an old display assignment during a drag.
