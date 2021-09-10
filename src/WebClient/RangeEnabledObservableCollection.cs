using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FeedReader.WebClient
{
    public class RangeEnabledObservableCollection<T> : ObservableCollection<T>
    {
        public void AddRange(IEnumerable<T> items)
        {
            using (BlockReentrancy())
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items));
            }
        }
    }
}
