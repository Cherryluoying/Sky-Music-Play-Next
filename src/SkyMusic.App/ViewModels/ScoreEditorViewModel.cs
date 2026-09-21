// 模块：SkyMusic.App 界面状态 ScoreEditorViewModel
using System.Collections.ObjectModel;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.App.ViewModels;

public sealed class ScoreEditorViewModel : ObservableObject
{
    private readonly IScoreImportService _importService;
    private readonly Stack<EditorState> _undo = [];
    private readonly Stack<EditorState> _redo = [];
    private bool _restoring;
    private string _title = "未命名乐谱";
    private string _composer = "";
    private string _statusText = "可以导入 Sky Studio TXT 或从空白乐谱开始";

    public ScoreEditorViewModel(IScoreImportService importService)
    {
        _importService = importService;
        AddNoteCommand = new RelayCommand(_ => AddNote());
        DeleteNoteCommand = new RelayCommand(item => DeleteNote(item as EditableNoteViewModel));
        UndoCommand = new RelayCommand(_ => Undo(), _ => _undo.Count > 0);
        RedoCommand = new RelayCommand(_ => Redo(), _ => _redo.Count > 0);
    }

    public ObservableCollection<EditableNoteViewModel> Notes { get; } = [];

    public RelayCommand AddNoteCommand { get; }

    public RelayCommand DeleteNoteCommand { get; }

    public RelayCommand UndoCommand { get; }

    public RelayCommand RedoCommand { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Composer
    {
        get => _composer;
        set => SetProperty(ref _composer, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    // 导入乐谱并生成可编辑音符集合
    public async Task ImportAsync(string path)
    {
        var result = await _importService.ImportAsync(path);
        if (!result.IsSuccess || result.Score is null)
        {
            StatusText = result.Issues.FirstOrDefault()?.Message ?? "乐谱导入失败";
            return;
        }

        _undo.Clear();
        _redo.Clear();
        Restore(EditorState.FromScore(result.Score));
        StatusText = $"已载入 {Notes.Count} 个音符";
    }

    // 校验当前音符并导出标准 MIDI
    public async Task ExportAsync(string path)
    {
        var score = BuildScore();
        await using var stream = File.Create(path);
        if (Path.GetExtension(path).Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".midi", StringComparison.OrdinalIgnoreCase))
        {
            new MidiScoreExporter().Export(score, stream);
            StatusText = $"已保存 {score.Notes.Count} 个 MIDI 音符";
            return;
        }

        var skipped = await new SkyStudioScoreExporter().ExportAsync(score, stream);
        StatusText = skipped == 0
            ? $"已保存 {score.Notes.Count} 个音符"
            : $"已保存 {score.Notes.Count - skipped} 个音符，Sky Studio 不支持的半音已跳过 {skipped} 个";
    }

    private void AddNote()
    {
        SaveUndo();
        var start = Notes.Count == 0 ? 0 : Notes.Max(note => note.StartMilliseconds + note.DurationMilliseconds);
        AddNoteCore(new EditableNoteViewModel(60, start, 300, 100));
        StatusText = $"当前共 {Notes.Count} 个音符";
    }

    private void DeleteNote(EditableNoteViewModel? note)
    {
        if (note is null)
        {
            return;
        }

        SaveUndo();
        note.ValueChanging -= OnNoteValueChanging;
        Notes.Remove(note);
        StatusText = $"当前共 {Notes.Count} 个音符";
    }

    private void OnNoteValueChanging(EditableNoteViewModel note, string propertyName, long oldValue)
    {
        if (_restoring)
        {
            return;
        }

        var state = Capture();
        var index = Notes.IndexOf(note);
        var values = state.Notes.ToArray();
        var current = values[index];
        values[index] = propertyName switch
        {
            nameof(EditableNoteViewModel.MidiNote) => current with { MidiNote = checked((int)oldValue) },
            nameof(EditableNoteViewModel.StartMilliseconds) => current with { StartMilliseconds = oldValue },
            nameof(EditableNoteViewModel.DurationMilliseconds) => current with { DurationMilliseconds = oldValue },
            nameof(EditableNoteViewModel.Velocity) => current with { Velocity = checked((int)oldValue) },
            _ => current
        };
        _undo.Push(state with { Notes = values });
        _redo.Clear();
        NotifyHistoryChanged();
    }

    private void Undo()
    {
        if (_undo.TryPop(out var state))
        {
            _redo.Push(Capture());
            Restore(state);
            NotifyHistoryChanged();
        }
    }

    private void Redo()
    {
        if (_redo.TryPop(out var state))
        {
            _undo.Push(Capture());
            Restore(state);
            NotifyHistoryChanged();
        }
    }

    // 在修改前保存编辑器快照并清空重做栈
    private void SaveUndo()
    {
        _undo.Push(Capture());
        _redo.Clear();
        NotifyHistoryChanged();
    }

    private EditorState Capture() => new(
        Title,
        Composer,
        Notes.Select(note => new EditorNote(
            note.MidiNote,
            note.StartMilliseconds,
            note.DurationMilliseconds,
            note.Velocity)).ToArray());

    private void Restore(EditorState state)
    {
        _restoring = true;
        try
        {
            foreach (var note in Notes)
            {
                note.ValueChanging -= OnNoteValueChanging;
            }
            Notes.Clear();
            Title = state.Title;
            Composer = state.Composer;
            foreach (var note in state.Notes)
            {
                AddNoteCore(new EditableNoteViewModel(
                    note.MidiNote,
                    note.StartMilliseconds,
                    note.DurationMilliseconds,
                    note.Velocity));
            }
        }
        finally
        {
            _restoring = false;
        }
    }

    private void AddNoteCore(EditableNoteViewModel note)
    {
        note.ValueChanging += OnNoteValueChanging;
        Notes.Add(note);
    }

    private Score BuildScore() => new(
        string.IsNullOrWhiteSpace(Title) ? "未命名乐谱" : Title.Trim(),
        Composer.Trim(),
        Notes.Select(note => new NoteEvent(
                note.MidiNote,
                checked(note.StartMilliseconds * 1_000),
                checked(note.DurationMilliseconds * 1_000),
                checked((byte)note.Velocity)))
            .OrderBy(note => note.StartMicroseconds)
            .ThenBy(note => note.MidiNote)
            .ToArray());

    private void NotifyHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private sealed record EditorState(string Title, string Composer, IReadOnlyList<EditorNote> Notes)
    {
        public static EditorState FromScore(Score score) => new(
            score.Title,
            score.Composer,
            score.Notes.Select(note => new EditorNote(
                note.MidiNote,
                note.StartMicroseconds / 1_000,
                Math.Max(10, note.DurationMicroseconds / 1_000),
                note.Velocity)).ToArray());
    }

    private sealed record EditorNote(int MidiNote, long StartMilliseconds, long DurationMilliseconds, int Velocity);
}
