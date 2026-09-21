// 模块：SkyMusic.App 界面状态 GameComposerViewModel
using System.Collections.ObjectModel;
using SkyMusic.Core.GameScores;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using SkyMusic.App.Services;

namespace SkyMusic.App.ViewModels;

public sealed class GameComposerViewModel : ObservableObject, IDisposable
{
    private readonly GameScoreEditor _editor = new();
    private readonly Action<GameScoreDocument> _documentChanged;
    private readonly IScorePlaybackController? _playbackController;
    private readonly IInstrumentAssetCatalog? _instrumentAssets;
    private readonly IInstrumentPreviewService? _instrumentPreview;
    private readonly Stack<GameScoreDocument> _undo = new();
    private readonly Stack<GameScoreDocument> _redo = new();
    private IReadOnlyList<GameScoreColumn> _clipboard = [];
    private GameScoreDocument _document;
    private int _selectedColumn;
    private int _selectionAnchor;
    private int _selectedLayer;
    private bool _rangeSelectionEnabled;
    private int _midiTranspose;
    private int _midiOctaveFold = 4;
    private bool _midiIncludeAccidentals = true;
    private int _midiPrecision = 4;
    private bool _isSettingsOpen;
    private Score? _midiSource;
    private string _statusText = "就绪";

    public GameComposerViewModel(
        GameScoreDocument document,
        Action<GameScoreDocument> documentChanged,
        IScorePlaybackController? playbackController = null,
        IInstrumentAssetCatalog? instrumentAssets = null,
        IInstrumentPreviewService? instrumentPreview = null)
    {
        _document = document;
        _documentChanged = documentChanged;
        _playbackController = playbackController;
        _instrumentAssets = instrumentAssets;
        _instrumentPreview = instrumentPreview;
        TogglePadCommand = new RelayCommand(TogglePad);
        SelectLayerCommand = new RelayCommand(SelectLayer);
        AddColumnCommand = new RelayCommand(_ => Apply(_editor.AddColumns(Document, SelectedColumnIndexes.Max())));
        DeleteColumnCommand = new RelayCommand(_ => DeleteSelectedColumn());
        CopyCommand = new RelayCommand(_ => CopySelected());
        PasteCommand = new RelayCommand(_ => Paste(false));
        MergePasteCommand = new RelayCommand(_ => Paste(true));
        EraseCommand = new RelayCommand(_ => Apply(_editor.Erase(Document, SelectedColumnIndexes, SelectedLayer)));
        MoveUpCommand = new RelayCommand(_ => Apply(_editor.MoveNotes(Document, SelectedColumnIndexes, 1, SelectedLayer)));
        MoveDownCommand = new RelayCommand(_ => Apply(_editor.MoveNotes(Document, SelectedColumnIndexes, -1, SelectedLayer)));
        ToggleBreakpointCommand = new RelayCommand(_ => Apply(_editor.ToggleBreakpoint(Document, SelectedColumn)));
        AddInstrumentCommand = new RelayCommand(_ => Apply(_editor.AddInstrument(Document, DefaultInstrumentName())));
        RemoveInstrumentCommand = new RelayCommand(_ => RemoveSelectedInstrument());
        MoveLayerUpCommand = new RelayCommand(_ => MoveSelectedLayer(-1));
        MoveLayerDownCommand = new RelayCommand(_ => MoveSelectedLayer(1));
        UndoCommand = new RelayCommand(_ => Undo(), _ => _undo.Count > 0);
        RedoCommand = new RelayCommand(_ => Redo(), _ => _redo.Count > 0);
        SelectProfileCommand = new RelayCommand(SelectProfile);
        PreviewCommand = new AsyncRelayCommand(_ => PreviewAsync(), _ => _playbackController is not null, SetError);
        StopPreviewCommand = new AsyncRelayCommand(
            _ => _playbackController!.StopAsync().AsTask(),
            _ => _playbackController is not null,
            SetError);
        PreviewSelectionCommand = new AsyncRelayCommand(
            _ => PreviewSelectionAsync(),
            _ => _playbackController is not null,
            SetError);
        ReimportMidiCommand = new RelayCommand(_ => ConvertMidiSource(), _ => _midiSource is not null);
        ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
        if (_playbackController is not null)
            _playbackController.Changed += OnPlaybackChanged;
        RefreshCollections();
        _ = PreloadCurrentInstrumentAsync();
    }

    public ObservableCollection<ComposerPadViewModel> Pads { get; } = [];
    public ObservableCollection<ComposerInstrumentViewModel> Instruments { get; } = [];
    public ObservableCollection<ComposerColumnViewModel> Columns { get; } = [];
    public ObservableCollection<MidiImportTrackViewModel> MidiTracks { get; } = [];
    public ObservableCollection<InstrumentChoiceViewModel> AvailableInstruments { get; } = [];

    public GameScoreDocument Document
    {
        get => _document;
        private set
        {
            if (SetProperty(ref _document, value))
            {
                OnPropertyChanged(nameof(Bpm));
                OnPropertyChanged(nameof(ProfileName));
                OnPropertyChanged(nameof(SelectedTempoStep));
                OnPropertyChanged(nameof(ColumnSummary));
                OnPropertyChanged(nameof(DurationText));
                OnPropertyChanged(nameof(SongName));
                OnPropertyChanged(nameof(IsSkyProfile));
                OnPropertyChanged(nameof(IsGenshinProfile));
                OnPropertyChanged(nameof(IsCustomProfile));
                OnPropertyChanged(nameof(BackgroundArtwork));
                OnPropertyChanged(nameof(KeyboardMaxWidth));
            }
        }
    }

    public int SelectedColumn
    {
        get => _selectedColumn;
        set
        {
            var clamped = Math.Clamp(value, 0, Document.Columns.Count - 1);
            if (SetProperty(ref _selectedColumn, clamped))
            {
                if (!RangeSelectionEnabled)
                    _selectionAnchor = clamped;
                foreach (var column in Columns)
                    column.IsSelected = SelectedColumnIndexes.Contains(column.Index);
                RefreshPads();
                OnPropertyChanged(nameof(SelectedTempoStep));
                OnPropertyChanged(nameof(ColumnSummary));
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }
    }

    public int SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            var clamped = Math.Clamp(value, 0, Document.Instruments.Count - 1);
            if (SetProperty(ref _selectedLayer, clamped))
            {
                RefreshPads();
                RefreshColumns();
                NotifyInstrumentChanged();
                _ = PreloadCurrentInstrumentAsync();
            }
        }
    }

    public int Bpm
    {
        get => Document.Bpm;
        set
        {
            var bpm = Math.Clamp(value, 20, 999);
            if (bpm != Document.Bpm)
                Apply(Document with { Bpm = bpm });
        }
    }

    public int SelectedTempoStep
    {
        get => Document.Columns[SelectedColumn].TempoStep;
        set
        {
            if (value is >= 0 and <= 3 && value != Document.Columns[SelectedColumn].TempoStep)
                Apply(_editor.SetTempoStep(Document, SelectedColumn, value));
        }
    }

    public string ProfileName => Document.Profile switch
    {
        GameScoreProfile.Genshin => "原神 21 键",
        GameScoreProfile.Custom => "自定义 24 键",
        _ => "光遇 15 键"
    };
    public string SongName => Document.Name;
    public bool IsSkyProfile => Document.Profile == GameScoreProfile.Sky;
    public bool IsGenshinProfile => Document.Profile == GameScoreProfile.Genshin;
    public bool IsCustomProfile => Document.Profile == GameScoreProfile.Custom;
    public Bitmap? BackgroundArtwork => ComposerArtworkCache.GetBackground(Document.Profile);
    public double KeyboardMaxWidth => Document.Profile switch
    {
        GameScoreProfile.Genshin => 520,
        GameScoreProfile.Custom => 600,
        _ => 400
    };
    public string ColumnSummary => $"第 {SelectedColumn + 1} / {Document.Columns.Count} 列";
    public string SelectionSummary => SelectedColumnIndexes.Count == 1
        ? ColumnSummary
        : $"已选择 {SelectedColumnIndexes.Count} 列";

    public bool RangeSelectionEnabled
    {
        get => _rangeSelectionEnabled;
        set
        {
            if (!SetProperty(ref _rangeSelectionEnabled, value))
                return;
            if (!value)
                _selectionAnchor = SelectedColumn;
            RefreshColumns();
            OnPropertyChanged(nameof(SelectionSummary));
        }
    }

    public int MidiTranspose
    {
        get => _midiTranspose;
        set => SetProperty(ref _midiTranspose, Math.Clamp(value, -48, 48));
    }

    public int MidiOctaveFold
    {
        get => _midiOctaveFold;
        set => SetProperty(ref _midiOctaveFold, Math.Clamp(value, 0, 8));
    }

    public bool MidiIncludeAccidentals
    {
        get => _midiIncludeAccidentals;
        set => SetProperty(ref _midiIncludeAccidentals, value);
    }

    public int MidiPrecision
    {
        get => _midiPrecision;
        set => SetProperty(ref _midiPrecision, Math.Clamp(value, 1, 4));
    }

    public bool HasMidiSource => _midiSource is not null;
    public string DurationText
    {
        get
        {
            var milliseconds = Document.Columns.Sum(column => GameTempoSteps.GetDurationMilliseconds(Document.Bpm, column.TempoStep));
            return TimeSpan.FromMilliseconds(milliseconds).ToString(@"mm\:ss");
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public int InstrumentVolume
    {
        get => Document.Instruments[SelectedLayer].Volume;
        set => UpdateInstrument(item => item with { Volume = Math.Clamp(value, 0, 127) });
    }

    public string InstrumentAlias
    {
        get => Document.Instruments[SelectedLayer].Alias;
        set => UpdateInstrument(item => item with { Alias = value ?? string.Empty });
    }

    public InstrumentChoiceViewModel? SelectedInstrument
    {
        get => AvailableInstruments.FirstOrDefault(item => string.Equals(
            item.Id,
            Document.Instruments[SelectedLayer].Name,
            StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null || string.Equals(
                    value.Id,
                    Document.Instruments[SelectedLayer].Name,
                    StringComparison.OrdinalIgnoreCase))
                return;
            UpdateInstrument(item => item with { Name = value.Id });
            _ = PreloadCurrentInstrumentAsync();
        }
    }

    public bool InstrumentMuted
    {
        get => Document.Instruments[SelectedLayer].IsMuted;
        set => UpdateInstrument(item => item with { IsMuted = value });
    }

    public bool InstrumentVisible
    {
        get => Document.Instruments[SelectedLayer].IsVisible;
        set => UpdateInstrument(item => item with { IsVisible = value });
    }

    public bool Reverb
    {
        get => Document.Reverb;
        set
        {
            if (value != Document.Reverb)
                Apply(Document with { Reverb = value });
        }
    }

    public RelayCommand TogglePadCommand { get; }
    public RelayCommand SelectLayerCommand { get; }
    public RelayCommand AddColumnCommand { get; }
    public RelayCommand DeleteColumnCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand PasteCommand { get; }
    public RelayCommand MergePasteCommand { get; }
    public RelayCommand EraseCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand ToggleBreakpointCommand { get; }
    public RelayCommand AddInstrumentCommand { get; }
    public RelayCommand RemoveInstrumentCommand { get; }
    public RelayCommand MoveLayerUpCommand { get; }
    public RelayCommand MoveLayerDownCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand SelectProfileCommand { get; }
    public AsyncRelayCommand PreviewCommand { get; }
    public AsyncRelayCommand StopPreviewCommand { get; }
    public AsyncRelayCommand PreviewSelectionCommand { get; }
    public RelayCommand ReimportMidiCommand { get; }
    public RelayCommand ToggleSettingsCommand { get; }

    public IReadOnlyList<int> SelectedColumnIndexes
    {
        get
        {
            var start = RangeSelectionEnabled ? Math.Min(_selectionAnchor, SelectedColumn) : SelectedColumn;
            var end = RangeSelectionEnabled ? Math.Max(_selectionAnchor, SelectedColumn) : SelectedColumn;
            return Enumerable.Range(start, end - start + 1).ToArray();
        }
    }

    public void ToggleCell(int column, int key)
    {
        SelectedColumn = column;
        Apply(_editor.ToggleNote(Document, column, key, SelectedLayer));
        StatusText = $"已编辑第 {column + 1} 列";
    }

    // 载入游戏谱并重建编辑器状态与撤销历史
    public void LoadDocument(GameScoreDocument document, string status = "已载入乐谱")
    {
        GameScoreValidator.Validate(document);
        _undo.Clear();
        _redo.Clear();
        _clipboard = [];
        _selectionAnchor = 0;
        RangeSelectionEnabled = false;
        Document = document;
        SelectedColumn = 0;
        SelectedLayer = 0;
        RefreshCollections();
        StatusText = status;
        NotifyHistoryChanged();
    }

    public void LoadMidiScore(Score score)
    {
        _midiSource = score;
        MidiTracks.Clear();
        foreach (var group in score.Notes.GroupBy(note => note.Track).OrderBy(group => group.Key))
            MidiTracks.Add(new MidiImportTrackViewModel(group.Key, group.Count()));
        OnPropertyChanged(nameof(HasMidiSource));
        ReimportMidiCommand.NotifyCanExecuteChanged();
        ConvertMidiSource();
    }

    // 按当前量化参数把 MIDI 转为游戏谱
    private void ConvertMidiSource()
    {
        if (_midiSource is null)
            return;
        var score = _midiSource;
        var bpm = score.Metadata?.TryGetValue("bpm", out var value) == true &&
                  double.TryParse(value, out var parsed)
            ? (int)Math.Round(parsed)
            : Bpm;
        var included = MidiTracks.Where(track => track.IsIncluded).Select(track => track.Track).ToHashSet();
        if (included.Count == 0)
        {
            StatusText = "至少选择一个 MIDI 轨道";
            return;
        }
        var offsets = MidiTracks.Where(track => track.Transpose != 0)
            .ToDictionary(track => track.Track, track => track.Transpose);
        var converted = new GameScoreMidiConverter().Convert(
            score,
            new GameScoreImportOptions(
                Document.Profile,
                bpm,
                MidiTranspose,
                MidiOctaveFold,
                MidiIncludeAccidentals,
                MidiPrecision,
                IncludedTracks: included,
                TrackTransposeSemitones: offsets));
        LoadDocument(
            converted.Document,
            $"已导入 {converted.ImportedNoteCount} 个音符 · 临时音 {converted.AccidentalNoteCount} · 越界 {converted.BelowRangeCount + converted.AboveRangeCount}");
        _documentChanged(Document);
    }

    private void TogglePad(object? value)
    {
        if (value is int key || value is string text && int.TryParse(text, out key))
        {
            ToggleCell(SelectedColumn, key);
            _ = PreviewPadAsync(key);
        }
    }

    // 用当前乐器采样即时试听按键
    private async Task PreviewPadAsync(int key)
    {
        if (_instrumentPreview is null)
            return;
        var instrument = Document.Instruments[SelectedLayer];
        try
        {
            await _instrumentPreview.PlayAsync(
                Document.Profile,
                instrument.Name,
                key,
                instrument.Volume);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or
                                           DllNotFoundException or EntryPointNotFoundException or
                                           NotSupportedException or ObjectDisposedException)
        {
            Dispatcher.UIThread.Post(() => StatusText = $"试听失败: {exception.Message}");
        }
    }

    private async Task PreloadCurrentInstrumentAsync()
    {
        if (_instrumentPreview is null)
            return;
        var instrument = Document.Instruments[SelectedLayer];
        try
        {
            await _instrumentPreview.PreloadAsync(Document.Profile, instrument.Name);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or
                                           DllNotFoundException or EntryPointNotFoundException or
                                           NotSupportedException or ObjectDisposedException)
        {
            Dispatcher.UIThread.Post(() => StatusText = $"试听不可用: {exception.Message}");
        }
    }

    private void SelectLayer(object? value)
    {
        if (value is int index || value is string text && int.TryParse(text, out index))
            SelectedLayer = index;
    }

    private void SelectProfile(object? value)
    {
        if (value is not string text || !Enum.TryParse<GameScoreProfile>(text, true, out var profile))
            return;
        var keyCount = profile switch
        {
            GameScoreProfile.Genshin => 21,
            GameScoreProfile.Custom => 24,
            _ => 15
        };
        var columns = Document.Columns.Select(column => column with
        {
            Notes = column.Notes.Where(note => note.KeyIndex < keyCount).ToArray()
        }).ToArray();
        var instruments = Document.Instruments.Select(instrument =>
            _instrumentAssets?.Find(profile, instrument.Name) is not null
                ? instrument
                : instrument with { Name = DefaultInstrumentName(profile) }).ToArray();
        Apply(Document with { Profile = profile, Columns = columns, Instruments = instruments });
        StatusText = $"已切换为 {ProfileName}";
        _ = PreloadCurrentInstrumentAsync();
    }

    private void DeleteSelectedColumn()
    {
        var oldIndex = SelectedColumn;
        Apply(_editor.DeleteColumns(Document, SelectedColumnIndexes));
        SelectedColumn = Math.Min(oldIndex, Document.Columns.Count - 1);
    }

    private void CopySelected()
    {
        _clipboard = _editor.CopyColumns(Document, SelectedColumnIndexes, SelectedLayer);
        StatusText = $"已复制 {_clipboard.Count} 列";
    }

    private void Paste(bool merge)
    {
        if (_clipboard.Count == 0)
        {
            StatusText = "剪贴板为空";
            return;
        }
        Apply(_editor.PasteColumns(Document, SelectedColumn, _clipboard, merge));
        StatusText = merge ? "已合并粘贴" : "已插入粘贴";
    }

    private void RemoveSelectedInstrument()
    {
        if (Document.Instruments.Count == 1)
        {
            StatusText = "至少保留一个图层";
            return;
        }
        Apply(_editor.RemoveInstrument(Document, SelectedLayer));
        SelectedLayer = Math.Min(SelectedLayer, Document.Instruments.Count - 1);
    }

    private void MoveSelectedLayer(int amount)
    {
        var target = SelectedLayer + amount;
        if (target < 0 || target >= Document.Instruments.Count)
            return;
        Apply(_editor.MoveLayer(Document, SelectedLayer, target, false));
        SelectedLayer = target;
    }

    private void UpdateInstrument(Func<GameInstrumentLayer, GameInstrumentLayer> update)
    {
        var next = _editor.UpdateInstrument(Document, SelectedLayer, update);
        if (next.Instruments[SelectedLayer] != Document.Instruments[SelectedLayer])
            Apply(next);
    }

    private void Undo()
    {
        if (_undo.Count == 0)
            return;
        _redo.Push(Document);
        SetDocument(_undo.Pop());
        StatusText = "已撤销";
    }

    private void Redo()
    {
        if (_redo.Count == 0)
            return;
        _undo.Push(Document);
        SetDocument(_redo.Pop());
        StatusText = "已重做";
    }

    // 保存撤销快照后应用一次不可变文档变更
    private void Apply(GameScoreDocument next)
    {
        if (ReferenceEquals(next, Document) || next == Document)
            return;
        GameScoreValidator.Validate(next);
        _undo.Push(Document);
        _redo.Clear();
        SetDocument(next);
    }

    private void SetDocument(GameScoreDocument document)
    {
        Document = document;
        SelectedColumn = Math.Min(SelectedColumn, document.Columns.Count - 1);
        _selectionAnchor = Math.Min(_selectionAnchor, document.Columns.Count - 1);
        SelectedLayer = Math.Min(SelectedLayer, document.Instruments.Count - 1);
        RefreshCollections();
        _documentChanged(document);
        NotifyInstrumentChanged();
        NotifyHistoryChanged();
    }

    private void RefreshCollections()
    {
        RefreshAvailableInstruments();
        Instruments.Clear();
        for (var index = 0; index < Document.Instruments.Count; index++)
            Instruments.Add(new ComposerInstrumentViewModel(Document.Instruments[index], index, Document.Profile));
        RefreshPads();
        RefreshColumns();
    }

    private void RefreshPads()
    {
        Pads.Clear();
        var active = Document.Columns[SelectedColumn].Notes
            .Where(note => note.HasLayer(SelectedLayer))
            .Select(note => note.KeyIndex)
            .ToHashSet();
        for (var index = 0; index < Document.KeyCount; index++)
            Pads.Add(new ComposerPadViewModel(index, KeyLabel(Document.Profile, index), Document.Profile)
            {
                IsActive = active.Contains(index)
            });
    }

    private void RefreshColumns()
    {
        Columns.Clear();
        var order = GameKeyLayout.GetComposerOrder(Document.Profile);
        var breakpoints = Document.Breakpoints.ToHashSet();
        for (var index = 0; index < Document.Columns.Count; index++)
        {
            Columns.Add(new ComposerColumnViewModel(
                index,
                Document.Columns[index],
                order,
                SelectedLayer,
                breakpoints.Contains(index),
                SelectedColumnIndexes.Contains(index),
                selected => SelectedColumn = selected,
                ToggleCell));
        }
    }

    private void NotifyHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void NotifyInstrumentChanged()
    {
        OnPropertyChanged(nameof(SelectedInstrument));
        OnPropertyChanged(nameof(InstrumentVolume));
        OnPropertyChanged(nameof(InstrumentAlias));
        OnPropertyChanged(nameof(InstrumentMuted));
        OnPropertyChanged(nameof(InstrumentVisible));
        OnPropertyChanged(nameof(Reverb));
    }

    private void RefreshAvailableInstruments()
    {
        AvailableInstruments.Clear();
        var currentName = Document.Instruments[SelectedLayer].Name;
        var definitions = (_instrumentAssets?.GetInstruments(Document.Profile) ?? [])
            .OrderByDescending(instrument => string.Equals(
                instrument.Id,
                currentName,
                StringComparison.OrdinalIgnoreCase))
            .ThenBy(instrument => instrument.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var instrument in definitions)
            AvailableInstruments.Add(new InstrumentChoiceViewModel(instrument));
        OnPropertyChanged(nameof(SelectedInstrument));
    }

    private async Task PreviewAsync()
        => await LoadAndStartPreviewAsync(Document);

    private async Task PreviewSelectionAsync()
    {
        if (_playbackController is null)
            return;
        var columns = SelectedColumnIndexes.Select(index => Document.Columns[index]).ToArray();
        var preview = Document with { Columns = columns, Breakpoints = [0] };
        await LoadAndStartPreviewAsync(preview);
    }

    // 将选定谱面转为播放时间线并启动预览
    private async Task LoadAndStartPreviewAsync(GameScoreDocument document)
    {
        if (_playbackController is null)
            return;
        var score = new GameScorePlaybackConverter().ToScore(document);
        var targetId = document.Profile switch
        {
            GameScoreProfile.Genshin => "genshin-21",
            GameScoreProfile.Sky => "sky-15",
            _ => _playbackController.Snapshot.TargetId
        };
        if (_playbackController.Targets.Any(target => target.Id == targetId))
            await _playbackController.SelectTargetAsync(targetId);
        await _playbackController.LoadScoreAsync(score);
        await _playbackController.RefreshWindowsAsync();
        await _playbackController.StartAsync();
    }

    private void OnPlaybackChanged(ScorePlaybackSnapshot snapshot)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = snapshot.Session.State switch
            {
                AutoPlayState.Playing => "正在预览游戏演奏",
                AutoPlayState.Paused => "预览已暂停",
                AutoPlayState.Completed => "预览完成",
                _ => StatusText
            };
        });
    }

    private void SetError(Exception exception) => StatusText = exception.Message;

    public void Dispose()
    {
        if (_playbackController is not null)
            _playbackController.Changed -= OnPlaybackChanged;
        _instrumentPreview?.Dispose();
    }

    private string DefaultInstrumentName()
        => DefaultInstrumentName(Document.Profile);

    private static string DefaultInstrumentName(GameScoreProfile profile)
        => profile == GameScoreProfile.Genshin ? "Lyre" : "Piano";

    private static string KeyLabel(GameScoreProfile profile, int index)
    {
        const string skyKeys = "YUIOPHJKL;NM,./";
        const string genshinKeys = "QWERTYUASDFGHJZXCVBNM";
        var keys = profile switch
        {
            GameScoreProfile.Sky => skyKeys,
            GameScoreProfile.Genshin => genshinKeys,
            _ => string.Empty
        };
        return index < keys.Length ? keys[index].ToString() : (index + 1).ToString();
    }
}
