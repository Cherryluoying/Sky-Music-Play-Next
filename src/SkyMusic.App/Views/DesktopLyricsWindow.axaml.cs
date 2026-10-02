// 模块：桌面歌词窗口；复用现有播放命令，仅负责透明展示、悬停交互和填色时钟。
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkyMusic.App.ViewModels;
using SkyMusic.App.Services;

namespace SkyMusic.App.Views;

public sealed partial class DesktopLyricsWindow : Window
{
    private readonly DesktopLyricsAppearance _appearance;
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Stopwatch _positionClock = new();
    private PlaybackViewModel? _playback;
    private bool _isOpen;
    private bool _closed;
    private bool _dirty;
    private int _openPopovers;
    private DesktopLyricsInputMode? _inputMode;
    private DesktopLyricsUnlockWindow? _unlockWindow;

    public DesktopLyricsWindow() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SkyMusicPlay", "desktop-lyrics.json")) { }

    // 可注入独立设置路径，便于验证 UI 而不污染用户偏好。
    public DesktopLyricsWindow(string settingsPath)
    {
        InitializeComponent();
        _appearance = new DesktopLyricsAppearance(settingsPath);
        AppearancePanel.DataContext = _appearance;
        LyricText.ApplyAppearance(_appearance);
        TranslationText.FontScale = .65;
        TranslationText.ApplyAppearance(_appearance);
        ApplyTextLayout();
        _appearance.PropertyChanged += Appearance_OnChanged;
        _frameTimer.Tick += Frame_OnTick;
        _saveTimer.Tick += Save_OnTick;
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closed += OnClosed;
        PositionChanged += (_, _) => PositionUnlockWindow();
        SizeChanged += (_, _) => PositionUnlockWindow();
        if (OperatingSystem.IsWindows()) Win32Properties.AddWndProcHookCallback(this, WindowMessage);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachPlayback();
        if (_closed) return;
        _playback = DataContext as PlaybackViewModel;
        if (_playback is not null)
        {
            _playback.PropertyChanged += Playback_OnChanged;
            _playback.Lyrics.CollectionChanged += Lyrics_OnChanged;
            _playback.Queue.CollectionChanged += Queue_OnChanged;
            _playback.LyricFonts.PropertyChanged += Fonts_OnChanged;
        }
        ApplyFont();
        SynchronizeClock();
        Queue_OnChanged(null, null);
    }

    private void DetachPlayback()
    {
        if (_playback is null) return;
        _playback.PropertyChanged -= Playback_OnChanged;
        _playback.Lyrics.CollectionChanged -= Lyrics_OnChanged;
        _playback.Queue.CollectionChanged -= Queue_OnChanged;
        _playback.LyricFonts.PropertyChanged -= Fonts_OnChanged;
        _playback = null;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _isOpen = true;
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle()?.Handle is { } hwnd)
        {
            _inputMode = new DesktopLyricsInputMode(hwnd);
            ApplyLockState();
        }
        SynchronizeClock();
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;

        Width = Math.Min(Width, screen.WorkingArea.Width / screen.Scaling - 24);
        var width = (int)(Width * screen.Scaling);
        var height = (int)(Height * screen.Scaling);
        Position = new PixelPoint(
            screen.WorkingArea.X + ((screen.WorkingArea.Width - width) / 2),
            screen.WorkingArea.Bottom - height - 56);
    }

    // 关闭时释放订阅和计时器，歌词窗口不能阻止主程序退出。
    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _isOpen = false;
        _frameTimer.Stop();
        _saveTimer.Stop();
        _positionClock.Stop();
        _unlockWindow?.Close();
        _unlockWindow = null;
        if (OperatingSystem.IsWindows()) Win32Properties.RemoveWndProcHookCallback(this, WindowMessage);
        QueueButton.Flyout?.Hide();
        SettingsButton.Flyout?.Hide();
        _appearance.PropertyChanged -= Appearance_OnChanged;
        if (_dirty) _appearance.Save();
        DetachPlayback();
    }

    private void Playback_OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackViewModel.IsDesktopLyricsLocked)) ApplyLockState();
        if (e.PropertyName is nameof(PlaybackViewModel.PositionSeconds) or nameof(PlaybackViewModel.IsPlaying)
            or nameof(PlaybackViewModel.CurrentItem))
            SynchronizeClock();
        else if (e.PropertyName is nameof(PlaybackViewModel.CurrentLyricIndex) or nameof(PlaybackViewModel.DurationSeconds))
            RenderFrame();
    }

    private void Fonts_OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricFontSettings.EffectiveDesktopFont)) ApplyFont();
    }

    private void ApplyFont()
    {
        var family = _playback?.LyricFonts.EffectiveDesktopFont.Family ?? LyricFontSettings.DefaultFamily;
        LyricText.ApplyFont(family);
        TranslationText.ApplyFont(family);
    }

    private void Lyrics_OnChanged(object? sender, NotifyCollectionChangedEventArgs e) => RenderFrame();

    private void Queue_OnChanged(object? sender, NotifyCollectionChangedEventArgs? e)
        => QueueEmpty.IsVisible = _playback?.Queue.Count is not > 0;

    private void SynchronizeClock()
    {
        _positionClock.Restart();
        if (_isOpen && _playback?.IsPlaying == true) _frameTimer.Start();
        else
        {
            _frameTimer.Stop();
            _positionClock.Stop();
        }
        RenderFrame();
    }

    private void Frame_OnTick(object? sender, EventArgs e) => RenderFrame();

    // LRC 只有句级时间：在当前句起点与下一句起点之间填色，不编造逐字时间戳。
    // 快照间最多外推 250ms；卡顿、暂停和跳转均以播放器位置为准，不另起播放时钟。
    private void RenderFrame()
    {
        var playback = _playback;
        TranslationText.IsVisible = false;
        if (playback is null) { LyricText.SetFrame("猫橘咪音乐", 0); return; }
        var index = playback.CurrentLyricIndex;
        if (index < 0 || index >= playback.Lyrics.Count)
        {
            LyricText.SetFrame(playback.HasLyrics ? "♪" : "暂无歌词", 0);
            return;
        }

        var line = playback.Lyrics[index];
        var start = line.Line.Timestamp.TotalSeconds;
        var end = playback.DurationSeconds;
        for (var next = index + 1; next < playback.Lyrics.Count; next++)
        {
            var timestamp = playback.Lyrics[next].Line.Timestamp.TotalSeconds;
            if (timestamp > start) { end = timestamp; break; }
        }
        var position = playback.PositionSeconds + (playback.IsPlaying ? Math.Min(.25, _positionClock.Elapsed.TotalSeconds) : 0);
        LyricText.SetFrame(line.Text, end > start ? (position - start) / (end - start) : 0);
        TranslationText.IsVisible = line.HasTranslation;
        if (line.HasTranslation) TranslationText.SetFrame(line.Translation, end > start ? (position - start) / (end - start) : 0);
    }

    private void Appearance_OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopLyricsAppearance.SaveError)) return;
        LyricText.ApplyAppearance(_appearance);
        TranslationText.ApplyAppearance(_appearance);
        ApplyTextLayout();
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Save_OnTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        _appearance.Save();
        _dirty = false;
    }

    // 为字号和译文间距预留实际高度，避免调大后被固定行高再次缩小。
    private void ApplyTextLayout()
    {
        LyricText.MinHeight = _appearance.FontSize * 1.4 + _appearance.OutlineWidth * 2;
        TranslationText.Height = _appearance.FontSize * .65 * 1.4 + _appearance.OutlineWidth * 2;
        TranslationText.Margin = new Thickness(8, _appearance.TranslationSpacing, 8, 0);
        Height = Math.Max(156, LyricText.MinHeight + TranslationText.Height + _appearance.TranslationSpacing + 66);
    }

    private void Surface_OnPointerEntered(object? sender, PointerEventArgs e) => SetControlsVisible(true);
    private void Surface_OnPointerExited(object? sender, PointerEventArgs e) => UpdateControlsVisibility();
    private void Popover_OnOpened(object? sender, EventArgs e)
    {
        _openPopovers++;
        SetControlsVisible(true);
    }
    private void Popover_OnClosed(object? sender, EventArgs e)
    {
        _openPopovers = Math.Max(0, _openPopovers - 1);
        UpdateControlsVisibility();
    }

    private void UpdateControlsVisibility() => SetControlsVisible(DesktopSurface.IsPointerOver || _openPopovers > 0);

    private void SetControlsVisible(bool visible)
    {
        visible &= _playback?.IsDesktopLyricsLocked != true;
        HoverControls.Opacity = visible ? 1 : 0;
        HoverControls.IsHitTestVisible = visible;
    }

    // 穿透后正文窗口不能接收解锁点击；独立热区与主播放器共用同一个解锁命令。
    private void ApplyLockState()
    {
        if (_inputMode is null) return;
        try
        {
            _inputMode.SetLocked(_playback?.IsDesktopLyricsLocked == true);
            if (_playback?.IsDesktopLyricsLocked == true)
            {
                QueueButton.Flyout?.Hide();
                SettingsButton.Flyout?.Hide();
                FocusManager?.Focus(null);
                if (_isOpen && _unlockWindow is null)
                {
                    _unlockWindow = new DesktopLyricsUnlockWindow();
                    _unlockWindow.UnlockRequested += (_, _) => _playback?.ToggleDesktopLyricsLockCommand.Execute(null);
                    PositionUnlockWindow();
                    _unlockWindow.Show(this);
                }
            }
            else { _unlockWindow?.Close(); _unlockWindow = null; }
            UpdateControlsVisibility();
        }
        catch (Win32Exception ex)
        {
            Trace.TraceError($"桌面歌词输入模式切换失败：{ex.Message}");
            if (_playback is not null && _playback.IsDesktopLyricsLocked) _playback.IsDesktopLyricsLocked = false;
        }
    }

    // 解锁按钮停在歌词窗口右下方的控制区，不覆盖文字，跨屏移动与字号调整时跟随。
    private void PositionUnlockWindow()
    {
        if (_unlockWindow is null) return;
        var point = this.PointToScreen(new Point(Math.Max(0, Bounds.Width - 54), Math.Max(0, Bounds.Height - 48)));
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            var size = (int)Math.Ceiling(44 * screen.Scaling);
            point = new PixelPoint(Math.Clamp(point.X, screen.WorkingArea.X, Math.Max(screen.WorkingArea.X, screen.WorkingArea.Right - size)),
                Math.Clamp(point.Y, screen.WorkingArea.Y, Math.Max(screen.WorkingArea.Y, screen.WorkingArea.Bottom - size)));
        }
        _unlockWindow.Position = point;
    }

    private nint WindowMessage(nint hwnd, uint message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021 && _playback?.IsDesktopLyricsLocked == true)
        { handled = true; return 3; } // WM_MOUSEACTIVATE：锁定时不能抢游戏焦点。
        return 0;
    }

    private void Surface_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            e.Source is Button || e.Source is Visual visual && visual.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        BeginMoveDrag(e);
    }

    // 等按钮完成命令分发后再收起气泡，避免弹出层卸载影响当前点击。
    private void QueueTrack_OnClick(object? sender, RoutedEventArgs e)
        => Dispatcher.UIThread.Post(() => QueueButton.Flyout?.Hide(), DispatcherPriority.Background);

    private void RemoveQueueTrack_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TrackItemViewModel item }) _playback?.RemoveFromQueue(item);
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();
}
