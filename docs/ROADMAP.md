# ClipDrop — incremental delivery plan

Updated 2026-10-29. Windows first. Each stage ends with a runnable installer,
portable package, targeted checks and user feedback before expanding its scope.
Main is the working branch. This plan supersedes the older future-stage lists
in FOUNDATION.md; those sections also document historical releases.

| Stage | Scope | Boundary |
| --- | --- | --- |
| 1 · Daily actions | Per-clip actions, modern compact surfaces | Delivered in 0.5; emoji/PNG revised in 0.6 |
| 2 · Basic video | Background video thumbnails, explicit Play/Pause, seek, muted start, open-in-app fallback | Delivered in 0.5 |
| 3a · Icons and linked monitors | Emoji/PNG roles, append-right pins, one linked dock per selected screen | 0.6 preview; automated acceptance below |
| 3c · Temporary shelf workflow | Use → Trash, keep-after-use pin, selection/batch removal, drag reorder | Current 0.7 iteration |
| 3d · Low-distraction daily use | Idle visibility, compact/dismissible menus, single-click tray, hide/restore and quick undo | Current 0.8 iteration |
| 3e · Responsiveness | Stable Library rows, bounded background file checks, background autosave, fitted icons | In progress |
| 3b · Hardware feedback | Physical dual-monitor scaling, sleep/reconnect, user feedback | Next stabilization slice |
| 4a · Saved panels | Create/name/switch, save selection, combine sets, close/restore | In progress after responsiveness fixes |
| 4b · Independent views | Simultaneous windows showing different sets | After feedback on 4a |
| 5 · Library depth | Content editing/version restore, backup export/import, explicit cache cleanup | Separate changes with migration/recovery checks |
| 6 · Clipboard history | Opt-in monitoring, pause, exclusions, deduplication and retention | Separate privacy/storage stage |

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

## Current implementation slice: responsiveness and saved panels

Use the existing C#/WPF surfaces. Keep one Library and one shared active set
across mirrored docks; do not introduce independent-window state in this slice.

- [ ] Reproduce unchanged refresh/selection churn on a 5,000-clip Library and
  constrained emoji/PNG layouts. Keep row/container identity and scroll/selection;
  notify only changed selection properties. Reuse unchanged dock resources.
- [ ] Move debounced disk flushes off the dispatcher using an immutable encoded
  state snapshot; serialize writes and drain pending work on explicit save/quit.
  Keep atomic replacement, recovery and pre-migration backup behavior.
- [ ] Fit fallback emoji by their measured full text bounds; leave hover/press
  feedback within zero-spacing card bounds. Inspect actual WPF captures.
- [ ] Add `PanelSet`/`PanelSets` in Core. Each set owns ordered unique clip IDs;
  Library/Trash keep the original clip payloads. Schema 5 migrates schema 1–4
  pins into Main with an immutable backup; reject malformed/newer data.
- [ ] Add a compact Saved panels manager from dock/Library. Create empty or save
  current/selected clips, rename, open, combine two or more into a new set in
  stable order without duplicates; leave source sets available. Close is reversible
  in Recently closed, separate from clip Trash. Restore after restart.
- [ ] Run core, native Windows gesture/render, 5,000-row stability and upgrade
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

## Later complexity stays separate

- Advanced media: hover scrubbing, timeline filmstrips, HDR/ProRes-specific
  decoding, an optional licensed codec backend and audio waveforms.
- Named panel sets: content membership, independent windows, persistent recently
  closed records and restoration without deleting clips from Library/Trash.
- Backup/version work: conflict-safe import/merge, previews before restore,
  preservation of opaque clipboard formats and original external references.
- Clipboard history: explicit enable/pause, sensible limits and exclusion policy;
  no silent monitoring introduced by a design or multi-monitor update.
- macOS, ARM64, signing/updater and cloud sync are separate platform/product work.
