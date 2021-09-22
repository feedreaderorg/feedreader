using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FeedReader.WebClient
{
    public class RangeEnabledObservableCollection<T> : ObservableCollection<T>
    {
        public void AddRange(IEnumerable<T> items, Comparison<T> comparison)
        {
            using (BlockReentrancy())
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }
                ArrayList.Adapter((IList)Items).Sort(new ComparisonComparer<T>(comparison));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items));
            }
        }
    }

    public class ComparisonComparer<T> : IComparer<T>, IComparer
    {
        readonly Comparison<T> comparison;

        public ComparisonComparer(Comparison<T> c)
        {
            comparison = c;
        }

        public int Compare(T x, T y)
        {
            return comparison(x, y);
        }

        public int Compare(object o1, object o2)
        {
            return comparison((T)o1, (T)o2);
        }
    }
}
