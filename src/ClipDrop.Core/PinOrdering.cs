using System;
using System.Linq;

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
    public static long Next(ShelfState state) => state.Items.Concat(state.Trash.Select(t => t.Item)).Select(i => i.PinOrder).DefaultIfEmpty().Max() + 1;
}
