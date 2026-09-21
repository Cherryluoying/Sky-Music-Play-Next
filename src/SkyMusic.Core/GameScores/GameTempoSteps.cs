// 模块：SkyMusic.Core 游戏乐谱领域 GameTempoSteps
namespace SkyMusic.Core.GameScores;

public static class GameTempoSteps
{
    private static readonly double[] Ratios = [1d, 0.5d, 0.25d, 0.125d];

    public static double GetRatio(int step)
        => step >= 0 && step < Ratios.Length
            ? Ratios[step]
            : throw new ArgumentOutOfRangeException(nameof(step));

    public static int GetDurationMilliseconds(int bpm, int step)
        => (int)Math.Round((60_000d / bpm) * GetRatio(step));
}
