// 模块：SkyMusic.App 页面视图 PlayerView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class PlayerView : UserControl
{
    public PlayerView() => InitializeComponent();

    // 列宽只由窗口尺寸决定，长歌词或手动滚动不会重新挤压封面与中间留白。
    private void LyricsColumns_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid || e.NewSize.Width <= 0) return;
        var gap = Math.Clamp(e.NewSize.Width * 0.06, 28, 72);
        var available = Math.Max(0, e.NewSize.Width - gap);
        grid.ColumnDefinitions[0].Width = new GridLength(available * 0.46);
        grid.ColumnDefinitions[1].Width = new GridLength(gap);
        grid.ColumnDefinitions[2].Width = new GridLength(available * 0.54);
    }

    // 从沉浸页选择 LRC/TXT，解析与持久化交给播放状态层处理。
    private async void ImportLyrics_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PlaybackViewModel playback || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入本地歌词",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("歌词文件") { Patterns = ["*.lrc", "*.txt"] }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await playback.ImportLocalLyricsAsync(path);
        }
    }
}
