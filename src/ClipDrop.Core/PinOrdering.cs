using System;
using System.Linq;
using System.Collections.Generic;

namespace ClipDrop.Core;

public static class PinOrdering
{
    // Preserve the old visible order once; subsequent saves and re-pins append.
    public static void Migrate(ShelfState state)
    {
        long order = 0;
        foreach (var item in state.Items.Concat(state.Trash.Select(t => t.Item)).OrderByDescending(i => i.CreatedUtc))
            item.PinOrder = ++order;
    }
    public static bool Move(ShelfState state, IEnumerable<string> ids, int insertionIndex)
    {
        var requested = ids.Distinct().ToArray(); var selected = requested.ToHashSet();
        var pins = state.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).ToList();
        var moving = pins.Where(i => selected.Contains(i.Id)).Concat(requested.Select(id => state.Items.FirstOrDefault(i => i.Id == id && !i.IsPinned)).OfType<ShelfItem>()).ToArray();
        if (moving.Length == 0) return false;
        var index = Math.Clamp(insertionIndex, 0, pins.Count);
        var adjusted = index - pins.Take(index).Count(i => selected.Contains(i.Id));
        var result = pins.Where(i => !selected.Contains(i.Id)).ToList(); result.InsertRange(adjusted, moving);
        if (pins.Select(i => i.Id).SequenceEqual(result.Select(i => i.Id))) return false;
        var next = Next(state);
        foreach (var item in result) { item.IsPinned = true; item.PinOrder = next++; }
        return true;
    }
    public static long Next(ShelfState state) => state.Items.Concat(state.Trash.Select(t => t.Item)).Select(i => i.PinOrder).DefaultIfEmpty().Max() + 1;
}
