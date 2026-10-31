# ClipDrop release versions

From this user-requested migration, public releases use **1.0 Preview**, then
**1.1, 1.2, …, 1.9, 2.0**. Increment for a changed delivered product, not for each
commit. Numbers identify releases; they do not express percent completion or a
SemVer public-library compatibility contract. Keep Preview until acceptance
supports a stable channel.

`Directory.Build.props/VersionPrefix` is the authority. Keep its native value
`major.minor.0`, with major1–255 and minor0–9. Derive other representations:

| Surface | Example |
| --- | --- |
| Native assemblies / installer / Chrome version | 1.0.0 |
| UI, diagnostics, Chrome version_name | 1.0 Preview |
| Git tag | v1.0 |
| Installer | ClipDrop_v1.0_windows-x64_setup.exe |
| Portable archive / root | ClipDrop_v1.0_windows-x64_portable.zip / ClipDrop_v1.0 |
| Chrome companion | ClipDrop_v1.0_chrome-companion.zip |

`python3 scripts/release-version.py` validates and prints the release label;
`--native` prints its native representation; `--next` prints the next native
value without changing files. Example:1.9.0 →2.0.0. Source Chrome metadata is
checked against the authority by the browser tests. Packaging and publication
derive their labels from this helper; runtime UI derives from assembly metadata.

The transition **0.19.0 →1.0.0** is newer under numeric version comparison. Retain
all issued 0.x tags, files and links. Never rename or replace an existing release
asset. AppId, data directory, schema, extension key and native host ID do not
change with numbering. Validate update from the previous issued version as well
as the historical migration fixture. A Preview label is not an excuse to skip
the existing Windows, transfer, Chrome, startup and installer acceptance gate.
