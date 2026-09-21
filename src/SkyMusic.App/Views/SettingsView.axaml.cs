// 模块：SkyMusic.App 页面视图 SettingsView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private async void SelectFfmpeg_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 FFmpeg 可执行文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("FFmpeg") { Patterns = ["ffmpeg.exe", "ffmpeg"] }]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            viewModel.FfmpegPath = path;
        }
    }

    private async void SelectPianoTrans_OnClick(object? sender, RoutedEventArgs e)
        => await SelectFolderAsync("选择 PianoTrans-v1.0 目录", path =>
            ((SettingsViewModel)DataContext!).PianoTransPath = path);

    private async void SelectCache_OnClick(object? sender, RoutedEventArgs e)
        => await SelectFolderAsync("选择缓存目录", path =>
            ((SettingsViewModel)DataContext!).CacheDirectory = path);

    private async Task SelectFolderAsync(string title, Action<string> setPath)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || DataContext is not SettingsViewModel)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            setPath(path);
        }
    }
}
