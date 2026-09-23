// 模块：SkyMusic.App 专业编曲工作区状态
using System.Collections.ObjectModel;
using Avalonia.Threading;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Projects;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public enum ProfessionalEditTool
{
    Select,
    Draw,
    Erase
}

public sealed class ProfessionalWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly MusicProjectEditor _editor = new();
    private readonly MusicProjectScoreConverter _scoreConverter = new();
    private readonly Action<MusicProject> _projectChanged;
    private readonly IScorePlaybackController? _playbackController;
    private readonly Stack<MusicProject> _undo = new();
    private readonly Stack<MusicProject> _redo = new();
    private MusicProject _project;
    private ProfessionalTrackViewModel? _selectedTrack;
    private Guid? _selectedNoteId;
    private ProfessionalEditTool _tool;
    private string _quantize = "1/16";
    private double _horizontalZoom = 1;
    private long _playheadTick;
    private bool _isPlaying;
    private bool _playbackLoaded;
    private string _statusText = "专业工作区就绪";

    public ProfessionalWorkspaceViewModel(
        MusicProject project,
        Action<MusicProject> projectChanged,
        IScorePlaybackController? playbackController = null)
    {
        _project = project;
        _projectChanged = projectChanged;
        _playbackController = playbackController;

        AddTrackCommand = new RelayCommand(_ => AddTrack());
        RemoveTrackCommand = new RelayCommand(_ => RemoveTrack(), _ => SelectedTrack is not null);
        DuplicateTrackCommand = new RelayCommand(_ => DuplicateTrack(), _ => SelectedTrack is not null);
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        ToggleSoloCommand = new RelayCommand(ToggleSolo);
        SelectToolCommand = new RelayCommand(value => SelectTool(value?.ToString()));
        UndoCommand = new RelayCommand(_ => Undo(), _ => _undo.Count > 0);
        RedoCommand = new RelayCommand(_ => Redo(), _ => _redo.Count > 0);
        DeleteNoteCommand = new RelayCommand(_ => DeleteSelectedNote(), _ => SelectedNoteId is not null);
        DuplicateNoteCommand = new RelayCommand(_ => DuplicateSelectedNote(), _ => SelectedNote is not null);
        QuantizeCommand = new RelayCommand(_ => QuantizeSelected(), _ => SelectedNote is not null);
        TransposeUpCommand = new RelayCommand(_ => TransposeSelected(1), _ => SelectedNote is not null);
        TransposeDownCommand = new RelayCommand(_ => TransposeSelected(-1), _ => SelectedNote is not null);
        PlayPauseCommand = new AsyncRelayCommand(_ => TogglePlaybackAsync(), _ => _playbackController is not null, SetError);
        StopCommand = new AsyncRelayCommand(_ => StopAsync(), _ => _playbackController is not null, SetError);
        RewindCommand = new AsyncRelayCommand(_ => RewindAsync(), _ => _playbackController is not null, SetError);

        if (_playbackController is not null)
            _playbackController.Changed += OnPlaybackChanged;
        RefreshProject();
    }

    public ObservableCollection<ProfessionalTrackViewModel> Tracks { get; } = [];
    public IReadOnlyList<string> QuantizeOptions { get; } = ["1/4", "1/8", "1/12", "1/16", "1/32"];

    public MusicProject Project => _project;
    public int Ppq => _project.Ppq;
    public IReadOnlyList<ProjectTrack> ProjectTracks => _project.Tracks;
    public IReadOnlyList<ProjectNote> Notes => SelectedTrack?.Track.Notes ?? [];
    public string ProjectSummary => $"{_project.Ppq} PPQ · {_project.Tracks.Count} 轨 · {_project.Tracks.Sum(track => track.Notes.Count)} 音符";
    public string TempoText => $"{Bpm:0.##} BPM";
    public string TimeSignatureText
    {
        get
        {
            var signature = _project.TimeSignatures.FirstOrDefault();
            return signature.Numerator > 0 ? $"{signature.Numerator}/{signature.Denominator}" : "4/4";
        }
    }

    public ProfessionalTrackViewModel? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (!SetProperty(ref _selectedTrack, value))
                return;
            SelectedNoteId = null;
            NotifySelectionChanged();
        }
    }

    public Guid? SelectedTrackId => SelectedTrack?.Id;

    public Guid? SelectedNoteId
    {
        get => _selectedNoteId;
        set
        {
            if (!SetProperty(ref _selectedNoteId, value))
                return;
            NotifyNoteChanged();
        }
    }

    public ProjectNote? SelectedNote => SelectedNoteId is { } id
        ? Notes.FirstOrDefault(note => note.Id == id)
        : null;

    public string SelectedTrackName
    {
        get => SelectedTrack?.Name ?? string.Empty;
        set
        {
            if (SelectedTrack is null || string.IsNullOrWhiteSpace(value) || value.Trim() == SelectedTrack.Name)
                return;
            Apply(_editor.UpdateTrack(_project, SelectedTrack.Id, track => track with { Name = value }), "轨道已重命名");
        }
    }

    public double SelectedTrackGain
    {
        get => SelectedTrack?.Track.Gain ?? 1;
        set
        {
            if (SelectedTrack is null || Math.Abs(value - SelectedTrack.Track.Gain) < 0.001)
                return;
            Apply(_editor.UpdateTrack(_project, SelectedTrack.Id, track => track with { Gain = (float)value }), "已调整轨道音量");
        }
    }

    public double SelectedTrackPan
    {
        get => SelectedTrack?.Track.Pan ?? 0;
        set
        {
            if (SelectedTrack is null || Math.Abs(value - SelectedTrack.Track.Pan) < 0.001)
                return;
            Apply(_editor.UpdateTrack(_project, SelectedTrack.Id, track => track with { Pan = (float)value }), "已调整声像");
        }
    }

    public int SelectedVelocity
    {
        get => SelectedNote?.Velocity ?? 100;
        set
        {
            if (SelectedTrack is null || SelectedNote is null || value == SelectedNote.Velocity)
                return;
            Apply(_editor.SetVelocity(_project, SelectedTrack.Id, [SelectedNote.Id], value), "已调整力度");
        }
    }

    public double Bpm
    {
        get => _project.TempoMap.Count == 0 ? 120 : _project.TempoMap[0].BeatsPerMinute;
        set
        {
            var bpm = Math.Clamp(value, 20, 400);
            if (Math.Abs(bpm - Bpm) < 0.01)
                return;
            var tempo = (int)Math.Round(60_000_000d / bpm);
            var map = _project.TempoMap.ToArray();
            map[0] = new TempoChange(0, tempo);
            Apply(_project with
            {
                TempoMap = map,
                Metadata = _project.Metadata with { ModifiedAt = DateTimeOffset.UtcNow }
            }, "已调整工程速度");
        }
    }

    public string Quantize
    {
        get => _quantize;
        set
        {
            if (SetProperty(ref _quantize, value))
                OnPropertyChanged(nameof(QuantizeTicks));
        }
    }

    public long QuantizeTicks => Quantize switch
    {
        "1/4" => Ppq,
        "1/8" => Math.Max(1, Ppq / 2),
        "1/12" => Math.Max(1, Ppq / 3),
        "1/32" => Math.Max(1, Ppq / 8),
        _ => Math.Max(1, Ppq / 4)
    };

    public ProfessionalEditTool Tool
    {
        get => _tool;
        private set
        {
            if (!SetProperty(ref _tool, value))
                return;
            OnPropertyChanged(nameof(IsSelectTool));
            OnPropertyChanged(nameof(IsDrawTool));
            OnPropertyChanged(nameof(IsEraseTool));
        }
    }

    public bool IsSelectTool { get => Tool == ProfessionalEditTool.Select; set { if (value) Tool = ProfessionalEditTool.Select; } }
    public bool IsDrawTool { get => Tool == ProfessionalEditTool.Draw; set { if (value) Tool = ProfessionalEditTool.Draw; } }
    public bool IsEraseTool { get => Tool == ProfessionalEditTool.Erase; set { if (value) Tool = ProfessionalEditTool.Erase; } }

    public double HorizontalZoom
    {
        get => _horizontalZoom;
        set
        {
            if (!SetProperty(ref _horizontalZoom, Math.Clamp(value, 0.5, 3)))
                return;
            OnPropertyChanged(nameof(PixelsPerQuarter));
            OnPropertyChanged(nameof(TimelineWidth));
        }
    }

    public double PixelsPerQuarter => 96 * HorizontalZoom;
    public double TimelineWidth => 74 + Math.Max(8, Math.Ceiling((double)ProjectLengthTicks / Ppq) + 2) * PixelsPerQuarter;
    public double PianoRollHeight => 88 * 18;
    public long ProjectLengthTicks => Math.Max(Ppq * 16L, _project.Tracks.SelectMany(track => track.Notes).Select(note => note.EndTick).DefaultIfEmpty(0).Max());

    public long PlayheadTick
    {
        get => _playheadTick;
        private set => SetProperty(ref _playheadTick, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
                OnPropertyChanged(nameof(PlayPauseGlyph));
        }
    }

    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768";
    public string PositionText => $"{new TickTimeConverter(Ppq, _project.TempoMap).TickToMicroseconds(PlayheadTick) / 1_000_000d:0.00}s";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public RelayCommand AddTrackCommand { get; }
    public RelayCommand RemoveTrackCommand { get; }
    public RelayCommand DuplicateTrackCommand { get; }
    public RelayCommand ToggleMuteCommand { get; }
    public RelayCommand ToggleSoloCommand { get; }
    public RelayCommand SelectToolCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand DeleteNoteCommand { get; }
    public RelayCommand DuplicateNoteCommand { get; }
    public RelayCommand QuantizeCommand { get; }
    public RelayCommand TransposeUpCommand { get; }
    public RelayCommand TransposeDownCommand { get; }
    public AsyncRelayCommand PlayPauseCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand RewindCommand { get; }

    public void LoadProject(MusicProject project, string status = "已载入工程")
    {
        MusicProjectValidator.Validate(project);
        _project = project;
        _undo.Clear();
        _redo.Clear();
        _playbackLoaded = false;
        SelectedNoteId = null;
        StatusText = status;
        RefreshProject();
        NotifyHistoryChanged();
    }

    public void SelectNote(Guid? noteId) => SelectedNoteId = noteId;

    public void SelectTrack(Guid trackId)
        => SelectedTrack = Tracks.FirstOrDefault(track => track.Id == trackId) ?? SelectedTrack;

    public void AddNote(long startTick, long lengthTicks, int midiNote, byte velocity = 100)
    {
        if (SelectedTrack is null)
            return;
        var note = new ProjectNote(
            Guid.NewGuid(),
            Math.Max(0, startTick),
            Math.Max(1, lengthTicks),
            Math.Clamp(midiNote, 0, 127),
            velocity,
            SelectedTrack.Track.Kind == ProjectTrackKind.Percussion ? 9 : 0);
        Apply(_editor.AddNote(_project, SelectedTrack.Id, note), "已添加音符");
        SelectedNoteId = note.Id;
    }

    public void MoveNote(Guid noteId, long startTick, int midiNote)
    {
        if (SelectedTrack is null || Notes.FirstOrDefault(note => note.Id == noteId) is not { } note)
            return;
        Apply(_editor.MoveNotes(
            _project,
            SelectedTrack.Id,
            [noteId],
            startTick - note.StartTick,
            midiNote - note.MidiNote), "已移动音符");
        SelectedNoteId = noteId;
    }

    public void ResizeNote(Guid noteId, long lengthTicks)
    {
        if (SelectedTrack is null)
            return;
        Apply(_editor.ResizeNote(_project, SelectedTrack.Id, noteId, lengthTicks), "已调整音符长度");
        SelectedNoteId = noteId;
    }

    public void DeleteNote(Guid noteId)
    {
        if (SelectedTrack is null)
            return;
        Apply(_editor.DeleteNotes(_project, SelectedTrack.Id, [noteId]), "已删除音符");
        SelectedNoteId = null;
    }

    private void AddTrack()
    {
        var next = _editor.AddTrack(_project, $"Track {_project.Tracks.Count + 1}", color: TrackColor(_project.Tracks.Count));
        Apply(next, "已添加轨道");
        SelectedTrack = Tracks.LastOrDefault();
    }

    private void RemoveTrack()
    {
        if (SelectedTrack is null)
            return;
        var index = Tracks.IndexOf(SelectedTrack);
        Apply(_editor.RemoveTrack(_project, SelectedTrack.Id), "已删除轨道");
        SelectedTrack = Tracks.Count == 0 ? null : Tracks[Math.Min(index, Tracks.Count - 1)];
    }

    private void DuplicateTrack()
    {
        if (SelectedTrack is null)
            return;
        var sourceIndex = Tracks.IndexOf(SelectedTrack);
        Apply(_editor.DuplicateTrack(_project, SelectedTrack.Id), "已复制轨道");
        SelectedTrack = Tracks[Math.Min(sourceIndex + 1, Tracks.Count - 1)];
    }

    private void ToggleMute(object? parameter) => UpdateTrackFlag(parameter, true);
    private void ToggleSolo(object? parameter) => UpdateTrackFlag(parameter, false);

    private void UpdateTrackFlag(object? parameter, bool mute)
    {
        if (!TryGetTrackId(parameter, out var id))
            return;
        Apply(_editor.UpdateTrack(_project, id, track => mute
            ? track with { IsMuted = !track.IsMuted }
            : track with { IsSolo = !track.IsSolo }), mute ? "已切换静音" : "已切换独奏");
    }

    private void SelectTool(string? value)
    {
        Tool = Enum.TryParse<ProfessionalEditTool>(value, true, out var tool) ? tool : ProfessionalEditTool.Select;
        StatusText = Tool switch
        {
            ProfessionalEditTool.Draw => "铅笔工具 · 单击网格添加音符",
            ProfessionalEditTool.Erase => "擦除工具 · 单击音符删除",
            _ => "选择工具 · 拖动音符，拖右边缘改变长度"
        };
    }

    private void DeleteSelectedNote()
    {
        if (SelectedNoteId is { } id)
            DeleteNote(id);
    }

    private void DuplicateSelectedNote()
    {
        if (SelectedTrack is null || SelectedNote is not { } note)
            return;
        var copy = note with { Id = Guid.NewGuid(), StartTick = note.StartTick + QuantizeTicks };
        Apply(_editor.AddNote(_project, SelectedTrack.Id, copy), "已复制音符");
        SelectedNoteId = copy.Id;
    }

    private void QuantizeSelected()
    {
        if (SelectedTrack is null || SelectedNote is not { } note)
            return;
        Apply(_editor.QuantizeNotes(_project, SelectedTrack.Id, [note.Id], QuantizeTicks), $"已量化到 {Quantize}");
        SelectedNoteId = note.Id;
    }

    private void TransposeSelected(int semitones)
    {
        if (SelectedTrack is null || SelectedNote is not { } note)
            return;
        Apply(_editor.MoveNotes(_project, SelectedTrack.Id, [note.Id], 0, semitones), "已移调音符");
        SelectedNoteId = note.Id;
    }

    private void Undo()
    {
        if (_undo.Count == 0)
            return;
        _redo.Push(_project);
        SetProject(_undo.Pop());
        StatusText = "已撤销";
    }

    private void Redo()
    {
        if (_redo.Count == 0)
            return;
        _undo.Push(_project);
        SetProject(_redo.Pop());
        StatusText = "已重做";
    }

    private void Apply(MusicProject next, string status)
    {
        if (ReferenceEquals(next, _project) || next == _project)
            return;
        MusicProjectValidator.Validate(next);
        _undo.Push(_project);
        _redo.Clear();
        SetProject(next);
        StatusText = status;
    }

    private void SetProject(MusicProject project)
    {
        var trackId = SelectedTrackId;
        var noteId = SelectedNoteId;
        _project = project;
        _playbackLoaded = false;
        RefreshProject(trackId);
        SelectedNoteId = Notes.Any(note => note.Id == noteId) ? noteId : null;
        _projectChanged(project);
        NotifyHistoryChanged();
    }

    private void RefreshProject(Guid? preferredTrackId = null)
    {
        var selectedId = preferredTrackId ?? SelectedTrackId;
        Tracks.Clear();
        for (var index = 0; index < _project.Tracks.Count; index++)
            Tracks.Add(new ProfessionalTrackViewModel(_project.Tracks[index], index));
        _selectedTrack = Tracks.FirstOrDefault(track => track.Id == selectedId) ?? Tracks.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedTrack));
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(ProjectTracks));
        OnPropertyChanged(nameof(ProjectSummary));
        OnPropertyChanged(nameof(Ppq));
        OnPropertyChanged(nameof(Bpm));
        OnPropertyChanged(nameof(TempoText));
        OnPropertyChanged(nameof(TimeSignatureText));
        OnPropertyChanged(nameof(QuantizeTicks));
        OnPropertyChanged(nameof(ProjectLengthTicks));
        OnPropertyChanged(nameof(TimelineWidth));
        NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedTrackId));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(SelectedTrackName));
        OnPropertyChanged(nameof(SelectedTrackGain));
        OnPropertyChanged(nameof(SelectedTrackPan));
        RemoveTrackCommand.NotifyCanExecuteChanged();
        DuplicateTrackCommand.NotifyCanExecuteChanged();
        NotifyNoteChanged();
    }

    private void NotifyNoteChanged()
    {
        OnPropertyChanged(nameof(SelectedNote));
        OnPropertyChanged(nameof(SelectedVelocity));
        DeleteNoteCommand.NotifyCanExecuteChanged();
        DuplicateNoteCommand.NotifyCanExecuteChanged();
        QuantizeCommand.NotifyCanExecuteChanged();
        TransposeUpCommand.NotifyCanExecuteChanged();
        TransposeDownCommand.NotifyCanExecuteChanged();
    }

    private void NotifyHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private async Task TogglePlaybackAsync()
    {
        if (_playbackController is null)
            return;
        if (_playbackController.Snapshot.Session.State == AutoPlayState.Playing)
        {
            await _playbackController.PauseAsync();
            StatusText = "试听已暂停";
            return;
        }

        if (!_playbackLoaded)
        {
            var score = _scoreConverter.ToScore(_project);
            if (score.Notes.Count == 0)
            {
                StatusText = "工程中没有可试听的音符";
                return;
            }
            await _playbackController.LoadScoreAsync(score);
            _playbackLoaded = true;
        }
        await _playbackController.StartAsync();
        StatusText = "正在试听工程";
    }

    private async Task StopAsync()
    {
        if (_playbackController is null)
            return;
        await _playbackController.StopAsync();
        PlayheadTick = 0;
        OnPropertyChanged(nameof(PositionText));
        StatusText = "试听已停止";
    }

    private async Task RewindAsync()
    {
        if (_playbackController is null)
            return;
        await _playbackController.SeekAsync(0);
        PlayheadTick = 0;
        OnPropertyChanged(nameof(PositionText));
    }

    private void OnPlaybackChanged(ScorePlaybackSnapshot snapshot)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = snapshot.Session.State == AutoPlayState.Playing;
            var converter = new TickTimeConverter(Ppq, _project.TempoMap);
            PlayheadTick = converter.MicrosecondsToTick(Math.Max(0, snapshot.Session.PositionMicroseconds));
            OnPropertyChanged(nameof(PositionText));
            PlayPauseCommand.NotifyCanExecuteChanged();
        });
    }

    private void SetError(Exception exception) => StatusText = exception.Message;

    private static bool TryGetTrackId(object? parameter, out Guid id)
    {
        if (parameter is Guid value)
        {
            id = value;
            return true;
        }
        return Guid.TryParse(parameter?.ToString(), out id);
    }

    private static string TrackColor(int index) => (index % 6) switch
    {
        0 => "#43A6C6",
        1 => "#D9A441",
        2 => "#65B56D",
        3 => "#D06B62",
        4 => "#7A8FD1",
        _ => "#B77BB5"
    };

    public void Dispose()
    {
        if (_playbackController is not null)
            _playbackController.Changed -= OnPlaybackChanged;
    }
}
