// 模块：SkyMusic.Infrastructure MIDI 钢琴窗可视化数据
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Scores;

public sealed class MidiVisualizationService : IMidiVisualizationService
{
    // 复用标准 MIDI 导入器，保证播放与可视化看到的是同一条时间线。
    public async ValueTask<IReadOnlyList<MidiVisualNote>> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var result = await new MidiScoreImporter().ImportAsync(
            File.OpenRead(filePath),
            Path.GetFileName(filePath),
            cancellationToken);
        if (!result.IsSuccess || result.Score is null)
        {
            return [];
        }

        return result.Score.Notes
            .Select(note => new MidiVisualNote(
                note.MidiNote,
                TimeSpan.FromTicks(note.StartMicroseconds * 10),
                TimeSpan.FromTicks(note.DurationMicroseconds * 10),
                note.Velocity,
                note.Track))
            .ToArray();
    }
}
