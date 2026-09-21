// 模块：SkyMusic.App 页面视图 TasksView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class TasksView : UserControl
{
    public TasksView() => InitializeComponent();

    private async void ImportScore_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || DataContext is not TasksViewModel viewModel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入乐谱",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("支持的乐谱")
                {
                    Patterns = ["*.txt", "*.json", "*.skysheet", "*.mid", "*.midi"]
                },
                new FilePickerFileType("Sky Studio")
                {
                    Patterns = ["*.txt", "*.json", "*.skysheet"]
                },
                new FilePickerFileType("MIDI")
                {
                    Patterns = ["*.mid", "*.midi"]
                }
            ]
        });

        var filePath = files.FirstOrDefault()?.TryGetLocalPath();
        if (filePath is null)
        {
            return;
        }

        try
        {
            await viewModel.ImportFileAsync(filePath);
        }
        catch (Exception exception)
        {
            viewModel.SetError(exception);
        }
    }
}
