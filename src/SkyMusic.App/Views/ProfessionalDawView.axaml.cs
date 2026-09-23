// 模块：SkyMusic.App 专业半 DAW 工作区
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Windows.Input;
using SkyMusic.App.Controls;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class ProfessionalDawView : UserControl
{
    public ProfessionalDawView() => InitializeComponent();

    private ProfessionalWorkspaceViewModel? ViewModel => DataContext as ProfessionalWorkspaceViewModel;

    private void PianoRoll_OnEditRequested(object? sender, PianoRollEditEventArgs e)
    {
        if (ViewModel is null)
            return;
        switch (e.Kind)
        {
            case PianoRollEditKind.Select:
                ViewModel.SelectNote(e.NoteId);
                break;
            case PianoRollEditKind.Add:
                ViewModel.AddNote(e.StartTick, e.LengthTicks, e.MidiNote);
                break;
            case PianoRollEditKind.Move when e.NoteId is { } moveId:
                ViewModel.MoveNote(moveId, e.StartTick, e.MidiNote);
                break;
            case PianoRollEditKind.Resize when e.NoteId is { } resizeId:
                ViewModel.ResizeNote(resizeId, e.LengthTicks);
                break;
            case PianoRollEditKind.Delete when e.NoteId is { } deleteId:
                ViewModel.DeleteNote(deleteId);
                break;
        }
    }

    private void Arrangement_OnTrackSelected(object? sender, Guid trackId)
        => ViewModel?.SelectTrack(trackId);

    private void PianoScroll_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer viewer && viewer.Offset.Y < 1)
            viewer.Offset = new Vector(viewer.Offset.X, 720);
    }

    private void Workspace_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null || e.Source is TextBox or NumericUpDown)
            return;

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        ICommand? command = (control, e.Key) switch
        {
            (true, Key.Z) => ViewModel.UndoCommand,
            (true, Key.Y) => ViewModel.RedoCommand,
            (true, Key.D) => ViewModel.SelectedNote is null
                ? ViewModel.DuplicateTrackCommand
                : ViewModel.DuplicateNoteCommand,
            (false, Key.Delete) => ViewModel.DeleteNoteCommand,
            (false, Key.Up) => ViewModel.TransposeUpCommand,
            (false, Key.Down) => ViewModel.TransposeDownCommand,
            (false, Key.Space) => ViewModel.PlayPauseCommand,
            _ => null
        };
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
            return;
        }

        var tool = e.Key switch
        {
            Key.V => nameof(ProfessionalEditTool.Select),
            Key.P => nameof(ProfessionalEditTool.Draw),
            Key.E => nameof(ProfessionalEditTool.Erase),
            _ => null
        };
        if (tool is not null)
        {
            ViewModel.SelectToolCommand.Execute(tool);
            e.Handled = true;
        }
    }
}
