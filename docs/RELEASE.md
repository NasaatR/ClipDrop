Faster large libraries and saved panels you can combine and reopen.

- Library keeps existing rows, selection and containers during unchanged refreshes. Search coalesces typing; small edits update only affected rows. Selection/insertion feedback does not re-evaluate clip metadata. File availability checks run off the dispatcher with bounded concurrency.
- Debounced autosave writes immutable snapshots on a serialized background queue; explicit save/quit drains writes. Atomic replacement, recovery and original migration backups remain.
- Emoji fallback fits its full measured line box, custom PNGs keep an inset, toolbar icons have enough padding, and hover feedback no longer grows beyond zero-spacing cells. Remove the blurred effect from the translucent dock and reuse unchanged surface resources.
- Saved panels: create empty, save dock/Library selection, name, switch, and combine multiple sets into one ordered panel without repeated clips. Combining preserves the source panels and original payloads. The Panels icon is available in dock and Library; clip More includes Save as panel.
- Close panel keeps its clips in Library and makes the set available in Recently closed, including after restart. Restore & open returns its membership. Clear preserves clips referenced by any open or closed saved panel.

The installer upgrades the existing installation and preserves data. Quit from the tray first. Runtime included; unsigned Windows x64 preview. Schema 5 migrates schemas 1–4 into a Main set in the current order, keeping an immutable shelf.json.schema-<old>-before-v5.bak. Older releases refuse this schema.

Mirrored displays share one active named set; independent simultaneous panel windows are the next separate stage. Physical mixed-DPI feedback remains open. Deleting/using an unprotected clip moves the shared clip to recoverable Trash across sets; restoring returns saved memberships. The corner keep pin allows repeated use. No automatic clipboard history, network or codec bundle is added.
