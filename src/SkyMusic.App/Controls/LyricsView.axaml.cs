// 模块：歌词居中跟随。手动浏览临时暂停跟随，逐句滚动使用与帧率无关的缓出动画。
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

namespace SkyMusic.App.Controls;

public sealed partial class LyricsView : UserControl
{
    private readonly ItemsControl _items;
    private readonly ScrollViewer _scroll;
    private readonly Border _padding;
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _resumeTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly Stopwatch _animation = new();
    private PlaybackViewModel? _viewModel;
    private bool _attached;
    private int _centerRequest;
    private double _startOffset;
    private double _targetOffset;

    public LyricsView()
    {
        InitializeComponent();
        _items = this.FindControl<ItemsControl>("LyricsItems")!;
        _scroll = this.FindControl<ScrollViewer>("LyricsScroll")!;
        _padding = this.FindControl<Border>("LyricsPadding")!;
        _scrollTimer.Tick += OnScrollTimerTick;
        _resumeTimer.Tick += (_, _) => ResumeFollow();
        // 在隧道路由中暂停跟随，避免 ScrollViewer 处理滚轮后继续自动居中。
        _scroll.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        DataContextChanged += (_, _) => BindViewModel();
    }

    private void BindViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnPlaybackChanged;
            _viewModel.Lyrics.CollectionChanged -= OnLyricsChanged;
        }
        _scrollTimer.Stop();
        _resumeTimer.Stop();
        _centerRequest++;
        _viewModel = _attached ? DataContext as PlaybackViewModel : null;
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += OnPlaybackChanged;
        _viewModel.Lyrics.CollectionChanged += OnLyricsChanged;
        QueueCenter();
    }

    private void OnPlaybackChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlaybackViewModel.CurrentLyricIndex) or nameof(PlaybackViewModel.CurrentItem))
            QueueCenter();
    }

    private void OnLyricsChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueCenter();

    // 两端各预留半个可视区，第一句、最后一句也使用同一居中规则。
    private void LyricsScroll_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_padding is null) return;
        var half = Math.Max(0, e.NewSize.Height / 2);
        _padding.Padding = new Thickness(0, half, 0, half);
        QueueCenter();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _centerRequest++;
        _scrollTimer.Stop();
        _resumeTimer.Stop();
        _resumeTimer.Start();
    }

    private void LyricsScroll_OnPointerExited(object? sender, PointerEventArgs e) => ResumeFollow();

    private void ResumeFollow()
    {
        _resumeTimer.Stop();
        QueueCenter();
    }

    // 点击整句或右侧时间戳都跳转，并恢复当前句的居中跟随。
    private void Timeline_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: LyricLineViewModel line } || _viewModel is null) return;
        _resumeTimer.Stop();
        _viewModel.SeekToLyric(line);
        QueueCenter(_viewModel.Lyrics.IndexOf(line));
        e.Handled = true;
    }

    private void QueueCenter(int? requestedIndex = null)
    {
        if (!_attached || _resumeTimer.IsEnabled) return;
        var request = ++_centerRequest;
        // 等布局和绑定提交后读取真实高度，不用上一帧位置计算目标。
        Dispatcher.UIThread.Post(() =>
        {
            if (request != _centerRequest || !_attached || _resumeTimer.IsEnabled || _viewModel is null) return;
            var index = requestedIndex ?? Math.Max(0, _viewModel.CurrentLyricIndex);
            _items.UpdateLayout();
            if (_items.ContainerFromIndex(index) is not Control container || _scroll.Viewport.Height <= 0) return;
            var point = container.TranslatePoint(default, _items);
            if (point is null) return;
            var center = _padding.Padding.Top + point.Value.Y + container.Bounds.Height / 2;
            _startOffset = _scroll.Offset.Y;
            _targetOffset = Math.Clamp(center - _scroll.Viewport.Height / 2, 0,
                Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height));
            _animation.Restart();
            _scrollTimer.Start();
        }, DispatcherPriority.Loaded);
    }

    private void OnScrollTimerTick(object? sender, EventArgs e)
    {
        var progress = Math.Clamp(_animation.Elapsed.TotalMilliseconds / 440, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        _scroll.Offset = new Vector(0, _startOffset + (_targetOffset - _startOffset) * eased);
        if (progress >= 1) _scrollTimer.Stop();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        BindViewModel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        BindViewModel();
        base.OnDetachedFromVisualTree(e);
    }
}
