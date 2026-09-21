// 模块：SkyMusic.App 页面视图 ScoreEditorView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class ScoreEditorView : UserControl
{
    public ScoreEditorView() => InitializeComponent();

    private async void Import_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ScoreEditorViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入乐谱",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("乐谱") { Patterns = ["*.txt", "*.json", "*.skysheet", "*.mid", "*.midi"] }]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            await viewModel.ImportAsync(path);
        }
    }

    private async void Export_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ScoreEditorViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存乐谱",
            SuggestedFileName = "score.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Sky Studio 乐谱") { Patterns = ["*.txt"] },
                new FilePickerFileType("标准 MIDI") { Patterns = ["*.mid", "*.midi"] }
            ]
        });
        var path = file?.TryGetLocalPath();
        if (path is not null)
        {
            await viewModel.ExportAsync(path);
        }
    }
}
