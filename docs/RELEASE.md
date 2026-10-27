Compact dock preview for Windows, inspired by WeavyClips workflow:

- Save clipboard in one click; new items appear as dock pins.
- Click a pin to request paste into the previous app, or drag it out natively.
- Separate searchable Library; Enter / Paste uses the selection.
- Non-activating dock, bounded pin row, shared state, light/dark surfaces,
  button size/spacing/corners/background opacity, and copyable diagnostics.
- Ctrl+Alt+Space toggles the dock; Ctrl+Alt+L opens Library.

Download `*_windows-x64_setup.exe` or extract the entire portable ZIP. Runtime
included. Quit the previous version before updating; existing libraries remain.
Unsigned Windows x64 preview.

CI checks data recovery, actual OLE clipboard formats, a real dock click into a
separate Windows editor process, focus preservation, WPF views, published-app
startup/shutdown, ZIP contents and installer/uninstaller. Photoshop/Figma/browser
compatibility still needs manual testing. Blocked or changed targets use copy-only
fallback. Full design presets, persistent trash/closed panels and automatic
clipboard history are planned next stages and are not included in this release.
