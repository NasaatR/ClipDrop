Emoji/PNG icons, stable right-appended clips and linked monitor docks (preview).

- Interface icons are emoji with the installed Windows font colors. Settings → Icons replaces any role with a local PNG, previews it live or restores emoji. Managed copies survive source moves and are included in saved styles. Thumbnail previews remain real images/video frames.
- New saves and re-pins append on the right. Duplicates stay put. Existing buttons are reused; a virtualized horizontal row reveals new pins and supports wheel scrolling.
- Settings → Screens: choose the main display, enable Duplicate dock on selected displays and select screens. One shared collection; independent positions and visibility. Display map, Show all / Hide all, reconnect fallback and one Library/Settings instance.
- Ctrl+Alt+Space targets the external app's screen, with pointer fallback. Tray offers Show all / Hide all. Drag stays on an assigned display; change screen in Settings.
- Unchanged appearance resources are reused; recycled thumbnails reset correctly. Monitor events are debounced, closed views release session subscriptions and monitor records survive disconnect.
- ROADMAP separates hardware stabilization, saved/recently closed panels, backup/version work and opt-in clipboard history. These later features are not bundled into this update.

The installer upgrades the existing installation and keeps data. Quit from the tray first. Runtime included; unsigned Windows x64 preview.

Data upgrades from schema 1/2 to 3. An immutable `shelf.json.schema-<old>-before-v3.bak` is kept before the first save, alongside the usual rotating backup. Older releases explicitly refuse the new schema. Original files and clipboard formats stay intact.

Monitor mirroring is a preview: core checks simulate negative origins/scaling/reconnect, and Windows checks create two real WPF docks on logical display assignments. Physical dual-monitor mixed-DPI, sleep and reconnect acceptance remains open. Video support continues to depend on installed Windows providers/codecs. PNG icons: up to 5 MB / 2048 × 2048. No network, new codec bundle, subscription or automatic clipboard monitoring.
