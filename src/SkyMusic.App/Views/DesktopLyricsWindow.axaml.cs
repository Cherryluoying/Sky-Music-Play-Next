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
        _appearance.PropertyChanged += Appearance_OnChanged;
        _frameTimer.Tick += Frame_OnTick;
        _saveTimer.Tick += Save_OnTick;
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closed += OnClosed;
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
        }
        SynchronizeClock();
        Queue_OnChanged(null, null);
    }

    private void DetachPlayback()
    {
        if (_playback is null) return;
        _playback.PropertyChanged -= Playback_OnChanged;
        _playback.Lyrics.CollectionChanged -= Lyrics_OnChanged;
        _playback.Queue.CollectionChanged -= Queue_OnChanged;
        _playback = null;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _isOpen = true;
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
        QueueButton.Flyout?.Hide();
        SettingsButton.Flyout?.Hide();
        _appearance.PropertyChanged -= Appearance_OnChanged;
        if (_dirty) _appearance.Save();
        DetachPlayback();
    }

    private void Playback_OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlaybackViewModel.PositionSeconds) or nameof(PlaybackViewModel.IsPlaying)
            or nameof(PlaybackViewModel.CurrentItem))
            SynchronizeClock();
        else if (e.PropertyName is nameof(PlaybackViewModel.CurrentLyricIndex) or nameof(PlaybackViewModel.DurationSeconds))
            RenderFrame();
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
    }

    private void Appearance_OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopLyricsAppearance.SaveError)) return;
        LyricText.ApplyAppearance(_appearance);
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
        HoverControls.Opacity = visible ? 1 : 0;
        HoverControls.IsHitTestVisible = visible;
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
