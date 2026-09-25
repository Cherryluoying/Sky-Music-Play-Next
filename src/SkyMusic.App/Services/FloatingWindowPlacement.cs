// 模块：悬浮气泡布局；以猫爪屏幕坐标为锚点，按工作区空间选择方向，支持负坐标和 DPI 缩放。
using Avalonia;

namespace SkyMusic.App.Services;

public sealed record FloatingWindowLayout(PixelPoint Position, Size Size, Rect Ball, Rect Bubble);

public static class FloatingWindowPlacement
{
    public static FloatingWindowLayout Calculate(PixelRect workingArea, double scale, PixelPoint ballPosition, bool expanded)
    {
        scale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var area = new Rect(0, 0, workingArea.Width / scale, workingArea.Height / scale);
        var ballSize = Math.Min(64, Math.Min(area.Width, area.Height));
        var ball = new Rect(Math.Clamp((ballPosition.X - workingArea.X) / scale, 0, Math.Max(0, area.Width - ballSize)),
            Math.Clamp((ballPosition.Y - workingArea.Y) / scale, 0, Math.Max(0, area.Height - ballSize)), ballSize, ballSize);
        var bubble = new Rect();
        var bounds = ball;
        if (expanded)
        {
            const double gap = 8;
            var width = Math.Min(352, area.Width);
            var height = Math.Min(412, area.Height);
            var right = Math.Max(0, area.Right - ball.Right - gap);
            var left = Math.Max(0, ball.Left - gap);
            var below = Math.Max(0, area.Bottom - ball.Bottom - gap);
            var above = Math.Max(0, ball.Top - gap);
            // 优先在左右完整展开；左右不足时改为上下。极小屏幕按可用空间缩短面板。
            var horizontal = Math.Max(left, right) >= width ||
                (Math.Max(above, below) < height && Math.Max(left, right) / width >= Math.Max(above, below) / height);
            if (horizontal)
            {
                width = Math.Min(width, Math.Max(left, right));
                bubble = new Rect(right >= left ? ball.Right + gap : ball.Left - gap - width,
                    Math.Clamp(ball.Center.Y - height / 2, 0, Math.Max(0, area.Height - height)), width, height);
            }
            else
            {
                height = Math.Min(height, Math.Max(above, below));
                bubble = new Rect(Math.Clamp(ball.Center.X - width / 2, 0, Math.Max(0, area.Width - width)),
                    below >= above ? ball.Bottom + gap : ball.Top - gap - height, width, height);
            }
            bounds = ball.Union(bubble);
        }

        var position = new PixelPoint(workingArea.X + (int)Math.Round(bounds.X * scale),
            workingArea.Y + (int)Math.Round(bounds.Y * scale));
        // 用取整后的窗口原点回算偏移，展开/收起不累积像素误差。
        var offset = new Vector((position.X - workingArea.X) / scale, (position.Y - workingArea.Y) / scale);
        return new FloatingWindowLayout(position, bounds.Size, ball.Translate(-offset), bubble.Translate(-offset));
    }
}
