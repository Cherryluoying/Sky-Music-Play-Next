// 模块：SkyMusic.App 自定义控件 LyricsView.axaml
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Controls;

public sealed partial class LyricsView : UserControl
{
    private readonly ListBox? _lyricsList;
    private PlaybackViewModel? _viewModel;

    public LyricsView()
    {
        InitializeComponent();
        _lyricsList = this.FindControl<ListBox>("LyricsList");
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
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
        Dispatcher.UIThread.Post(() => CenterCurrentLine(index), DispatcherPriority.Background);
    }

    private void CenterCurrentLine(int index)
    {
        if (_lyricsList is null)
        {
            return;
        }

        _lyricsList.ScrollIntoView(index);

        // 使用真实歌词项高度计算居中位置
        Dispatcher.UIThread.Post(() =>
        {
            var container = _lyricsList.ContainerFromIndex(index) as Control;
            var scrollViewer = _lyricsList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
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
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, Math.Clamp(target, 0, maximum));
        }, DispatcherPriority.Background);
    }
}
