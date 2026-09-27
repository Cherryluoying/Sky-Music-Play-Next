// 模块：SkyMusic.App 页面视图 LibraryView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

    // 选择整个混合目录，递归索引音频、MIDI、曲谱及其封面；取消不改变曲库。
    private async void ImportFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel vm || vm.IsImporting || TopLevel.GetTopLevel(this) is not { } topLevel) return;
        try
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "导入包含音乐、MIDI、乐谱的文件夹（含子目录）",
                AllowMultiple = true
            });
            var paths = folders.Select(folder => folder.TryGetLocalPath()).OfType<string>().ToArray();
            if (paths.Length > 0) await vm.ImportDirectoriesAsync(paths);
        }
        catch (Exception exception) { vm.ReportImportError(exception.Message); }
    }

    private async void ImportMedia_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入歌曲、MIDI 或乐谱",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("猫橘咪音乐媒体")
                {
                    Patterns = ["*.txt", "*.json", "*.skysheet", "*.mid", "*.midi", "*.mp3", "*.wav", "*.flac", "*.m4a", "*.aac", "*.ogg"]
                },
                new FilePickerFileType("乐谱") { Patterns = ["*.txt", "*.json", "*.skysheet"] },
                new FilePickerFileType("音乐") { Patterns = ["*.mp3", "*.wav", "*.flac", "*.m4a", "*.aac", "*.ogg"] },
                new FilePickerFileType("MIDI") { Patterns = ["*.mid", "*.midi"] }
            ]
        });
        var paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray();
        if (paths.Length > 0)
        {
            await viewModel.ImportFilesAsync(paths);
        }
    }
}
