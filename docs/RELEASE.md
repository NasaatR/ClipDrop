Smooth idle fading, responsive sliders and backup inspection.

- Idle opacity now animates from the displayed value without first flashing the target. Quick returns reverse the active transition; captured presses keep the panel open and releasing outside resumes fading. Content fades before hiding and the background contracts smoothly without resizing the native window. Explicit Downloads dismissal stays immediate.
- Settings restores sliders alongside exact number entry. A large hit area captures dragging anywhere on the rail, follows the pointer continuously and coalesces expensive live previews every 40 ms with a final release flush. Enter, Escape, keyboard steps and validation remain. Scrollbar range/viewport bindings are fixed.
- Settings-only updates retain clip indexes and tile collections; already visible panels are not shown/revealed again on every preview update.
- Settings → Backups → Inspect backup verifies an existing archive in the background and shows counts/integrity without extracting or changing the library. Cancellation and closing Settings stop the work. Conflict-safe restore/merge remains a later stage.

Quit through the tray, then install over the existing version. Installer identity, schema7, data directory, per-screen placement, Chrome companion identity and original-file/native-format semantics remain unchanged. No new dependency or extension permission.

Validation covers monotonic/interrupted native opacity, capture/release outside, fade-before-hide, rapid reopening, continuous rail dragging, coalesced model writes, scrollbar dragging, persistence and read-only backup inspection. Existing clipboard/OLE, monitor, real Chrome, published startup and historical installer upgrade gates remain required. Physical mixed-DPI hardware and Photoshop/Figma gestures remain separate acceptance checks.
