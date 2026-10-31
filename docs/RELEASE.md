ClipDrop 1.1 Preview — faster hover and verified backup comparison.

- Hover responds with 90ms cubic ease-out entry and 55ms exit, independently of panel disclosure speed. Stronger 1.16scale,1DIP lift and an accent cue stay within the row. No long trailing animation, native-window resize or layout growth.
- Hover state is lazy. Recycling preserves the current pointer effect rather than resetting/restarting it; focused rows reevaluate on owner-list entry/leave, preventing a second pointer hover while retaining keyboard focus styling. Disabled/reduced motion, unload and interruption guards remain.
- Concurrent thumbnail requests coalesce the in-flight file stat as well as decoding. Key/value removal cannot discard a newer task. Same-path validation retains the current picture until a fresh result arrives; different/recycled paths clear immediately. No time-based stat cache hides replacements or deletion.
- First stage5a2b1 slice: Inspect backup verifies the archive, then compares active clip IDs against the library snapshot captured at check start. It reports new IDs, identical recorded metadata, changed metadata and IDs already in Trash. No extraction, import, restoration or overwrite. Detailed conflict selection/recoverable restore remains next.

Quit via the tray and install over the current version. Native1.1.0/display1.1 Preview/tagv1.1; installer identity, schema7/data, Chrome identity, anchored Downloads and original-file semantics stay unchanged. No new dependency or permission.

Verification covers response/return latency, minimum-row bounds, rapid pointer sweeps, focus→outside→different-row transitions, stationary-pointer scrolling, recycled state, thumbnail concurrency/freshness/deletion and read-only backup comparison. Existing Windows/OLE/Chrome/startup plus historical and 1.0 upgrade gates remain required. Timing observations on CI do not guarantee frame rate on every machine.
