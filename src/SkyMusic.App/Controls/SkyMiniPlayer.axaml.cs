// 模块：SkyMusic.App 自定义控件 SkyMiniPlayer.axaml
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Controls;

public sealed partial class SkyMiniPlayer : UserControl
{
    public SkyMiniPlayer() => InitializeComponent();

    private void PlayerSurface_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || DataContext is not PlaybackViewModel playback || !playback.HasTrack ||
            e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        if (e.Source is Control source &&
            source.GetVisualAncestors().Any(ancestor => ancestor is Button or Slider or TextBox or ComboBox))
        {
            return;
        }

        playback.OpenPlayerCommand.Execute(null);
        e.Handled = true;
    }
}
