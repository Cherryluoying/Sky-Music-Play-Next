// 模块：SkyMusic.App 页面视图 MainWindow.axaml
using Avalonia.Controls;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private DesktopLyricsWindow? _desktopLyricsWindow;
    private WorkbenchWindow? _workbenchWindow;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Playback.DesktopLyricsVisibilityChanged -= OnDesktopLyricsVisibilityChanged;
            _viewModel.OpenWorkbenchRequested -= OnOpenWorkbenchRequested;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Playback.DesktopLyricsVisibilityChanged += OnDesktopLyricsVisibilityChanged;
            _viewModel.OpenWorkbenchRequested += OnOpenWorkbenchRequested;
        }
    }

    private void OnOpenWorkbenchRequested(object? sender, EventArgs e)
    {
        if (_workbenchWindow is not null)
        {
            _workbenchWindow.Activate();
            return;
        }

        _workbenchWindow = new WorkbenchWindow
        {
            DataContext = new WorkbenchWindowViewModel(playbackController: _viewModel?.ScorePlaybackController)
        };
        _workbenchWindow.Closed += OnWorkbenchClosed;
        _workbenchWindow.Show(this);
    }

    private void OnWorkbenchClosed(object? sender, EventArgs e)
    {
        if (_workbenchWindow is null)
            return;
        _workbenchWindow.Closed -= OnWorkbenchClosed;
        _workbenchWindow = null;
    }

    private void OnDesktopLyricsVisibilityChanged(bool isVisible)
    {
        if (isVisible)
        {
            if (_desktopLyricsWindow is not null)
            {
                return;
            }

            _desktopLyricsWindow = new DesktopLyricsWindow
            {
                DataContext = _viewModel?.Playback
            };
            _desktopLyricsWindow.Closed += OnDesktopLyricsClosed;
            _desktopLyricsWindow.Show();
            return;
        }

        _desktopLyricsWindow?.Close();
    }

    private void OnDesktopLyricsClosed(object? sender, EventArgs e)
    {
        if (_desktopLyricsWindow is not null)
        {
            _desktopLyricsWindow.Closed -= OnDesktopLyricsClosed;
            _desktopLyricsWindow = null;
        }

        _viewModel?.Playback.SetDesktopLyricsVisible(false);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Playback.DesktopLyricsVisibilityChanged -= OnDesktopLyricsVisibilityChanged;
            _viewModel.OpenWorkbenchRequested -= OnOpenWorkbenchRequested;
        }

        _desktopLyricsWindow?.Close();
        _workbenchWindow?.Close();
    }
}
