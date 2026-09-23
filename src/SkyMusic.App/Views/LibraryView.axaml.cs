// 模块：SkyMusic.App 页面视图 LibraryView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

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
                }
            ]
        });
        var paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray();
        if (paths.Length > 0)
        {
            await viewModel.ImportFilesAsync(paths);
        }
    }
}
