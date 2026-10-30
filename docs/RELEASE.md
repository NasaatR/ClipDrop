Movable clip panels and independently mirrored Downloads across displays.

- Fixed the main single dock snapping back to its previous screen on release. Background dragging uses native screen coordinates, keeps the DIP grab point through DPI changes, defers size/topology placement during movement and saves the destination once. The same HWND remains alive.
- Settings → Screens: One movable panel or Duplicate on selected displays for clips; independent Downloads on every display, enabled by default. Existing explicit false values, zero offsets, hidden state and selected-display opt-outs survive restart/mode changes. Named-set windows remain individually movable.
- Downloads copies share one feed/watcher/catalog and thumbnail cache; each has separate selection, position and visibility. Show all / Hide all, disable, disconnect/reconnect and disposal include every copy. No repeated reveal for unrelated layout changes.
- Moving single Downloads to a smaller work area recalculates its size so the bottom capsule remains reachable. Mirror changes during a live move retire displaced windows safely; at the 64-layout limit, unrecorded screens use temporary live positions without erasing saved layouts. Hidden missing-home fallback stays hidden.
- Copy diagnostics includes bounded numeric move geometry/DPI and mode flags, without clip contents, download paths or monitor identities. No new dock toolbar buttons, browser permissions or clipboard monitoring.

Quit ClipDrop through its tray, then install over the current version. Runtime included; unsigned Windows x64 preview. Installer identity/data location and schema7 remain unchanged. Existing Chrome companion connections keep the same extension/native-host identity; no browser reconnection is needed for these monitor changes.

Verification covers Core defaults/persistence/negative origins, native WPF mouse movement across logical display boundaries, size changes and mirror toggles during movement, direct file transfers, compact Downloads behavior, shared catalog updates, backup export, published startup, binary native messaging, a disposable real Chrome custom-folder download and historical installer upgrade/uninstall. Physical mixed-DPI monitors, sleep/reconnect and Photoshop/Figma gestures remain separate hardware/application checks.

This completes the monitor stabilization increment in the roadmap. Backup restore preview/conflict-safe import remains a separate next stage; backup export is already available.
