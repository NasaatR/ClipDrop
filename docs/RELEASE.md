ClipDrop 1.2 Preview — whole-card hover and individual backup differences.

- Downloads highlights the entire card immediately. The inset light rectangle and accent stripe are removed. Moving directly between files transfers the highlight without waiting for animation. Selected cards retain their selection fill and keyboard focus keeps its outline.
- Only the icon animates: 60ms ease-out entry and 40ms return, with bounded 16% enlargement and a 1-DIP lift. Panel disclosure speed remains independent. Removing the extra opacity clock/visual reduces per-row animation work; interruption, recycling and reduced-motion cleanup remain.
- Inspect backup now lists individual differing clips and their changed metadata fields. New, identical, changed and already-in-Trash identities remain separate. Include identical shows matching entries; the default list contains differences only. A bounded recycling viewport keeps long lists compact. Old results are cleared before a new operation and after failure/cancellation.
- Comparison remains read-only against the library snapshot captured at check start. No restoration, extraction, metadata overwrite or original-file modification. Conflict choice and recoverable restore/merge are the next separate stage.

Quit through the tray and install over the current version. Native1.2.0/display1.2 Preview/tagv1.2; installer/data/Chrome identities and schema7 stay unchanged. No added dependency or permission. Existing historical and recent1.1 upgrade checks remain required.

Verification covers immediate full-card feedback, rapid direct row switches, icon response/return, scrolling/recycling/focus/reduced motion, thumbnail freshness, per-clip difference categories/fields, read-only state, filters and stale-result cleanup. Full Windows/OLE, real Chrome, startup, packaging and upgrade checks are required before publication. CI timing is not a universal frame-rate guarantee.
