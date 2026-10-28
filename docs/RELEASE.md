One-use clips, keep pins, batch selection and drag reordering.

- After a dispatched paste or accepted external Copy drop, unprotected clips move straight into persistent Trash. The corner 📌 keeps a clip after use. It appears on hover and stays visible while on; Library/group menus expose the same action.
- Copy, cancelled/Esc drags, failed paste input/focus and clipboard-only fallback keep clips. Originals, clipboard blobs and image assets are never deleted by use cleanup.
- Select clips: plain clicks choose items without insertion. Ctrl toggles members, Shift selects a range. A count, Trash action and Exit selection make batch removal available without shortcuts. Right-click uses the selected group.
- Drag a clip/group within any linked dock to reorder. The insertion line and edge scrolling guide placement. Internal reorder keeps clips and shares the order across mirrors; new captures still append right. Library clips can be inserted into the dock.
- Manual deletion can remove protected clips too. Library → Trash → Restore or Ctrl+Z restores a deletion batch and protection, including after restart.
- Updated roadmap separates this workflow from physical monitor feedback, named/recently closed panels and later backup/history features.

The installer upgrades the existing installation and preserves data. Quit from the tray first. Runtime included; unsigned Windows x64 preview.

Schema 4 migrates data from 1/2/3, retaining monitor layouts and order. It keeps an immutable `shelf.json.schema-<old>-before-v4.bak` before the first save. Older releases refuse the new schema.

“Paste requested” means the input was dispatched; OLE cleanup uses the target's Copy acknowledgement. Neither proves that the destination saved a document. ClipDrop retains recoverable Trash and all originals. Monitor mirroring remains a preview pending physical mixed-DPI hardware feedback. No clipboard monitoring, accounts, network or codec bundle is added.
