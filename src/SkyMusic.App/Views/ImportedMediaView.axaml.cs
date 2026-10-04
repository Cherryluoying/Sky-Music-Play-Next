// 模块：已导入页面的文件选择；所有导入与目录扫描复用本地歌单服务，不另建数据库。
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class ImportedMediaView : UserControl
{
    public ImportedMediaView() => InitializeComponent();

    private async void ImportFiles_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportedMediaViewModel vm || vm.Library.IsImporting || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "导入音乐、乐谱或 MIDI", AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("本地媒体") { Patterns = vm.Library.SupportedExtensions.Select(extension => "*" + extension).ToArray() }]
            });
            var paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray();
            if (paths.Length > 0) await vm.Library.ImportFilesAsync(paths);
        }
        catch (Exception ex) { vm.Library.ReportImportError(ex.Message); }
    }

    private async void ImportFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportedMediaViewModel vm || vm.Library.IsImporting || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            { Title = "导入包含音乐、乐谱和 MIDI 的文件夹", AllowMultiple = true });
            var paths = folders.Select(folder => folder.TryGetLocalPath()).OfType<string>().ToArray();
            if (paths.Length > 0) await vm.Library.ImportDirectoriesAsync(paths);
        }
        catch (Exception ex) { vm.Library.ReportImportError(ex.Message); }
    }
}
