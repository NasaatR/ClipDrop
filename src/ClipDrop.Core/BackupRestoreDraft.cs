using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

public enum BackupChoice { KeepCurrent, UseBackup }
public enum BackupPlannedAction { Keep, Add, Replace, RestoreFromTrash }
public sealed record BackupPlannedClip(string Id, BackupPlannedAction Action);
public readonly record struct BackupActionCounts(int Add, int Replace, int Restore, int Keep);
public sealed record BackupActionPreview(IReadOnlyList<BackupPlannedClip> Clips, int Add, int Replace, int Restore, int Keep);

// Session-only choices over an inspected snapshot. This draft performs no IO
// and is never sufficient authority for a future restore: revalidate first.
public sealed class BackupRestoreDraft
{
    private readonly Dictionary<string, BackupClipStatus> _clips;
    private readonly Dictionary<string, BackupChoice> _choices = new(StringComparer.Ordinal);
    private int _add, _replace, _restore, _keep;
    public BackupActionCounts Counts => new(_add, _replace, _restore, _keep);
    public BackupRestoreDraft(BackupPreview preview)
    {
        _clips = preview.Clips.ToDictionary(c => c.Id, c => c.Status, StringComparer.Ordinal);
        _add = _clips.Values.Count(s => s == BackupClipStatus.New); _keep = _clips.Count - _add;
    }
    public BackupChoice ChoiceFor(string id) => _choices.TryGetValue(id, out var choice) ? choice :
        _clips[id] == BackupClipStatus.New ? BackupChoice.UseBackup : BackupChoice.KeepCurrent;
    public void Choose(string id, BackupChoice choice)
    {
        if (!_clips.TryGetValue(id, out var status)) throw new ArgumentException("Clip is not part of this inspected backup.", nameof(id));
        if (!Enum.IsDefined(choice) || status == BackupClipStatus.Identical && choice != BackupChoice.KeepCurrent)
            throw new ArgumentException("This choice is unavailable for this clip.", nameof(choice));
        var previous = ActionFor(id); _choices[id] = choice; var next = ActionFor(id);
        Adjust(previous, -1); Adjust(next, 1);
    }
    private BackupPlannedAction ActionFor(string id) => ChoiceFor(id) != BackupChoice.UseBackup ? BackupPlannedAction.Keep : _clips[id] switch {
        BackupClipStatus.New => BackupPlannedAction.Add, BackupClipStatus.ChangedMetadata => BackupPlannedAction.Replace,
        BackupClipStatus.InTrash => BackupPlannedAction.RestoreFromTrash, _ => BackupPlannedAction.Keep
    };
    private void Adjust(BackupPlannedAction action, int difference)
    {
        switch (action) {
            case BackupPlannedAction.Add: _add += difference; break;
            case BackupPlannedAction.Replace: _replace += difference; break;
            case BackupPlannedAction.RestoreFromTrash: _restore += difference; break;
            default: _keep += difference; break;
        }
    }
    public BackupActionPreview Preview()
    {
        var actions = _clips.Keys.Select(id => new BackupPlannedClip(id, ActionFor(id))).ToArray();
        return new(Array.AsReadOnly(actions), _add, _replace, _restore, _keep);
    }
}
