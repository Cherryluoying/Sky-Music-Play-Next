// 模块：SkyMusic.App 页面视图 MacroRunnerView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class MacroRunnerView : UserControl
{
    public MacroRunnerView() => InitializeComponent();

    private async void ImportMacro_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || DataContext is not MacroRunnerViewModel viewModel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入宏脚本",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON 宏脚本") { Patterns = ["*.json", "*.txt.json"] }]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        try
        {
            await viewModel.ImportAsync(path);
        }
        catch (Exception exception)
        {
            viewModel.SetError(exception);
        }
    }
}
