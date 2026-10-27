Clip actions and basic video preview, with a staged roadmap.

- One modern flat vector icon family across dock, Library, menus and live preview.
- Right-click clips for Paste, Copy, Preview, Open in app, Show in Explorer, Pin/Unpin, Rename/group and Move to Trash. Trash offers Restore. Menus keep their original target and selected group.
- Video thumbnails use native Windows providers in a bounded background queue. Unsupported frames keep a visible video symbol.
- Space/double-click/Preview opens video controls: explicit Play/Pause, seek, position/duration, muted start and default-player fallback. No autoplay or transcoding.
- ROADMAP.md splits the current two steps from next-stage two-monitor mirroring, named/closed panels, backup/versioning and opt-in clipboard history. Mirrored docks are planned, not part of 0.5.0.

The installer upgrades the existing installation and keeps data. Quit from the tray first. Runtime included; unsigned Windows x64 preview. No new data schema in this release.

Video support depends on installed Windows thumbnail providers/codecs; unsupported or stalled media exposes Open in app. No FFmpeg or codec installer is bundled. Original files and clipboard formats stay intact. CI checks native clip actions, transfer/focus, media fixture/fallback, recovery, WPF surfaces, published startup and previous-version upgrade. Physical two-monitor/DPI acceptance belongs to the next stage.
