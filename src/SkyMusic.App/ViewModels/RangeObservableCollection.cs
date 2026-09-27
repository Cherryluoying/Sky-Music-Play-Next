// 模块：界面批量集合；筛选大量媒体时只发送一次 Reset，避免逐条刷新造成悬浮窗卡顿。
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SkyMusic.App.ViewModels;

public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceRange(IEnumerable<T> values)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var value in values) Items.Add(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
