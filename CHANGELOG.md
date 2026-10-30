# Changelog

## 0.15.0 — 2026-10-30

- Compact Downloads: 34-DIP one-line rows, 24-DIP previews, short times and full
  tooltip details; remove the checkbox column and persistent action footer.
  Keep optional Ctrl/Shift groups, direct file drags and contextual actions.
- Replace hidden click-to-latch with transient hover/click opening, prompt idle
  collapse, immediate outside-click/after-transfer dismissal and a visible Close
  action. Temporary observers dispose when compact/hidden; OLE/menu/move holds
  preserve the operation. Main dock timing/preferences stay independent.
- Stage 5a1 backup export: capture current metadata on the dispatcher, stream
  referenced owned assets/raw clipboard blobs/style icons off-thread, verify the
  full archive, then publish a new filename. Never overwrite any existing file;
  this also prevents directory aliases from bypassing source protection.
- Settings Backups exposes creation/progress/cancel. Missing/corrupt managed
  assets fail honestly; external originals remain references. Restore preview
  and conflict-safe import remain stage 5a2. Data schema remains7.

## 0.14.0 — 2026-10-30

- Chrome downloads from all Save As folders through a local MV3 companion and
  per-user native messaging host. Manual one-time extension setup; old completed
  entries still in Chrome history and new events are both included. Separate
  bounded per-profile catalogs, stale-revision rejection and pause/reconnect.
- Drag any Downloads row directly without a preliminary selection/click. Optional
  group selection remains; original files, Library and Trash are unchanged by
  outgoing downloads transfers. Existing folder mode stays available.
- A stacked clip inherits Keep after use from the target. Kept images/stacks
  protect all incoming members; unkept targets create unkept stacks. Legacy mixed
  targets normalize consistently when extended.
- Add local protocol/extension behavior checks, real Windows direct/group/cancel
  transfers, published host pipe tests and disposable Chrome for Testing coverage.
  Source preference persists separately from styles; schema 7 remains compatible.

## 0.13.0 — 2026-10-30

- Classify WebP/common raster files consistently, including existing references and
  browser virtual files. Bundle SkiaSharp 4.154.0 for WebP/alpha and scaled previews;
  preserve encoded originals and include the exact decoder/dependency licenses.
- Explicit browser image drops can resolve HTML img/srcset or a direct public image
  URL in bounded background work. Keep the link until ready, upgrade the same row,
  retain failures and never resurrect removed clips. Ordinary clipboard saves keep
  their source formats; page-only Pinterest URLs remain links.
- Center-drop clip stacks with a count/plus cue; edge drops insert separately.
  Open members, copy/drag all or split without deleting payloads. Flat per-panel
  references retain scoped windows, ordering, protection and Trash restoration.
- Reuse cached tile projection/containers and thumbnail paths; update a preview
  when its pending image completes. Batch pin movement uses an index rather than
  repeated item searches. Bound the member popup and keep grouping metadata separate
  from the Library's original clips.
- Schema 7 migrates 1–6 with immutable original bytes and validates stack structure.
  The optional Chrome companion and backup/version UI remain separate planned work.

## 0.12.0 — 2026-10-30

- Opt-in Downloads stack: separate hover capsule, recent files, persistent placement
  and native checkbox/range group transfer without cluttering the main dock.
- Resolve the redirected Windows Downloads known folder or watch one custom folder.
  Filter temporary/empty/nested files, settle fresh entries and reject active writers.
- Coalesce watcher events; keep one bounded background scan, stable rows/selections
  and shared thumbnail caching. Missing folders and watcher errors remain recoverable.
- Native FileDrop copy/drags and explicit Library save preserve source files.
  Folder-view transfers never consume Library/Trash entries or synthesize clip IDs.
- Size short expanded lists to content; keep unused native pixels click-through
  and the selection action area stable. Show an explicit held-open capsule state.
- Reuse the Library selector style and honor list-row padding through the shared
  template. Downloads preferences stay separate from appearance presets; schema 6
  and existing clip/window/clipboard data remain unchanged.

## 0.11.0 — 2026-10-29

- Compact idle Save capsule with delayed collapse, immediate hover/drop expansion
  and menu/selection/drag holds. Keep native geometry/content containers in place,
  hide unused painted content and allow real input through alpha-zero pixels.
- Saved collapse behavior and shared compact preview; presets preserve behavior,
  animations stop with motion disabled and no hover polling is introduced.
- Independent named-set windows with scoped save/membership/reorder, per-window
  display-relative layout, hide/show, closed-window restore and reconnect recovery.
- Cross-set internal Copy adds target membership while retaining source clips.
  Main/mirrored docks keep their shared active set; Library actions use the invoking
  set's context. Shared payloads/Trash/protection retain prior semantics.
- Reuse per-revision pin/item indexes across windows and reduce passive feedback
  popups. One store/session/clipboard service and thumbnail cache remain.
- Schema 6 preserves window records against old releases, migrates 1–5 and keeps
  immutable pre-upgrade bytes; no automatic history or Figma integration.


## 0.10.0 — 2026-10-29

- Reduce the resting dock to Save and clip content. Remove grip, permanent utility
  buttons and eye; use a compact right-click command menu and background double-
  click Library access. Preserve background movement and non-activating paste.
- Move dock selection entry to its menu, with contextual selected-count/Done text
  controls. Increase the content viewport to up to 12 clips.
- Library collects directly without auto-pinning: clipboard, picker and external
  drops. Newly added clips are selected immediately and expose Drag N; checkbox
  selection avoids Ctrl. Batch selection refreshes the action bar once.
- Native group drag works from selected rows or a counted footer control; internal
  dock-to-Library drops acknowledge existing clips without consuming/reimporting.
- Simplify Library header/footer; move infrequent actions into More. Compact menu
  glyph columns collapse when unused; check states remain visible.
- Plan a separate optional Send_to_Figma adapter after inspecting its actual
  queue/auth/receiver contracts. No integration or network behavior is introduced.
- Data schema remains 5; original files, format bytes, saved panels and recovery
  behavior are retained.


## 0.9.0 — 2026-10-29

- Reconcile and cache Library rows instead of rebuilding every list on a shared
  change; retain containers/selection on unchanged refresh, debounce search and
  notify selection/insertion properties without refreshing metadata. Bounded
  background availability checks replace synchronous file stats in row bindings.
- Serialize immutable autosave snapshots to an ordered background disk queue;
  drain on explicit save/quit, retain recovery/atomic replacement semantics.
- Fit fallback emoji by their full line box, inset images, correct icon-button
  padding and prevent hover growth beyond zero-gap cards. Reuse dock resources
  and remove its blurred subtree effect.
- Named panel manager: empty/current/selected captures, rename/open, ordered
  deduplicated combine keeping sources, close and Recently closed restore. One
  shared active set across mirrored docks; clip payloads remain in Library/Trash.
- Schema 5 migrates 1–4 with immutable backups and protects panel memberships
  from old builds. Clear keeps references in open/recently closed sets.
- Updated staged plan: independent set windows, content versions/backups and
  opt-in clipboard history remain separate releases.

## 0.8.0 — 2026-10-29

- Whole dock defaults to 20% idle visibility and reveals on hover/menu/transfer;
  saved 5–100% appearance control, idle preview, short interruptible fades and no
  continuous hover polling. Background opacity remains separate.
- Compact Delete/Keep/Copy/More clip menu with secondary actions on request.
  Temporary outside-click/foreground/Escape observation fixes non-activating menus
  that otherwise remain open over the same external foreground window. Observers
  detach on close; hiding/moving closes popups and only one menu remains open.
- Single-click tray toggle for enabled docks, no double-toggle on double-click,
  small Hide button and direct minimized restore. Targeted hotkeys are retained.
- Dock background Hide/Undo removal/Library menu gives quick batch recovery.
- Updated staged plan; data schema 4, clipboard payloads and original files remain
  unchanged. Named/recently closed sets and clipboard history stay separate.

## 0.7.0 — 2026-10-28

- Dispatched paste and accepted external Copy drops archive unprotected clips to
  persistent Trash. Copy, cancellation, failed input/focus and internal reorder keep
  clips. No originals or managed payload files are deleted.
- Per-clip corner keep pin and shared Library context action; protection survives
  restart and restore. Dock membership remains a separate property.
- Transient per-view select mode, Ctrl/Shift range/toggle selection, group context
  actions and one-batch deletion with a visible count/exit control.
- Native OLE internal single/group reorder, insertion line, edge scrolling and
  shared order across mirrored docks. Active session tokens prevent internal drops
  from triggering use cleanup. Library clips can be inserted into the dock.
- Schema 4 with immutable migration backups from 1/2/3; schema-3 order and monitor
  records are retained. Older releases refuse the new schema.
- Updated incremental roadmap; physical monitor feedback and named/recently closed
  sets remain separate work.

## 0.6.0 — 2026-10-27

- Emoji interface icons with installed Windows font colors; managed per-role PNG
  overrides, live preview, reset and deep-copied saved-style choices.
- Stable explicit pin order: preserve old visible ordering, append saves/re-pins
  right, leave duplicates in place. Reuse WPF buttons, virtualize long rows and
  scroll to newly added clips. Reset recycled media thumbnails correctly.
- Linked monitor dock preview: selected-screen mirroring, separate positions and
  visibility, shared data, screen map, native physical placement/local DIP offsets,
  reconnect fallback, routed shortcuts and tray show/hide-all. Library reattaches
  to the invoking dock without retaining old event subscriptions.
- Dedicated process command HWND keeps tray/hotkeys independent of dock lifetime.
  Debounce display/resume events and skip unchanged appearance resources.
- Schema 3 migration from 1/2 with immutable pre-upgrade metadata backup; previous
  builds refuse the new schema. No original-file or clipboard payload conversion.
- Updated staged roadmap. Physical mixed-DPI hardware acceptance remains open;
  named/recently closed panels, version/backup UI and history are separate stages.

## 0.5.0 — 2026-10-27

- Replace mixed retro/emoji toolbar and clip glyphs with one flat vector icon family.
- Add clip-scoped context menus in dock and Library, including persistent Trash and restore actions.
- Add background Windows video thumbnails, visible fallback glyphs and a shared file-change-aware cache.
- Add explicit muted video playback, pause/seek/time, codec/open timeout fallback and resource release on close.
- Publish a staged roadmap separating everyday actions/media from two-monitor mirroring and later panel sets/history.


## 0.4.0 — 2026-10-27

- Match WeavyClips dock structure: colored floppy Save, gear Settings, three-line Library, then clip icons; render the same DockStrip in Settings live preview.
- Add original app/installer/tray icon and grouped compact Settings with appearance controls.
- Stop synthesizing TXT/URL files for text/link copy or drag. Preserve original file references.
- Persist native HGLOBAL clipboard formats and restore bytes for Copy/click-paste, including HTML/RTF and opaque registered formats; report partial app-bound captures.
- Add persistent Trash, recovery after restart, editable clip names/groups and filtering.
- Migrate schema 1 to 2 with preserved backup and older-version refusal protection.
- Test upgrade from the released 0.3.1 installer, one app registration, previous directory and retained user data.


## 0.3.1 — 2026-10-27

- Keep Settings background and foreground aligned during live theme changes; verify the actual dark surface and render its preset menu.

## 0.3.0 — 2026-10-27

- Move dock and Library from empty backgrounds while preserving interactive controls and card transfers.
- Anchor Library to the dock; manual movement detaches and the link button reattaches it.
- Replace Save text and scattered icons with a consistent centered vector icon system.
- Add four built-in styles, named saved presets, independent palettes, custom RGB backgrounds/accent and compact section navigation in Settings.
- Add bounded interaction/new-pin/reveal animations with Windows and app motion preferences.
- Preserve schema-1 data, explicit zero preferences, window placement and behavior when applying styles.
- Exercise native background movement/focus, placement, style application and persistence in Windows/core checks.


## 0.2.0 — 2026-10-27

- Add non-activating floating dock with clipboard save, quick pins and copy-only drag-out.
- Keep Library separate; share one session for saves, settings and removal/undo.
- Request click-paste into a validated previous app; retain clipboard on fallback, reject changed targets and avoid automatic paste retries.
- Add dock appearance controls, bounded overflow, native receiver/focus tests and published-app startup checks.
- Preserve existing data and issued 0.1.x packages.

## 0.1.1 — 2026-10-27

- Add a visible search hint and explicit theme backgrounds for shelf/settings surfaces.
- Stabilize drop highlighting across child controls and avoid dragging from empty space.
- Correct the Alt+F4 setting label and WPF capture backgrounds.
- Skip publication from superseded commits; retain already-issued 0.1.0 assets.

## 0.1.0 — 2026-10-27

- Original C#/.NET 10 WPF Windows shelf with a separate reusable core.
- Native multi-format input/output, copy-only transfers and persistent cards.
- Multi-selection, search, pinning, preview, undo removal, tray and hotkey.
- Atomic saving, backup recovery, appearance and bounded copyable diagnostics.
- Windows checks, WPF renders, installer/portable packaging and preview releases.
