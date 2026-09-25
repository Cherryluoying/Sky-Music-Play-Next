// 模块：SkyMusic.App 页面视图 MainWindow.axaml
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using System.ComponentModel;
using Avalonia.VisualTree;
using SkyMusic.App.Controls;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private DesktopLyricsWindow? _desktopLyricsWindow;
    private WorkbenchWindow? _workbenchWindow;
    private FloatingPlayerWindow? _floatingWindow;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
        Opened += (_, _) => UpdateFloatingWindow();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Playback.DesktopLyricsVisibilityChanged -= OnDesktopLyricsVisibilityChanged;
            _viewModel.OpenWorkbenchRequested -= OnOpenWorkbenchRequested;
            _viewModel.Settings.PropertyChanged -= OnSettingsChanged;
        }

        CloseFloatingWindow();
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Playback.DesktopLyricsVisibilityChanged += OnDesktopLyricsVisibilityChanged;
            _viewModel.OpenWorkbenchRequested += OnOpenWorkbenchRequested;
            _viewModel.Settings.PropertyChanged += OnSettingsChanged;
        }
        if (IsVisible) UpdateFloatingWindow();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.FloatingWindowEnabled)) UpdateFloatingWindow();
    }

    private void UpdateFloatingWindow()
    {
        if (_viewModel?.Settings.FloatingWindowEnabled != true) { CloseFloatingWindow(); return; }
        if (_floatingWindow is not null) return;
        _floatingWindow = new FloatingPlayerWindow(_viewModel.CreateFloatingPlayer());
        _floatingWindow.Closed += OnFloatingWindowClosed;
        _floatingWindow.Show();
    }

    private void OnFloatingWindowClosed(object? sender, EventArgs e)
    {
        _floatingWindow = null;
        if (_viewModel is not null) _viewModel.Settings.FloatingWindowEnabled = false;
    }

    // 关闭主程序时只释放窗口，不把用户的“下次启动仍开启”偏好改成关闭。
    private void CloseFloatingWindow()
    {
        if (_floatingWindow is null) return;
        _floatingWindow.Closed -= OnFloatingWindowClosed;
        _floatingWindow.Close();
        _floatingWindow = null;
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // TextPresenter 等模板子元素也属于搜索框，不能把编辑点击交给窗口拖动。
        if (e.Source is Visual visual && (visual is SkySearchBox ||
            visual.FindAncestorOfType<SkySearchBox>() is not null)) return;
        if (e.Handled || e.Source is Button or TextBox or ComboBox or Slider)
            return;
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Resize_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border grip || grip.Tag is not string edgeName ||
            e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        var edge = edgeName switch
        {
            "Left" => WindowEdge.West,
            "Right" => WindowEdge.East,
            "Top" => WindowEdge.North,
            "Bottom" => WindowEdge.South,
            "TopLeft" => WindowEdge.NorthWest,
            "TopRight" => WindowEdge.NorthEast,
            "BottomLeft" => WindowEdge.SouthWest,
            "BottomRight" => WindowEdge.SouthEast,
            _ => (WindowEdge?)null
        };
        if (edge is not null)
        {
            BeginResizeDrag(edge.Value, e);
            e.Handled = true;
        }
    }

    // 全局搜索结果与歌单使用一致的双击播放交互。
    private void GlobalSearchResult_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: TrackItemViewModel track })
        {
            return;
        }

        track.PlayCommand.Execute(null);
        e.Handled = true;
    }

    private void Minimize_OnClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_OnClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();

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
        if (IsVisible)
        {
            Activate();
            Focus();
        }
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
            _viewModel.Settings.PropertyChanged -= OnSettingsChanged;
        }

        CloseFloatingWindow();
        _desktopLyricsWindow?.Close();
        _workbenchWindow?.Close();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                desktop.Shutdown();
            }
            catch (InvalidOperationException)
            {
                // The desktop lifetime may already be shutting down after the main window closed.
            }
        }
    }
}
