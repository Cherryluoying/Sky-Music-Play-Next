// 模块：SkyMusic.Backend.Tests 后端测试 GameScoreEditorTests
using SkyMusic.Core.GameScores;

namespace SkyMusic.Backend.Tests;

public sealed class GameScoreEditorTests
{
    private readonly GameScoreEditor _editor = new();

    [Fact]
    public void ToggleNoteMergesAndRemovesLayerBits()
    {
        var document = GameScoreDocument.Create("Editor");
        document = _editor.AddInstrument(document, "Harp");

        document = _editor.ToggleNote(document, 0, 4, 0);
        document = _editor.ToggleNote(document, 0, 4, 1);

        Assert.Equal(3UL, Assert.Single(document.Columns[0].Notes).LayerMask);
        document = _editor.ToggleNote(document, 0, 4, 0);
        Assert.Equal(2UL, Assert.Single(document.Columns[0].Notes).LayerMask);
        document = _editor.ToggleNote(document, 0, 4, 1);
        Assert.Empty(document.Columns[0].Notes);
    }

    [Fact]
    public void DeleteColumnsKeepsBreakpointsAligned()
    {
        var document = GameScoreDocument.Create("Editor") with { Breakpoints = [0, 4, 8] };

        document = _editor.DeleteColumns(document, [2, 4]);

        Assert.Equal([0, 6], document.Breakpoints);
        Assert.Equal(30, document.Columns.Count);
    }

    [Fact]
    public void MovingLayerPreservesItsNotes()
    {
        var document = _editor.AddInstrument(GameScoreDocument.Create("Editor"), "Harp");
        document = _editor.ToggleNote(document, 0, 2, 0);

        document = _editor.MoveLayer(document, 0, 1, false);

        Assert.Equal("Piano", document.Instruments[1].Name);
        Assert.Equal(2UL, Assert.Single(document.Columns[0].Notes).LayerMask);
    }

    [Fact]
    public void InsertPasteMovesFollowingBreakpoints()
    {
        var document = GameScoreDocument.Create("Editor") with { Breakpoints = [0, 4] };
        var copied = _editor.CopyColumns(document, [0, 1]);

        document = _editor.PasteColumns(document, 3, copied, false);

        Assert.Equal([0, 6], document.Breakpoints);
    }
}
