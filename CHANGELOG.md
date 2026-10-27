# Changelog

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
