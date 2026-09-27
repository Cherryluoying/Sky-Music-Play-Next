// 模块：SkyMusic.App 页面视图 MainWindow.axaml
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using System.ComponentModel;
using Avalonia.VisualTree;
using Avalonia.Threading;
using SkyMusic.App.Controls;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private DesktopLyricsWindow? _desktopLyricsWindow;
    private WorkbenchWindow? _workbenchWindow;
    private FloatingPlayerWindow? _floatingWindow;
    public static readonly StyledProperty<bool> IsLyricsFullScreenProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(IsLyricsFullScreen));
    public bool IsLyricsFullScreen { get => GetValue(IsLyricsFullScreenProperty); private set => SetValue(IsLyricsFullScreenProperty, value); }
    public static readonly StyledProperty<string> LyricsFullScreenGlyphProperty =
        AvaloniaProperty.Register<MainWindow, string>(nameof(LyricsFullScreenGlyph), "\uE740");
    public string LyricsFullScreenGlyph { get => GetValue(LyricsFullScreenGlyphProperty); private set => SetValue(LyricsFullScreenGlyphProperty, value); }
    private WindowState _beforeFullScreen;
    private PixelPoint _beforeFullScreenPosition;
    private Size _beforeFullScreenSize;
    private CornerRadius _beforeFullScreenCorner;
    private bool _pointerAtPlayer;
    private IPointer? _fullscreenPointer;
    private readonly DispatcherTimer _hidePlayerTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
        Opened += (_, _) => UpdateFloatingWindow();
        AddHandler(KeyDownEvent, FullScreen_OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, FullScreen_OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        PointerExited += (_, _) => { if (IsLyricsFullScreen) { _pointerAtPlayer = false; _hidePlayerTimer.Start(); } };
        _hidePlayerTimer.Tick += (_, _) =>
        {
            // 滑块拖动和气泡操作期间保留播放器，避免控制区消失中断交互。
            if (_pointerAtPlayer || _fullscreenPointer?.Captured is not null ||
                MiniPlayer.GetVisualDescendants().OfType<Button>().Any(button => button.Flyout?.IsOpen == true)) return;
            MiniPlayer.IsVisible = !IsLyricsFullScreen;
            _hidePlayerTimer.Stop();
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && IsLyricsFullScreen && WindowState != WindowState.FullScreen)
                ExitLyricsFullScreen();
        };
    }

    // 使用原生全屏状态覆盖任务栏；保留原来的普通/最大化状态，不创建第二套播放器。
    public void ToggleLyricsFullScreen()
    {
        if (IsLyricsFullScreen) { ExitLyricsFullScreen(); return; }
        if (_viewModel?.Playback is not { HasTrack: true, ShowsLyrics: true }) return;
        _viewModel.Playback.OpenPlayerCommand.Execute(null);
        _beforeFullScreen = WindowState;
        _beforeFullScreenPosition = Position;
        _beforeFullScreenSize = ClientSize;
        _beforeFullScreenCorner = ShellBorder.CornerRadius;
        IsLyricsFullScreen = true;
        LyricsFullScreenGlyph = "\uE73F";
        TitleChrome.IsVisible = false;
        ShellBorder.CornerRadius = default;
        foreach (var grip in this.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("resize-grip"))) grip.IsVisible = false;
        _pointerAtPlayer = false;
        MiniPlayer.IsVisible = false;
        WindowState = WindowState.FullScreen;
    }

    private void ExitLyricsFullScreen()
    {
        if (!IsLyricsFullScreen) return;
        IsLyricsFullScreen = false;
        LyricsFullScreenGlyph = "\uE740";
        _hidePlayerTimer.Stop();
        _fullscreenPointer = null;
        TitleChrome.IsVisible = true;
        MiniPlayer.IsVisible = true;
        ShellBorder.CornerRadius = _beforeFullScreenCorner;
        foreach (var grip in this.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("resize-grip"))) grip.IsVisible = true;
        WindowState = _beforeFullScreen;
        if (_beforeFullScreen == WindowState.Normal)
        {
            Width = _beforeFullScreenSize.Width;
            Height = _beforeFullScreenSize.Height;
            Position = _beforeFullScreenPosition;
        }
    }

    private void FullScreen_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsLyricsFullScreen && e.Key == Key.Escape) { ExitLyricsFullScreen(); e.Handled = true; }
    }

    private void FullScreen_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsLyricsFullScreen) return;
        _fullscreenPointer = e.Pointer;
        var point = e.GetPosition(this);
        _pointerAtPlayer = point.Y >= ClientSize.Height - 104 && point.Y <= ClientSize.Height;
        if (_pointerAtPlayer) { _hidePlayerTimer.Stop(); MiniPlayer.IsVisible = true; }
        else if (MiniPlayer.IsVisible && !_hidePlayerTimer.IsEnabled) _hidePlayerTimer.Start();
    }

    private void FullScreenContext_OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsLyricsFullScreen && (_viewModel?.IsPlayerVisible != true || _viewModel.Playback.ShowsLyrics != true)) ExitLyricsFullScreen();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= FullScreenContext_OnChanged;
            _viewModel.Playback.PropertyChanged -= FullScreenContext_OnChanged;
            _viewModel.Playback.DesktopLyricsVisibilityChanged -= OnDesktopLyricsVisibilityChanged;
            _viewModel.OpenWorkbenchRequested -= OnOpenWorkbenchRequested;
            _viewModel.Settings.PropertyChanged -= OnSettingsChanged;
        }

        CloseFloatingWindow();
        ExitLyricsFullScreen();
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += FullScreenContext_OnChanged;
            _viewModel.Playback.PropertyChanged += FullScreenContext_OnChanged;
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
        _hidePlayerTimer.Stop();
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= FullScreenContext_OnChanged;
            _viewModel.Playback.PropertyChanged -= FullScreenContext_OnChanged;
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
