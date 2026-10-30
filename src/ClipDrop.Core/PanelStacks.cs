using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

public sealed record DockTile(string Key, string? StackId, ShelfItem[] Items);

// Stacks are flat per-panel references, never new payloads or nested containers.
public static class PanelStacks
{
    public static void Prune(PanelSet panel)
    {
        var members = panel.ClipIds.ToHashSet();
        foreach (var stack in panel.Stacks) stack.ClipIds.RemoveAll(id => !members.Contains(id));
        panel.Stacks.RemoveAll(stack => stack.ClipIds.Count < 2);
    }
    public static bool Normalize(PanelSet panel)
    {
        if (panel.Stacks.Count == 0) return false;
        var groups = panel.Stacks.SelectMany(s => s.ClipIds.Select(id => (id, s))).ToDictionary(p => p.id, p => p.s);
        var seen = new HashSet<string>(); var order = new List<string>();
        foreach (var id in panel.ClipIds)
            if (!groups.TryGetValue(id, out var stack)) order.Add(id);
            else if (seen.Add(stack.Id)) order.AddRange(stack.ClipIds);
        if (panel.ClipIds.SequenceEqual(order)) return false;
        panel.ClipIds = order; return true;
    }
    public static DockTile[] Tiles(PanelSet panel, IReadOnlyList<ShelfItem> pins)
    {
        if (panel.Stacks.Count == 0) return pins.Select(i => new DockTile(i.Id, null, new[] { i })).ToArray();
        var live = pins.ToDictionary(i => i.Id);
        var membership = panel.Stacks.SelectMany(s => s.ClipIds.Select(id => (id, s))).ToDictionary(p => p.id, p => p.s);
        var seen = new HashSet<string>(); var tiles = new List<DockTile>();
        foreach (var item in pins)
        {
            if (!membership.TryGetValue(item.Id, out var stack)) tiles.Add(new(item.Id, null, new[] { item }));
            else if (seen.Add(stack.Id)) tiles.Add(new(stack.Id, stack.Id, stack.ClipIds.Where(live.ContainsKey).Select(id => live[id]).ToArray()));
        }
        return tiles.ToArray();
    }
    public static bool DetachPartial(ShelfState state, PanelSet panel, IReadOnlySet<string> moving)
    {
        var changed = false;
        var live = state.Items.Select(i => i.Id).ToHashSet();
        foreach (var stack in panel.Stacks)
            if (stack.ClipIds.Any(moving.Contains) && stack.ClipIds.Any(id => live.Contains(id) && !moving.Contains(id)))
                { stack.ClipIds.RemoveAll(moving.Contains); changed = true; }
        Prune(panel); return changed;
    }
    public static bool Combine(ShelfState state, string panelId, string targetId, IEnumerable<string> incoming)
    {
        var panel = state.Panels.SingleOrDefault(p => p.Id == panelId && p.ClosedUtc is null) ?? throw new InvalidOperationException("Panel unavailable.");
        var requested = incoming.Distinct().ToArray(); var live = state.Items.Select(i => i.Id).ToHashSet();
        if (!live.Contains(targetId) || requested.Any(id => !live.Contains(id)) || !panel.ClipIds.Contains(targetId))
            throw new InvalidOperationException("A clip is no longer available.");
        PanelSets.CaptureActive(state);
        var target = panel.Stacks.FirstOrDefault(s => s.ClipIds.Contains(targetId));
        var targetIds = target?.ClipIds.ToList() ?? new() { targetId };
        // The drop target owns the resulting stack's use behavior. Any kept
        // target member also repairs mixed stacks created before this rule.
        var records = state.Items.Concat(state.Trash.Select(t => t.Item)).ToDictionary(i => i.Id);
        var keep = targetIds.Any(id => records.TryGetValue(id, out var item) && item.KeepAfterUse);
        var moving = requested.Where(id => !targetIds.Contains(id)).ToList();
        if (moving.Count == 0) return false;
        // A whole visible source stack also carries its deleted references, so
        // later Trash restore returns them to the combined stack.
        var requestedSet = requested.ToHashSet();
        foreach (var stack in panel.Stacks.Where(s => s != target))
            if (stack.ClipIds.Any(requestedSet.Contains) && stack.ClipIds.Where(live.Contains).All(requestedSet.Contains))
                foreach (var id in stack.ClipIds) if (!moving.Contains(id) && !targetIds.Contains(id)) moving.Add(id);
        var combined = targetIds.Concat(moving).Distinct().ToList(); var ids = combined.ToHashSet();
        var anchor = panel.ClipIds.FindIndex(id => targetIds.Contains(id));
        var slot = panel.ClipIds.Take(anchor).Count(id => !ids.Contains(id));
        var order = panel.ClipIds.Where(id => !ids.Contains(id)).ToList(); order.InsertRange(slot, combined);
        foreach (var stack in panel.Stacks.Where(s => s != target)) stack.ClipIds.RemoveAll(ids.Contains);
        panel.Stacks.RemoveAll(s => s != target && s.ClipIds.Count < 2);
        if (target is null) { target = new ClipStack(); panel.Stacks.Add(target); }
        target.ClipIds = combined; panel.ClipIds = order;
        foreach (var id in combined) if (records.TryGetValue(id, out var item)) item.KeepAfterUse = keep;
        if (panelId == state.ActivePanelId) PanelSets.ApplyActive(state);
        return true;
    }
    public static bool Split(ShelfState state, string panelId, string stackId)
    {
        var panel = state.Panels.SingleOrDefault(p => p.Id == panelId && p.ClosedUtc is null) ?? throw new InvalidOperationException("Panel unavailable.");
        return panel.Stacks.RemoveAll(s => s.Id == stackId) > 0;
    }
    public static void Copy(IEnumerable<PanelSet> sources, PanelSet target)
    {
        var members = target.ClipIds.ToHashSet(); var used = new HashSet<string>(); target.Stacks.Clear();
        foreach (var stack in sources.SelectMany(p => p.Stacks).ToArray())
        {
            var ids = stack.ClipIds.Where(id => members.Contains(id) && !used.Contains(id)).ToList();
            if (ids.Count < 2) continue;
            foreach (var id in ids) used.Add(id);
            target.Stacks.Add(new ClipStack { ClipIds = ids });
        }
        Normalize(target);
    }
}
