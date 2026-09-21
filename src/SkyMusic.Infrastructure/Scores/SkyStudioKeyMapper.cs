// 模块：SkyMusic.Infrastructure 通用模型 SkyStudioKeyMapper
namespace SkyMusic.Infrastructure.Scores;

internal static class SkyStudioKeyMapper
{
    private static readonly int[] MajorScaleOffsets = [0, 2, 4, 5, 7, 9, 11];

    public static bool TryMap(int keyIndex, int pitchLevel, bool applyPitchLevel, out int midiNote)
    {
        var octave = Math.DivRem(keyIndex, 7, out var scaleIndex);
        if (scaleIndex < 0)
        {
            scaleIndex += 7;
            octave--;
        }

        midiNote = 60 + (octave * 12) + MajorScaleOffsets[scaleIndex];
        if (applyPitchLevel)
        {
            midiNote += pitchLevel;
        }

        return midiNote is >= 21 and <= 108;
    }
}
