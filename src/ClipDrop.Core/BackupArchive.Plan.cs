using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace ClipDrop.Core;

public static partial class BackupArchive
{
    // Read-only preflight, not a restore transaction. A future executor must
    // repeat this at its own write boundary and check asset destination safety.
    public static BackupActionPreview ValidatePlan(string path, byte[] currentSnapshot, BackupPreview inspected, BackupActionPreview plan, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var currentIdentity = SnapshotIdentity(currentSnapshot);
        if (inspected.CurrentIdentity is not { Length: 64 } || inspected.CurrentIdentity != currentIdentity)
            throw new InvalidOperationException("Library changed since inspection. Inspect the backup again.");
        var current = StateStore.ReadSnapshot(currentSnapshot);
        var verified = VerifyContents(path, cancellation);
        if (inspected.ArchiveIdentity is not { Length: 64 } || inspected.ArchiveIdentity != verified.Identity)
            throw new InvalidOperationException("Backup changed since inspection. Inspect it again.");
        var fresh = Compare(verified.Summary, verified.State, current, verified.Identity, currentIdentity, cancellation);
        if (plan.Clips is null || plan.Clips.Count != fresh.Clips.Count) throw new InvalidDataException("Restore choices are incomplete.");
        var actions = plan.Clips.ToArray();
        if (actions.Any(a => a is null || a.Id is null || !Enum.IsDefined(a.Action)) || actions.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != actions.Length)
            throw new InvalidDataException("Restore choices contain invalid or duplicate identities.");
        var statuses = fresh.Clips.ToDictionary(c => c.Id, c => c.Status, StringComparer.Ordinal);
        var add = 0; var replace = 0; var restore = 0; var keep = 0;
        foreach (var action in actions)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!statuses.TryGetValue(action.Id, out var status)) throw new InvalidDataException("Restore choices refer to an unknown clip.");
            switch (action.Action)
            {
                case BackupPlannedAction.Keep: keep++; break;
                case BackupPlannedAction.Add when status == BackupClipStatus.New: add++; break;
                case BackupPlannedAction.Replace when status == BackupClipStatus.ChangedMetadata: replace++; break;
                case BackupPlannedAction.RestoreFromTrash when status == BackupClipStatus.InTrash: restore++; break;
                default: throw new InvalidDataException("A restore action does not match the current clip status.");
            }
        }
        if (add != plan.Add || replace != plan.Replace || restore != plan.Restore || keep != plan.Keep)
            throw new InvalidDataException("Restore action counts do not match the selected clips.");
        cancellation.ThrowIfCancellationRequested();
        var selected = actions.Where(a => a.Action != BackupPlannedAction.Keep).Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        // Inventory only chosen clip payloads, never current settings icons or external originals.
        var subset = new ShelfState { Items = verified.State.Items.Where(i => selected.Contains(i.Id)).ToList() };
        var references = References(verified.SourceRoot, subset).ToArray();
        long bytes = 0;
        foreach (var reference in references)
        {
            cancellation.ThrowIfCancellationRequested();
            bytes = checked(bytes + verified.Entries[reference.Name].Length);
        }
        return new(Array.AsReadOnly(actions), add, replace, restore, keep) { AssetsToStage = references.Length, BytesToStage = bytes };
    }
}
