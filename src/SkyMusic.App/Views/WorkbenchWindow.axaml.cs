// 模块：SkyMusic.App 页面视图 WorkbenchWindow.axaml
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class WorkbenchWindow : Window
{
    public WorkbenchWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private WorkbenchWindowViewModel? ViewModel => DataContext as WorkbenchWindowViewModel;

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || e.Source is Button or TextBox or ComboBox or Slider)
            return;
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Minimize_OnClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_OnClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        ViewModel?.Dispose();
        Owner?.Activate();
        Owner?.Focus();
    }

    private void NewProject_OnClick(object? sender, RoutedEventArgs e)
        => ViewModel?.NewProjectCommand.Execute(null);

    private async void OpenProject_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开猫橘咪音乐工程",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("支持的工程与乐谱")
                {
                    Patterns = ["*.skymusicproj", "*.skysheet.json", "*.genshinsheet.json", "*.json", "*.mid", "*.midi"]
                },
                new FilePickerFileType("猫橘咪音乐工程") { Patterns = ["*.skymusicproj"] },
                new FilePickerFileType("游戏乐谱") { Patterns = ["*.skysheet.json", "*.genshinsheet.json", "*.json"] },
                new FilePickerFileType("MIDI") { Patterns = ["*.mid", "*.midi"] }
            ]
        });
        if (files.Count == 0 || ViewModel is null)
            return;

        await using var stream = await files[0].OpenReadAsync();
        await ViewModel.LoadAsync(stream, files[0].Name);
    }

    private async void SaveProject_OnClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存猫橘咪音乐工程",
            SuggestedFileName = $"{ViewModel?.ProjectTitle ?? "未命名工程"}.skymusicproj",
            DefaultExtension = "skymusicproj",
            FileTypeChoices =
            [
                new FilePickerFileType("猫橘咪音乐工程") { Patterns = ["*.skymusicproj"] }
            ]
        });
        if (file is null || ViewModel is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await ViewModel.SaveAsync(stream);
    }

    private async void ExportGameScore_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;
        var extension = ViewModel.Composer.Document.Profile == Core.GameScores.GameScoreProfile.Genshin
            ? "genshinsheet.json"
            : "skysheet.json";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出游戏乐谱",
            SuggestedFileName = $"{ViewModel.Composer.Document.Name}.{extension}",
            DefaultExtension = extension,
            FileTypeChoices =
            [
                new FilePickerFileType("genshin-music 乐谱")
                {
                    Patterns = ["*.skysheet.json", "*.genshinsheet.json", "*.json"]
                }
            ]
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await ViewModel.ExportGameScoreAsync(stream, legacy: false);
    }

    private async void ExportMidi_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出 MIDI",
            SuggestedFileName = $"{ViewModel.Composer.Document.Name}.mid",
            DefaultExtension = "mid",
            FileTypeChoices =
            [
                new FilePickerFileType("MIDI") { Patterns = ["*.mid", "*.midi"] }
            ]
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        ViewModel.ExportMidi(stream);
    }
}
