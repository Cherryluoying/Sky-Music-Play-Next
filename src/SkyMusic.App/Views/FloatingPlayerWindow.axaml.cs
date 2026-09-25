// 模块：悬浮球窗口管理；一个窗口内展开气泡，拖动与单击分离，关闭后取消异步读取。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SkyMusic.App.ViewModels;
using SkyMusic.App.Services;

namespace SkyMusic.App.Views;

public sealed partial class FloatingPlayerWindow : Window
{
    private readonly FloatingPlayerViewModel _viewModel = null!;
    private PixelPoint? _pressPoint;
    private PixelPoint _dragOrigin;
    private bool _dragging;
    private bool _closed;
    private bool _searchInput;
    private int _sectionRequest;
    private Point _ballOffset;
    public bool IsExpanded => Bubble.IsVisible;

    // 无参构造仅供 Avalonia 资源加载与设计器，运行时由主窗口注入共享播放状态。
    public FloatingPlayerWindow() => InitializeComponent();

    public FloatingPlayerWindow(FloatingPlayerViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        Win32Properties.AddWndProcHookCallback(this, WindowMessage);
        Opened += (_, _) =>
        {
            if (Screens.ScreenFromWindow(this) is not { } screen) return;
            Position = new PixelPoint(screen.WorkingArea.Right - (int)(88 * screen.Scaling),
                screen.WorkingArea.Y + (int)(screen.WorkingArea.Height * .6));
        };
        Closed += (_, _) =>
        {
            _closed = true;
            Win32Properties.RemoveWndProcHookCallback(this, WindowMessage);
            _viewModel.Dispose();
        };
    }

    // 普通鼠标控制不激活窗口，防止 SendInput 演奏的音符发到悬浮窗；只有搜索主动获取焦点。
    private nint WindowMessage(nint hwnd, uint message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021 && !_searchInput) // WM_MOUSEACTIVATE / MA_NOACTIVATE
        {
            handled = true;
            return 3;
        }
        return 0;
    }

    public async Task ToggleBubbleAsync()
    {
        if (IsExpanded) { Collapse(); return; }
        SetExpanded(true);
        await _viewModel.RefreshAsync();
    }

    private void SetExpanded(bool expanded)
    {
        if (_closed || IsExpanded == expanded) return;
        PlaceAtBall(GetBallPosition(), expanded);
    }

    private PixelPoint GetBallPosition() => new(Position.X + (int)Math.Round(_ballOffset.X * RenderScaling),
        Position.Y + (int)Math.Round(_ballOffset.Y * RenderScaling));

    // 只移动面板相对于猫爪的偏移；猫爪位置不会因靠边翻转而跳动。
    private void PlaceAtBall(PixelPoint anchor, bool expanded)
    {
        var screen = Screens.ScreenFromPoint(anchor) ?? Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var layout = FloatingWindowPlacement.Calculate(screen.WorkingArea, screen.Scaling, anchor, expanded);
        Width = layout.Size.Width;
        Height = layout.Size.Height;
        _ballOffset = layout.Ball.Position;
        FloatingBall.Margin = new Thickness(layout.Ball.X, layout.Ball.Y, 0, 0);
        FloatingBall.Width = layout.Ball.Width;
        FloatingBall.Height = layout.Ball.Height;
        Bubble.Margin = new Thickness(layout.Bubble.X, layout.Bubble.Y, 0, 0);
        Bubble.Width = layout.Bubble.Width;
        Bubble.Height = layout.Bubble.Height;
        Bubble.IsVisible = expanded;
        Position = layout.Position;
    }

    public void Collapse()
    {
        _searchInput = false;
        SetExpanded(false);
    }

    private async void Section_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section }) return;
        var request = ++_sectionRequest;
        if (!await _viewModel.SelectSectionAsync(section)) return;
        if (_closed || !IsExpanded || request != _sectionRequest) return;
        _searchInput = _viewModel.IsSearch;
        if (_searchInput) { Activate(); SearchBox.Focus(); }
    }

    // 搜索后恢复演奏再点击输入框，也必须重新暂停，不能让音符写入查询文本。
    private async void SearchBox_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_searchInput) return;
        e.Handled = true;
        if (!await _viewModel.SelectSectionAsync("search")) return;
        if (!_closed && IsExpanded && _viewModel.IsSearch)
        {
            _searchInput = true;
            Activate();
            SearchBox.Focus();
        }
    }

    private void Track_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: TrackItemViewModel track }) return;
        _searchInput = false;
        track.PlayCommand.Execute(null);
    }

    private void Transport_OnClick(object? sender, RoutedEventArgs e)
    {
        _searchInput = false;
        var playback = _viewModel.Playback;
        var command = (sender as Control)?.Tag?.ToString() switch
        {
            "previous" => playback.PreviousCommand,
            "next" => playback.NextCommand,
            _ => playback.TogglePlayCommand
        };
        if (command.CanExecute(null)) command.Execute(null);
    }

    private void Collapse_OnClick(object? sender, RoutedEventArgs e) => Collapse();

    private void Ball_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressPoint = this.PointToScreen(e.GetPosition(this));
        _dragOrigin = GetBallPosition();
        _dragging = false;
        e.Pointer.Capture(FloatingBall);
        e.Handled = true;
    }

    private void Ball_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressPoint is not { } pressed) return;
        var current = this.PointToScreen(e.GetPosition(this));
        var delta = current - pressed;
        if (!_dragging && Math.Abs(delta.X) + Math.Abs(delta.Y) < 6 * RenderScaling) return;
        _dragging = true;
        PlaceAtBall(_dragOrigin + delta, IsExpanded);
    }

    private async void Ball_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressPoint is null) return;
        var wasDragging = _dragging;
        _pressPoint = null;
        e.Pointer.Capture(null);
        if (wasDragging) PlaceAtBall(GetBallPosition(), IsExpanded);
        else await ToggleBubbleAsync();
        e.Handled = true;
    }

    private void Ball_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _pressPoint = null;
}
