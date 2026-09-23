// 模块：SkyMusic.App 自定义控件 TrackRow.axaml
using Avalonia.Controls;
using Avalonia.Input;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Controls;

public sealed partial class TrackRow : UserControl
{
    public TrackRow() => InitializeComponent();

    // 媒体列表保留单击聚焦语义，双击时才进入统一播放链路。
    private void Track_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not TrackItemViewModel track)
        {
            return;
        }

        track.PlayCommand.Execute(null);
        e.Handled = true;
    }
}
