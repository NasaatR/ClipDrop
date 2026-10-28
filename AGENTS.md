# ClipDrop development

- Canonical source: https://github.com/NasaatR/ClipDrop. Preserve provenance and applicable notices when reusing substantial source.
- Original code: MIT. Product credit: `Made by Nasaat`, in Settings.
- English UI/docs/identifiers by default; keep README.ru.md aligned.
- Run `dotnet run --project tests/ClipDrop.Core.Tests -c Release` and, on Windows, `dotnet run --project tests/ClipDrop.Windows.Tests -c Release -- artifacts/qa`.
- WPF clipboard/OLE data objects stay on the STA/UI thread. Do not pass live foreign COM data into background tasks.
- Never delete/move originals or time-delete exported payloads. External drags remain copy-only. Per the user’s 2026-10-28 request, an accepted external copy-drop or dispatched paste moves unprotected clips to persistent Trash; cancellation, copy-only fallback and internal reorder keep clips. KeepAfterUse protects against automatic removal; explicit removal remains allowed.
- StateStore must load before saving. Newer schemas/unreadable data must never silently reset.
- Version comes from Directory.Build.props. Never replace issued release bytes under the same version.
- Builds/renders do not prove Photoshop/Figma/browser gesture compatibility.
