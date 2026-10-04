// 模块：SkyMusic.App 自定义控件 TrackRow.axaml
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Controls;

public sealed partial class TrackRow : UserControl
{
    public TrackRow() => InitializeComponent();

    // 歌单、喜欢、最近播放和已导入列表共用此入口；MIDI 不显示此菜单。
    private async void CustomizeCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TrackItemViewModel { CanCustomizeCover: true } item ||
            TopLevel.GetTopLevel(this) is not { DataContext: MainWindowViewModel shell } top) return;
        try
        {
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择歌曲或乐谱封面", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("封面图片") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] }]
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await shell.Playback.SetCustomCoverAsync(item, path);
        }
        catch (Exception ex) { shell.Playback.ReportCoverError(ex.Message); }
        e.Handled = true;
    }

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
