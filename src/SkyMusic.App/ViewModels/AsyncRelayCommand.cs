// 模块：SkyMusic.App 界面状态 AsyncRelayCommand
using System.Windows.Input;

namespace SkyMusic.App.ViewModels;

public sealed class AsyncRelayCommand(
    Func<object?, Task> execute,
    Func<object?, bool>? canExecute = null,
    Action<Exception>? onError = null) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        NotifyCanExecuteChanged();
        try
        {
            await execute(parameter);
        }
        catch (Exception exception)
        {
            onError?.Invoke(exception);
        }
        finally
        {
            _isRunning = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
