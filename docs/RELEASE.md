WebP previews, actual browser-image intake and clip stacks.

- WebP now has a bundled decoder, alpha and scaled previews across dock, Library and Downloads; old WebP file references are recognized on load. Source image files and downloaded encodings stay intact. Previews use a common bounded cache; no separate WebP codec installation.
- An explicit browser drop can use image bytes/files or the actual img/srcset/direct image URL. Public image retrieval runs in the background, upgrades the same clip and keeps its link on failure. Removing a pending clip prevents late resurrection. Settings → Dock → Fetch image URLs on drop disables retrieval; ordinary clipboard saves retain their original formats.
- Drop into the center of a card to stack clips, or at an edge/between cards to insert separately. The plus/line cues show the action. One counted card opens its members, drags/copies them together and offers Split stack without deleting them. Protection, use-to-Trash and restore work per member; saved sets/independent views retain their structure.
- Tile projection, member popups and background requests are bounded. Unchanged rows are retained; a completed image updates its actual thumbnail. The installer includes the native decoder and its exact notices, omitting native debug symbols.

A Pinterest page link alone is still a link. A public image may also be inaccessible without browser authorization; no cookies or page scraping are used. The optional Chrome image action/native-messaging companion is in the roadmap, not installed here. PNG/JPEG/GIF/BMP/TIFF/ICO keep their normal decoding path; uncommon raster formats depend on available decoders. Image previews show one frame.

Quit ClipDrop from the tray and install over the current version. Runtime included; unsigned Windows x64 preview. Schema 7 preserves clips, panels/windows, Downloads preferences, styles and raw clipboard payloads while adding clip-stack metadata. Migration keeps shelf.json.schema-<old>-before-v7.bak; older releases refuse newer metadata.

Next: backup export/import with restore preview, then content versions. Physical mixed-DPI hardware and actual Pinterest/Reddit/Photoshop/Figma application gestures remain manual compatibility checks; native tests use representative transfer data and controlled HTTP results.
