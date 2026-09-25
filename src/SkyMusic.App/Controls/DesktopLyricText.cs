// 模块：透明桌面歌词绘制；缓存文字轮廓，只按播放时钟更新填色裁剪区域。
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Controls;

public sealed class DesktopLyricText : Control
{
    private Geometry? _geometry;
    private string _text = string.Empty;
    private double _progress;
    private double _fontSize = 36;
    private double _outlineWidth = 2;
    private IBrush _textBrush = Brushes.White;
    private IBrush _fillBrush = new SolidColorBrush(Color.Parse("#FFCB70"));
    private IBrush _outlineBrush = new SolidColorBrush(Color.Parse("#20242C"));
    private readonly Typeface _typeface = new(
        new FontFamily("avares://SkyMusic.App/Assets/fonts/OPPO-Sans.ttf#OPPO Sans 4.0"),
        FontStyle.Normal, FontWeight.Bold);

    public string Text => _text;
    public double Progress => _progress;

    // 只显示当前句，长句整体缩放到窗口宽度，不换行、不显示下一句。
    public void SetFrame(string text, double progress)
    {
        var clean = text.Replace('\r', ' ').Replace('\n', ' ');
        var bounded = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;
        if (_text == clean && Math.Abs(_progress - bounded) < 0.0001) return;
        if (_text != clean) _geometry = null;
        _text = clean;
        _progress = bounded;
        InvalidateVisual();
    }

    public void ApplyAppearance(DesktopLyricsAppearance appearance)
    {
        if (_fontSize != appearance.FontSize) _geometry = null;
        _fontSize = appearance.FontSize;
        _outlineWidth = appearance.OutlineWidth;
        _textBrush = appearance.TextColor.Brush;
        _fillBrush = appearance.FillColor.Brush;
        _outlineBrush = appearance.OutlineColor.Brush;
        Opacity = appearance.OpacityPercent / 100;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (string.IsNullOrWhiteSpace(_text) || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        _geometry ??= new FormattedText(_text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            _typeface, _fontSize, Brushes.White).BuildGeometry(default);
        if (_geometry is null || _geometry.Bounds.Width <= 0) return;

        var ink = _geometry.Bounds;
        var margin = _outlineWidth + 3;
        var scale = Math.Min(1, Math.Min(Bounds.Width / (ink.Width + margin * 2),
            Bounds.Height / (ink.Height + margin * 2)));
        var x = (Bounds.Width - ink.Width * scale) / 2 - ink.X * scale;
        var y = (Bounds.Height - ink.Height * scale) / 2 - ink.Y * scale;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            // 描边在填充之前绘制，填色覆盖字形内部，不破坏深色轮廓。
            if (_outlineWidth > 0)
                context.DrawGeometry(null, new Pen(_outlineBrush, _outlineWidth * 2,
                    lineJoin: PenLineJoin.Round), _geometry);
            context.DrawGeometry(_textBrush, null, _geometry);
            if (_progress > 0)
            {
                using (context.PushClip(new Rect(ink.X, ink.Y - margin,
                    ink.Width * _progress, ink.Height + margin * 2)))
                    context.DrawGeometry(_fillBrush, null, _geometry);
            }
        }
    }
}
