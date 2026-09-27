// 模块：演奏参数视图，复用 PlaybackViewModel，不持有独立播放状态。
using Avalonia.Controls;
namespace SkyMusic.App.Controls;
public sealed partial class ScorePerformanceSettings : UserControl
{
    public ScorePerformanceSettings() => InitializeComponent();
}
