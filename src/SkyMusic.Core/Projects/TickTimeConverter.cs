// 模块：SkyMusic.Core 工程领域 TickTimeConverter
namespace SkyMusic.Core.Projects;

public sealed class TickTimeConverter
{
    private readonly int _ppq;
    private readonly TempoSegment[] _segments;

    public TickTimeConverter(int ppq, IReadOnlyList<TempoChange> tempoMap)
    {
        if (ppq <= 0)
            throw new ArgumentOutOfRangeException(nameof(ppq));
        ArgumentNullException.ThrowIfNull(tempoMap);
        if (tempoMap.Count == 0 || tempoMap[0].Tick != 0)
            throw new ArgumentException("Tempo map must start at tick zero", nameof(tempoMap));

        _ppq = ppq;
        _segments = BuildSegments(tempoMap);
    }

    // 根据速度段把 MIDI tick 转为微秒
    public long TickToMicroseconds(long tick)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));

        var segment = FindByTick(tick);
        var elapsed = (decimal)(tick - segment.StartTick) * segment.MicrosecondsPerQuarterNote / _ppq;
        return checked(segment.StartMicroseconds + Round(elapsed));
    }

    // 根据速度段把微秒反算为 MIDI tick
    public long MicrosecondsToTick(long microseconds)
    {
        if (microseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(microseconds));

        var segment = FindByMicroseconds(microseconds);
        var elapsed = (decimal)(microseconds - segment.StartMicroseconds) * _ppq /
                      segment.MicrosecondsPerQuarterNote;
        return checked(segment.StartTick + Round(elapsed));
    }

    private TempoSegment FindByTick(long tick)
    {
        var low = 0;
        var high = _segments.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (_segments[middle].StartTick <= tick)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return _segments[Math.Max(0, high)];
    }

    private TempoSegment FindByMicroseconds(long microseconds)
    {
        var low = 0;
        var high = _segments.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (_segments[middle].StartMicroseconds <= microseconds)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return _segments[Math.Max(0, high)];
    }

    // 预计算各速度段的累计起点以支持快速查找
    private TempoSegment[] BuildSegments(IReadOnlyList<TempoChange> tempoMap)
    {
        var result = new TempoSegment[tempoMap.Count];
        long accumulatedMicroseconds = 0;
        for (var index = 0; index < tempoMap.Count; index++)
        {
            var current = tempoMap[index];
            if (current.Tick < 0 || current.MicrosecondsPerQuarterNote <= 0 ||
                (index > 0 && current.Tick <= tempoMap[index - 1].Tick))
                throw new ArgumentException("Tempo map must be ordered and contain valid values", nameof(tempoMap));

            if (index > 0)
            {
                var previous = tempoMap[index - 1];
                var elapsed = (decimal)(current.Tick - previous.Tick) * previous.MicrosecondsPerQuarterNote / _ppq;
                accumulatedMicroseconds = checked(accumulatedMicroseconds + Round(elapsed));
            }

            result[index] = new TempoSegment(current.Tick, accumulatedMicroseconds, current.MicrosecondsPerQuarterNote);
        }

        return result;
    }

    private static long Round(decimal value) => decimal.ToInt64(decimal.Round(value, 0, MidpointRounding.AwayFromZero));

    private readonly record struct TempoSegment(long StartTick, long StartMicroseconds, int MicrosecondsPerQuarterNote);
}
