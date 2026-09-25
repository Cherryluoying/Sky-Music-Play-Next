// 模块：SkyMusic.App 页面视图 SettingsView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    // 仅在用户点击时交由系统浏览器打开固定项目地址。
    private async void ProjectAddress_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not { } window ||
                !await window.Launcher.LaunchUriAsync(new Uri("https://github.com/Cherryluoying/Sky-Music-Play-Next")))
                (DataContext as SettingsViewModel)?.ReportLinkError();
        }
        catch (Exception) { (DataContext as SettingsViewModel)?.ReportLinkError(); }
    }

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

    // 分类目录选择与打开复用系统文件接口；取消选择不改变原设置。
    private async void SelectLibraryDirectory_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || TopLevel.GetTopLevel(this) is not { } topLevel) return;
        var midi = (sender as Control)?.Tag?.ToString() == "midi";
        try
        {
            var current = vm.GetLibraryDirectory(midi);
            var initial = Directory.Exists(current)
                ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(current) : null;
            var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = midi ? "选择 MIDI 库目录" : "选择曲谱库目录",
                AllowMultiple = false,
                SuggestedStartLocation = initial
            });
            if (selected.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
            if (midi) vm.MidiLibraryDirectory = path;
            else vm.ScoreLibraryDirectory = path;
        }
        catch (Exception ex) { vm.ReportDirectoryError(ex.Message); }
    }

    private async void OpenLibraryDirectory_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || TopLevel.GetTopLevel(this) is not { } topLevel) return;
        try
        {
            var path = vm.GetLibraryDirectory((sender as Control)?.Tag?.ToString() == "midi");
            Directory.CreateDirectory(path);
            if (!await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path)))
                vm.ReportDirectoryError("无法打开资源管理器，请检查目录是否可访问。");
        }
        catch (Exception ex) { vm.ReportDirectoryError(ex.Message); }
    }
}
