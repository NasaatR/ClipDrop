using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ClipDrop.Windows.Views;

// Small edits preserve realized containers; large filter/reorder changes emit one
// reset rather than thousands of layout/selection notifications. Rows stay cached.
internal sealed class RowCollection : ObservableCollection<ItemRow>
{
    public void Reconcile(ItemRow[] next)
    {
        if (Count == next.Length && this.SequenceEqual(next)) return;
        var mismatches = Math.Abs(Count - next.Length);
        for (var i = 0; i < Math.Min(Count, next.Length) && mismatches <= 64; i++) if (!ReferenceEquals(this[i], next[i])) mismatches++;
        if (mismatches > 64)
        {
            Items.Clear(); foreach (var row in next) Items.Add(row);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)); return;
        }
        var wanted = next.ToHashSet();
        for (var i = Count - 1; i >= 0; i--) if (!wanted.Contains(this[i])) RemoveAt(i);
        for (var i = 0; i < next.Length; i++)
        {
            if (i < Count && ReferenceEquals(this[i], next[i])) continue;
            var index = IndexOf(next[i]);
            if (index >= 0) Move(index, i); else Insert(i, next[i]);
        }
    }
}
