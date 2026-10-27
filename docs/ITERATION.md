# 0.6 implementation slice

User request (2026-10-27): emoji interface icons with optional PNG replacements,
new pins appended at the right, focused optimization, and the next roadmap stage.
Continue the existing C#/.NET WPF app on main. No platform or framework replacement.

## Sequence and decisions

1. Emoji + PNG: shared `AppIcon` for toolbar, card fallback and clip menus.
   Settings selects a role, previews the actual icon, imports a local PNG into
   managed assets, or resets the role. Decode/size limits, fallback and deep-copy
   saved-style behavior are part of this change. Native taskbar/tray/install
   branding remains an ICO, as required by Windows.
2. Stable pins: explicit persisted pin order, old visual order retained on upgrade,
   new saves/re-pins append, duplicates stay put. Reuse existing row visuals rather
   than rebuilding the strip on every setting/item change. Virtualize horizontal
   pins and reveal the new rightmost pin when the viewport fills.
3. Linked monitors: one coordinator and session; one dock per enabled attached
   monitor. Stable monitor interface identity, individual work-area-relative DIP
   offsets, visibility, singleton Library/Settings and clean anchor subscriptions.
   Native physical positioning avoids multiplying global coordinates at mixed DPI.
   Hotkey targets the external app's display, pointer fallback; tray show/hide all.
   Preserve disconnected records; create one reachable fallback view when needed.
4. Schema 3: preserve an immutable pre-upgrade copy before the first write, migrate
   schemas 1/2 without resetting preferences, refuse future schemas. Retain existing
   legacy coordinates as migration inputs, not competing live layout sources.
5. Meaningful verification: core ordering/layout/migration/recovery checks; Windows
   emoji/PNG import/fallback/style/restart and actual WPF reconciliation/anchor
   behavior; synthetic monitor topologies plus native screen positioning.
   Published executable and previous installer upgrade checks remain required.

## Delivery boundary

Release a new version only after Windows checks, inspect representative opened UI,
then keep the staged roadmap current. Physical mixed-DPI dual-monitor testing is
not available on the one-display CI machine: ship monitor mirroring as preview
and state that limitation. Saved named panels/recently closed panels are next;
clipboard monitoring and advanced codecs are not introduced here.
