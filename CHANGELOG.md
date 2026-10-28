# Changelog

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
