// 模块：全屏浮层显隐；保留退场帧、支持快速反向切换，不改变父容器尺寸。
using Avalonia.Controls;
using Avalonia.Media;

namespace SkyMusic.App.Services;

public sealed class SurfaceVisibilityMotion(Control surface, double hiddenOffset) : IDisposable
{
    private CancellationTokenSource? _motion;
    public bool IsShown { get; private set; } = surface.IsVisible;

    public async void SetVisible(bool visible, bool animate = true)
    {
        if (visible == IsShown && animate) return;
        IsShown = visible;
        var from = surface.IsVisible && surface.RenderTransform is TranslateTransform current
            ? current.Y : surface.IsVisible ? 0 : hiddenOffset;
        _motion?.Cancel();
        surface.IsHitTestVisible = visible;
        surface.RenderTransform = new TranslateTransform(0, visible ? 0 : hiddenOffset);
        if (!animate) { surface.IsVisible = visible; return; }
        var motion = new CancellationTokenSource();
        _motion = motion;
        surface.IsVisible = true;
        try
        {
            await SurfaceMotion.SlideAsync(surface, from, visible ? 0 : hiddenOffset, motion.Token);
            if (!motion.IsCancellationRequested) surface.IsVisible = visible;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_motion == motion) _motion = null;
            motion.Dispose();
        }
    }

    public void Dispose() => _motion?.Cancel();
}
