// 模块：SkyMusic.App 页面视图 TranscriptionView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class TranscriptionView : UserControl
{
    public TranscriptionView() => InitializeComponent();

    private async void SelectAudio_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || DataContext is not TranscriptionViewModel viewModel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择钢琴音频",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("音频文件")
                {
                    Patterns = ["*.wav", "*.mp3", "*.flac", "*.m4a", "*.ogg", "*.aac", "*.wma", "*.mp4"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            await viewModel.TranscribeAsync(path);
        }
    }
}
