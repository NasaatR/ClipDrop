WeavyClips-inspired design, source-format clipboard copying and persistent Library recovery.

- Colored floppy Save, gear Settings, three-line Library, then clip icons. The live Settings preview is the real dock component.
- Grouped settings and original application, installer and tray icon.
- Text/links stay text: no generated TXT/URL files. Original file copies stay original files.
- Save retains native memory-backed clipboard formats, including Unicode, HTML/RTF, PNG/DIB and registered app formats. Copy/click-paste restores their bytes. Process/owner-bound formats are not universally portable; partial captures are reported.
- Persistent Trash, restoration after restart, names/groups and group filters.

The installer updates the existing installation and keeps data. Quit from the tray first. Portable ZIPs are extracted separately. Runtime included; unsigned Windows x64 preview.

Data schema 2 migrates existing schema-1 libraries with an untouched previous-save backup. Older builds refuse newer metadata; back up the data folder before manually rolling back. Original files remain untouched. Native snapshot limit: 128 formats / 64 MB. No automatic clipboard monitoring or cache deletion.

CI exercises source-format roundtrips through restart, native click-paste/focus, background movement, Library restore and groups, schema migration, shared preview and WPF captures. Packaging checks upgrade from the actual 0.3.1 release, one installation, previous directory, kept data and uninstall. Photoshop/Figma app-specific objects and mixed-DPI monitor transitions still need manual testing. Saved/closed panel sets, automatic clipboard history, content editing/version history and backup UI remain future stages.
