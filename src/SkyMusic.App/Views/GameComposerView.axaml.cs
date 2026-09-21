// 模块：SkyMusic.App 页面视图 GameComposerView.axaml
using Avalonia.Controls;
using Avalonia.Input;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class GameComposerView : UserControl
{
    public GameComposerView() => InitializeComponent();

    private void Composer_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not GameComposerViewModel viewModel || e.Source is TextBox)
            return;

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var command = (control, e.Key) switch
        {
            (true, Key.Z) => viewModel.UndoCommand,
            (true, Key.Y) => viewModel.RedoCommand,
            (true, Key.C) => viewModel.CopyCommand,
            (true, Key.V) => viewModel.MergePasteCommand,
            (false, Key.Delete) => viewModel.EraseCommand,
            (false, Key.Up) => viewModel.MoveUpCommand,
            (false, Key.Down) => viewModel.MoveDownCommand,
            (false, Key.B) => viewModel.ToggleBreakpointCommand,
            _ => null
        };
        if (command?.CanExecute(null) != true)
            return;
        command.Execute(null);
        e.Handled = true;
    }
}
