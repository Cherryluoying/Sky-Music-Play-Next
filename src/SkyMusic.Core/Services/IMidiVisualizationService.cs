// 模块：SkyMusic.Core MIDI 可视化数据服务
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Services;

public interface IMidiVisualizationService
{
    ValueTask<IReadOnlyList<MidiVisualNote>> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
