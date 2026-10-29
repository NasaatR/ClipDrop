An idle Save capsule and independent floating windows for saved sets.

- Collapse when idle is on by default. Away from the pointer, the visible dock shrinks to Save after a short delay; hover reveals clips immediately. Supported drops, menus, selection and movement hold it open. Save stays in place, with no native window resizing on hover. Empty transparent pixels let clicks reach the app underneath.
- Settings → Dock → Collapse when idle changes the behavior live. Preview compact state uses the same dock component. Existing idle opacity remains separate; animation preferences are respected and appearance presets do not reset collapse behavior.
- Saved panels → Open window gives a named set its own floating dock (one per set, up to 12 open). Saving, adding/removing membership and reordering target that set. Use in dock switches the main dock and its mirrors; it does not retarget independent windows.
- Internal drops between different sets copy membership to the target without consuming source clips. Library opened from a window scopes its panel filter and membership actions to that set. Payloads, Keep after use and recoverable Trash remain shared.
- Close window keeps the set and layout in Recently closed. Restore window reopens it in place without switching the main dock. Show/Hide all includes open independent windows; missing/reconnected displays retain layouts and do not create duplicate views.
- One session/store/clipboard service and shared preview caches serve all windows. Per-revision item/pin indexes avoid rebuilding the same lookup for each view; feedback popups are limited to interacting docks.

Quit from the tray and run the installer over the current version. Runtime included; unsigned Windows x64 preview. Schema 6 migrates 1–5, retaining named memberships/order, monitor layouts, styles, payloads and an immutable shelf.json.schema-<old>-before-v6.bak. Older releases refuse the new schema.

Physical mixed-DPI dual-monitor feedback remains open. Independent-window records are per saved set, not duplicate payload copies. Backup/version UI, opt-in clipboard history and Send to Figma are later stages; no network client or clipboard monitoring is added here.
