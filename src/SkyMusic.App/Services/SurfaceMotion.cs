// 模块：轻量展示动画；只改变渲染变换，不参与测量布局，不改动播放状态。
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace SkyMusic.App.Services;

public static class SurfaceMotion
{
    // 由调用方取消上一次过渡，避免快速开关后旧动画再次隐藏新页面。
    public static Task SlideAsync(Control surface, double from, double to, CancellationToken cancellationToken)
    {
        surface.RenderTransform ??= new TranslateTransform();
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(260),
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.YProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.YProperty, to) } }
            }
        };
        return animation.RunAsync(surface, cancellationToken);
    }
}
