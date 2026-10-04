// 模块：非激活切歌提示；同一个窗口随悬浮球移动，开关悬浮球时自动切换到屏幕顶部。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SkyMusic.App.Services;

namespace SkyMusic.App.Views;

public sealed partial class NextTrackNoticeWindow : Window
{
    private readonly Func<FloatingPlayerWindow?> _floating;
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private CancellationTokenSource? _motion;
    private bool _closed;

    public NextTrackNoticeWindow() : this(() => null) { }
    public NextTrackNoticeWindow(Func<FloatingPlayerWindow?> floating)
    {
        InitializeComponent();
        _floating = floating;
        _placementTimer.Tick += (_, _) => PlaceNotice();
        Opened += (_, _) =>
        {
            if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
                new DesktopLyricsInputMode(handle.Handle).SetLocked(true);
        };
        Closed += (_, _) => { _closed = true; _motion?.Cancel(); _placementTimer.Stop(); };
    }

    // 取消旧动画后继续响应最新状态，退场完成才隐藏，避免快速切歌留下旧气泡。
    public async void SetTrack(string? title)
    {
        if (_closed) return;
        _motion?.Cancel();
        if (title is null && !IsVisible) return;
        var motion = new CancellationTokenSource();
        _motion = motion;
        var show = title is not null;
        if (show)
        {
            TrackTitle.Text = title;
            PlaceNotice();
            if (!IsVisible) Show();
            PlaceNotice();
            _placementTimer.Start();
        }
        NoticeSurface.RenderTransform = new TranslateTransform(0, show ? 0 : -10);
        try
        {
            await SurfaceMotion.SlideAsync(NoticeSurface, show ? 10 : 0, show ? 0 : -10, motion.Token);
            if (!show && !motion.IsCancellationRequested) { Hide(); _placementTimer.Stop(); }
        }
        catch (OperationCanceledException) { }
        finally { if (_motion == motion) _motion = null; motion.Dispose(); }
    }

    private void PlaceNotice()
    {
        var floating = _floating();
        var ball = floating is { IsVisible: true } ? floating.BallScreenBounds : (PixelRect?)null;
        var screen = ball is { } anchor ? Screens.ScreenFromPoint(anchor.Position) : Screens.Primary;
        screen ??= Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        Width = Math.Min(340, area.Width / scale);
        var width = (int)Math.Round(Width * scale);
        var height = (int)Math.Round(Height * scale);
        var gap = (int)Math.Round(12 * scale);
        var x = area.X + (area.Width - width) / 2;
        var y = area.Y + gap;
        if (ball is { } rect)
        {
            // 靠右显示在左侧，靠左显示在右侧；窄屏改为上下并限制在当前屏幕工作区。
            x = rect.Right + gap + width <= area.Right ? rect.Right + gap : rect.X - gap - width;
            y = rect.Y + (rect.Height - height) / 2;
            if (x < area.X) { x = rect.X; y = rect.Bottom + gap + height <= area.Bottom ? rect.Bottom + gap : rect.Y - gap - height; }
        }
        Position = new PixelPoint(Math.Clamp(x, area.X, Math.Max(area.X, area.Right - width)),
            Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - height)));
    }
}
