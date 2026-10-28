using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public sealed class ClipDrag : IDisposable
{
    internal const string TokenFormat = "NasaatR.ClipDrop.DragToken";
    internal string Token { get; } = Guid.NewGuid().ToString("N");
    private readonly ShelfSession _session;
    public ShelfItem[] Items { get; }
    public DataObject Data { get; }
    public bool InternalHandled { get; set; }
    internal ClipDrag(ShelfSession session, IReadOnlyList<ShelfItem> items)
    {
        _session = session; Items = items.ToArray(); Data = session.Transfer.BuildDrag(Items);
        Data.SetData(TokenFormat, Token, false);
    }
    public void Dispose() => _session.EndDrag(this);
}
