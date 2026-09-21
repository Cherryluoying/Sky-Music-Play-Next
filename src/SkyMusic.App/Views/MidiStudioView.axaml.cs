// 模块：SkyMusic.App 页面视图 MidiStudioView.axaml
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.Controls;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class MidiStudioView : UserControl
{
    private readonly PianoKeyboardControl? _keyboard;
    private MidiStudioViewModel? _viewModel;

    public MidiStudioView()
    {
        InitializeComponent();
        _keyboard = this.FindControl<PianoKeyboardControl>("PianoKeyboard");
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ActiveNotesChanged -= OnActiveNotesChanged;
        }

        _viewModel = DataContext as MidiStudioViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ActiveNotesChanged += OnActiveNotesChanged;
        }
    }

    private void OnActiveNotesChanged(IReadOnlySet<int> notes) => _keyboard?.SetActiveNotes(notes);

    private async void ExportRecording_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出演奏",
            SuggestedFileName = "midi-recording.mid",
            DefaultExtension = "mid",
            FileTypeChoices =
            [
                new FilePickerFileType("标准 MIDI") { Patterns = ["*.mid", "*.midi"] },
                new FilePickerFileType("Sky Studio 乐谱") { Patterns = ["*.txt"] }
            ]
        });
        var path = file?.TryGetLocalPath();
        if (path is not null)
        {
            await _viewModel.ExportRecordingAsync(path);
        }
    }
}
