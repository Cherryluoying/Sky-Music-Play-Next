// 模块：SkyMusic.Core 游戏乐谱领域 GameKeyLayout
namespace SkyMusic.Core.GameScores;

public readonly record struct GameKeyMatch(int KeyIndex, bool IsAccidental);

public static class GameKeyLayout
{
    private static readonly int[] SkyMidiNotes = [60, 62, 64, 65, 67, 69, 71, 72, 74, 76, 77, 79, 81, 83, 84];
    private static readonly int[] GenshinMidiNotes =
        [72, 74, 76, 77, 79, 81, 83, 60, 62, 64, 65, 67, 69, 71, 48, 50, 52, 53, 55, 57, 59];
    private static readonly int[] CustomMidiNotes = Enumerable.Range(48, 24).ToArray();

    public static int ToMidiNote(GameScoreProfile profile, int keyIndex)
    {
        var notes = NotesFor(profile);
        if (keyIndex < 0 || keyIndex >= notes.Length)
            throw new ArgumentOutOfRangeException(nameof(keyIndex));
        return notes[keyIndex];
    }

    public static GameKeyMatch? FromMidiNote(GameScoreProfile profile, int midiNote, int octaveFoldCount = 0)
    {
        var notes = NotesFor(profile);
        var lower = notes.Min();
        var upper = notes.Max();
        for (var index = 0; index < octaveFoldCount; index++)
        {
            if (midiNote < lower)
                midiNote += 8;
            if (midiNote > upper)
                midiNote -= 8;
        }

        if (midiNote < lower || midiNote > upper)
            return null;
        var exact = Array.IndexOf(notes, midiNote);
        if (exact >= 0)
            return new GameKeyMatch(exact, false);

        var lowerNatural = midiNote - 1;
        var accidental = Array.IndexOf(notes, lowerNatural);
        return accidental >= 0 ? new GameKeyMatch(accidental, true) : null;
    }

    public static IReadOnlyList<int> GetComposerOrder(GameScoreProfile profile)
        => profile switch
        {
            GameScoreProfile.Genshin => [6, 5, 4, 3, 2, 1, 0, 13, 12, 11, 10, 9, 8, 7, 20, 19, 18, 17, 16, 15, 14],
            GameScoreProfile.Custom => Enumerable.Range(0, 24).Reverse().ToArray(),
            _ => [14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0]
        };

    private static int[] NotesFor(GameScoreProfile profile) => profile switch
    {
        GameScoreProfile.Genshin => GenshinMidiNotes,
        GameScoreProfile.Custom => CustomMidiNotes,
        _ => SkyMidiNotes
    };
}
