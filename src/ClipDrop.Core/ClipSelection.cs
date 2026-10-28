using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

// Per-view, transient selection. It never changes content or persisted clip flags.
public sealed class ClipSelection
{
    private readonly HashSet<string> _ids = new();
    private string? _anchor;
    public IReadOnlySet<string> Ids => _ids;
    public void Clear() { _ids.Clear(); _anchor = null; }
    public void Single(string id) { Clear(); _ids.Add(id); _anchor = id; }
    public void Click(IReadOnlyList<string> order, string id, bool control, bool shift, bool toggle)
    {
        if (!order.Contains(id)) return;
        var anchor = _anchor is not null ? order.ToList().IndexOf(_anchor) : -1;
        if (shift && anchor >= 0)
        {
            var end = order.ToList().IndexOf(id);
            if (!control) _ids.Clear();
            for (var i = System.Math.Min(anchor, end); i <= System.Math.Max(anchor, end); i++) _ids.Add(order[i]);
        }
        else
        {
            if (!control && !toggle) _ids.Clear();
            if (!_ids.Add(id)) _ids.Remove(id);
            _anchor = id;
        }
    }
    public void Trim(IEnumerable<string> remaining) { _ids.IntersectWith(remaining); if (_anchor is not null && !_ids.Contains(_anchor)) _anchor = null; }
}
