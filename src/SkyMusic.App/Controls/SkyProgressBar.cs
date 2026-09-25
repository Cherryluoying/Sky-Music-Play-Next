// 模块：播放进度拖动；完整热区点击定位，捕获指针后连续预览，松手只提交一次。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SkyMusic.App.Controls;

public sealed class SkyProgressBar : Slider
{
    private IPointer? _pointer;
    public event EventHandler? ScrubStarted;
    public event EventHandler? ScrubCompleted;
    public event EventHandler? ScrubCanceled;

    public SkyProgressBar()
    {
        // 在模板的 Thumb / RepeatButton 处理事件之前接管，防止默认步进与自定义定位叠加。
        AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, Moved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, _) => Finish(false);
        PropertyChanged += (_, change) =>
        {
            if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) Finish(false);
        };
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Maximum <= Minimum) return;
        Focus(NavigationMethod.Pointer);
        _pointer = e.Pointer;
        _pointer.Capture(this);
        ScrubStarted?.Invoke(this, EventArgs.Empty);
        Preview(e.GetPosition(this));
        e.Handled = true;
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (_pointer != e.Pointer) return;
        Preview(e.GetPosition(this));
        e.Handled = true;
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (_pointer != e.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;
        Preview(e.GetPosition(this));
        Finish(true);
        e.Handled = true;
    }

    private void Preview(Point position)
    {
        var fraction = Bounds.Width > 0 ? Math.Clamp(position.X / Bounds.Width, 0, 1) : 0;
        SetCurrentValue(ValueProperty, Minimum + fraction * (Maximum - Minimum));
    }

    private void Finish(bool commit)
    {
        var pointer = _pointer;
        if (pointer is null) return;
        _pointer = null;
        pointer.Capture(null);
        if (commit) ScrubCompleted?.Invoke(this, EventArgs.Empty);
        else ScrubCanceled?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        Finish(false);
        base.OnPointerCaptureLost(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _pointer is not null) { Finish(false); e.Handled = true; }
        else base.OnKeyDown(e);
    }
}
