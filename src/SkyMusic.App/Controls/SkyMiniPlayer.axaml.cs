// 模块：SkyMusic.App 自定义控件 SkyMiniPlayer.axaml
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Controls;

public sealed partial class SkyMiniPlayer : UserControl
{
    public SkyMiniPlayer() => InitializeComponent();

    private void Progress_OnScrubStarted(object? sender, EventArgs e)
        => (DataContext as PlaybackViewModel)?.BeginScrub();

    private void Progress_OnScrubCompleted(object? sender, EventArgs e)
        => (DataContext as PlaybackViewModel)?.EndScrub(true);

    private void Progress_OnScrubCanceled(object? sender, EventArgs e)
        => (DataContext as PlaybackViewModel)?.EndScrub(false);

    // 气泡里的删除仅编辑播放队列，不删除歌单、收藏或磁盘文件。
    private void RemoveQueueTrack_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TrackItemViewModel item } && DataContext is PlaybackViewModel playback)
            playback.RemoveFromQueue(item);
        e.Handled = true;
    }

    // 打开 MIDI 音色气泡时先扫描插件目录，让首次打开也能直接选择音色。
    private void InstrumentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is PlaybackViewModel playback && playback.IsMidi)
        {
            playback.ScanInstrumentPluginsCommand.Execute(null);
        }
    }

    private void PlayerSurface_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || DataContext is not PlaybackViewModel playback || !playback.HasTrack ||
            e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        if (e.Source is Control source &&
            (source is Button or Slider or TextBox or ComboBox ||
             source.GetVisualAncestors().Any(ancestor => ancestor is Button or Slider or TextBox or ComboBox)))
        {
            return;
        }

        playback.OpenPlayerCommand.Execute(null);
        e.Handled = true;
    }
}
