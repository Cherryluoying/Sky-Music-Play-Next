// 模块：SkyMusic.App 自定义控件 LyricsView.axaml
using System.ComponentModel;
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
    private readonly ListBox? _lyricsList;
    private readonly DispatcherTimer _scrollTimer;
    private readonly DispatcherTimer _resumeFollowTimer;
    private PlaybackViewModel? _viewModel;
    private ScrollViewer? _scrollViewer;
    private double _targetOffset;

    public LyricsView()
    {
        InitializeComponent();
        _lyricsList = this.FindControl<ListBox>("LyricsList");
        _scrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _scrollTimer.Tick += OnScrollTimerTick;
        _resumeFollowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _resumeFollowTimer.Tick += ResumeFollow_OnTick;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _scrollTimer.Stop();
        _scrollViewer = null;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PlaybackViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlaybackViewModel.CurrentLyricIndex) ||
            _viewModel is null ||
            _lyricsList is null ||
            _viewModel.CurrentLyricIndex < 0)
        {
            return;
        }

        var index = _viewModel.CurrentLyricIndex;
        if (_resumeFollowTimer.IsEnabled)
        {
            return;
        }
        Dispatcher.UIThread.Post(() => CenterCurrentLine(index), DispatcherPriority.Background);
    }

    // 用户滚动时暂时停止自动追踪，三秒后平滑回到当前歌词。
    private void LyricsList_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _scrollTimer.Stop();
        _resumeFollowTimer.Stop();
        _resumeFollowTimer.Start();
    }

    private void ResumeFollow_OnTick(object? sender, EventArgs e)
    {
        _resumeFollowTimer.Stop();
        if (_viewModel?.CurrentLyricIndex >= 0)
        {
            CenterCurrentLine(_viewModel.CurrentLyricIndex);
        }
    }

    private void Timeline_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: LyricLineViewModel line } || _viewModel is null)
        {
            return;
        }

        _resumeFollowTimer.Stop();
        _viewModel.SeekToLyric(line);
        CenterCurrentLine(_viewModel.Lyrics.IndexOf(line));
        e.Handled = true;
    }

    private void CenterCurrentLine(int index)
    {
        if (_lyricsList is null)
        {
            return;
        }

        var scrollViewer = _lyricsList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var previousOffset = scrollViewer?.Offset.Y ?? 0;
        _lyricsList.ScrollIntoView(index);

        // 使用真实歌词项高度计算居中位置
        Dispatcher.UIThread.Post(() =>
        {
            var container = _lyricsList.ContainerFromIndex(index) as Control;
            scrollViewer ??= _lyricsList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (container is null || scrollViewer is null)
            {
                return;
            }

            var point = container.TranslatePoint(default, scrollViewer);
            if (point is null)
            {
                return;
            }

            var target = scrollViewer.Offset.Y + point.Value.Y -
                         ((scrollViewer.Viewport.Height - container.Bounds.Height) / 2);
            var maximum = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            _targetOffset = Math.Clamp(target, 0, maximum);
            _scrollViewer = scrollViewer;

            // ScrollIntoView 仅用于生成虚拟化容器，随后恢复位置并平滑过渡。
            var restoredOffset = Math.Clamp(previousOffset, 0, maximum);
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, restoredOffset);
            _scrollTimer.Start();
        }, DispatcherPriority.Background);
    }

    private void OnScrollTimerTick(object? sender, EventArgs e)
    {
        if (_scrollViewer is null)
        {
            _scrollTimer.Stop();
            return;
        }

        var current = _scrollViewer.Offset.Y;
        var delta = _targetOffset - current;
        if (Math.Abs(delta) < 0.5)
        {
            _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, _targetOffset);
            _scrollTimer.Stop();
            return;
        }

        var next = current + (delta * 0.18);
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, next);
    }
}
