// 模块：SkyMusic.App 界面状态 MidiStudioViewModel
using System.Collections.ObjectModel;
using System.Threading.Channels;
using Avalonia.Threading;
using SkyMusic.Core.Midi;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Plugins;
using SkyMusic.Infrastructure.Plugins;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.App.ViewModels;

public sealed class MidiStudioViewModel : ObservableObject, IDisposable
{
    private readonly IMidiInputCapture _capture;
    private readonly MidiPerformanceRecorder _recorder = new();
    private readonly IPlaybackEventMonitor _playbackMonitor;
    private readonly IInstrumentPluginHost _pluginHost;
    private readonly IReadOnlyList<string> _vst3SearchPaths;
    private readonly Channel<PluginMidiEvent> _pluginEvents = Channel.CreateBounded<PluginMidiEvent>(new BoundedChannelOptions(2048)
    {
        SingleReader = true,
        SingleWriter = false,
        // TryWrite 必须在满队列时返回 false，不能悄悄丢掉松键/踏板释放。
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly CancellationTokenSource _pluginCancellation = new();
    private readonly Task _pluginWorker;
    private int _resetPluginQueue;
    private readonly HashSet<int> _activeNotes = [];
    private MidiInputDeviceInfo? _selectedDevice;
    private InstrumentPluginInfo? _selectedPlugin;
    private Score? _recordedScore;
    private bool _isListening;
    private bool _isRecording;
    private string _statusText = "选择 MIDI 输入设备后开始监听";

    public MidiStudioViewModel(
        IMidiInputCapture capture,
        IPlaybackEventMonitor playbackMonitor,
        IInstrumentPluginHost pluginHost,
        IReadOnlyList<string>? vst3SearchPaths = null)
    {
        _capture = capture;
        _playbackMonitor = playbackMonitor;
        _pluginHost = pluginHost;
        _vst3SearchPaths = vst3SearchPaths is { Count: > 0 }
            ? vst3SearchPaths
            : Vst3PluginCatalog.GetDefaultSearchPaths();
        _capture.NoteChanged += OnNoteChanged;
        _capture.MessageReceived += OnMidiMessage;
        _playbackMonitor.EventPlayed += OnPlaybackEvent;
        RefreshCommand = new RelayCommand(_ => RefreshDevices());
        ToggleListeningCommand = new RelayCommand(_ => ToggleListening(), _ => SelectedDevice is not null);
        ToggleRecordingCommand = new RelayCommand(_ => ToggleRecording(), _ => IsListening);
        ScanPluginsCommand = new RelayCommand(_ => ScanPlugins());
        LoadPluginCommand = new AsyncRelayCommand(_ => LoadPluginAsync(), _ => SelectedPlugin is not null, SetError);
        _pluginWorker = ProcessPluginEventsAsync(_pluginCancellation.Token);
        RefreshDevices();
    }

    public event Action<IReadOnlySet<int>>? ActiveNotesChanged;

    public ObservableCollection<MidiInputDeviceInfo> Devices { get; } = [];

    public ObservableCollection<MidiNoteItemViewModel> RecentEvents { get; } = [];

    public ObservableCollection<InstrumentPluginInfo> Plugins { get; } = [];

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ToggleListeningCommand { get; }

    public RelayCommand ToggleRecordingCommand { get; }

    public RelayCommand ScanPluginsCommand { get; }

    public AsyncRelayCommand LoadPluginCommand { get; }

    public MidiInputDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                ToggleListeningCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public InstrumentPluginInfo? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (SetProperty(ref _selectedPlugin, value))
            {
                LoadPluginCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (SetProperty(ref _isListening, value))
            {
                OnPropertyChanged(nameof(ListeningLabel));
                ToggleRecordingCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                OnPropertyChanged(nameof(RecordingLabel));
            }
        }
    }

    public string ListeningLabel => IsListening ? "停止监听" : "开始监听";

    public string RecordingLabel => IsRecording ? "结束录制" : "录制演奏";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool HasRecording => _recordedScore?.Notes.Count > 0;

    // 同步刷新 MIDI 输入输出设备列表
    public void RefreshDevices()
    {
        try
        {
            var selectedIndex = SelectedDevice?.Index;
            Devices.Clear();
            foreach (var device in _capture.RefreshDevices())
            {
                Devices.Add(device);
            }

            SelectedDevice = Devices.FirstOrDefault(device => device.Index == selectedIndex) ?? Devices.FirstOrDefault();
            StatusText = Devices.Count == 0 ? "未发现 MIDI 输入设备" : $"已发现 {Devices.Count} 个输入设备";
        }
        catch (Exception exception)
        {
            StatusText = $"MIDI 设备发现失败：{exception.Message}";
        }
    }

    public async Task ExportRecordingAsync(string path)
    {
        if (_recordedScore is null)
        {
            return;
        }

        await using var stream = File.Create(path);
        if (Path.GetExtension(path).Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".midi", StringComparison.OrdinalIgnoreCase))
        {
            new MidiScoreExporter().Export(_recordedScore, stream);
            StatusText = $"已导出 {_recordedScore.Notes.Count} 个 MIDI 音符";
            return;
        }

        var skipped = await new SkyStudioScoreExporter().ExportAsync(_recordedScore, stream);
        StatusText = skipped == 0
            ? $"已导出 {_recordedScore.Notes.Count} 个音符"
            : $"已导出 {_recordedScore.Notes.Count - skipped} 个音符，跳过 {skipped} 个半音音符";
    }

    private void ToggleListening()
    {
        try
        {
            if (IsListening)
            {
                StopListening();
                return;
            }

            if (SelectedDevice is null)
            {
                return;
            }

            _capture.Start(SelectedDevice.Index);
            IsListening = true;
            StatusText = $"正在监听 {SelectedDevice.Name}";
        }
        catch (Exception exception)
        {
            StatusText = $"MIDI 监听失败：{exception.Message}";
        }
    }

    private void ScanPlugins()
    {
        try
        {
            Plugins.Clear();
            foreach (var plugin in new Vst3PluginCatalog().Discover(_vst3SearchPaths))
            {
                Plugins.Add(plugin);
            }
            SelectedPlugin = Plugins.FirstOrDefault();
            StatusText = Plugins.Count == 0
                ? "未发现 VST3 插件"
                : $"已发现 {Plugins.Count} 个 VST3 插件";
        }
        catch (Exception exception)
        {
            StatusText = $"VST3 扫描失败：{exception.Message}";
        }
    }

    // 开始或结束带时间戳的 MIDI 演奏录制
    private void ToggleRecording()
    {
        if (!IsRecording)
        {
            _recordedScore = null;
            _recorder.Start();
            IsRecording = true;
            OnPropertyChanged(nameof(HasRecording));
            StatusText = "正在录制 MIDI 演奏";
            return;
        }

        _recordedScore = _recorder.Stop($"MIDI 录制 {DateTime.Now:yyyy-MM-dd HH-mm}");
        IsRecording = false;
        OnPropertyChanged(nameof(HasRecording));
        StatusText = $"录制完成，共 {_recordedScore.Notes.Count} 个音符";
    }

    // 完整消息只转发一次；音符事件保留给录制和钢琴显示。
    private void OnMidiMessage(MidiChannelMessage message) => EnqueuePluginEvent(new(message));

    // 同步钢琴窗与录制状态，不重复向插件发送音符。
    private void OnNoteChanged(MidiNoteMessage message)
    {
        _recorder.Process(message);
        Dispatcher.UIThread.Post(() =>
        {
            if (message.IsNoteOn)
            {
                _activeNotes.Add(message.Note);
            }
            else
            {
                _activeNotes.Remove(message.Note);
            }

            ActiveNotesChanged?.Invoke(_activeNotes);
            RecentEvents.Insert(0, MidiNoteItemViewModel.FromMessage(message));
            while (RecentEvents.Count > 24)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }
        });
    }

    private void OnPlaybackEvent(PlaybackEvent playbackEvent)
    {
        EnqueuePluginEvent(new PluginMidiEvent(new MidiChannelMessage(
            playbackEvent.Type == PlaybackEventType.KeyDown ? MidiChannelMessageKind.NoteOn : MidiChannelMessageKind.NoteOff,
            playbackEvent.MidiNote,
            playbackEvent.Velocity,
            playbackEvent.Channel)));
        Dispatcher.UIThread.Post(() =>
        {
            if (playbackEvent.Type == PlaybackEventType.KeyDown)
            {
                _activeNotes.Add(playbackEvent.MidiNote);
            }
            else
            {
                _activeNotes.Remove(playbackEvent.MidiNote);
            }
            ActiveNotesChanged?.Invoke(_activeNotes);
        });
    }

    private void StopListening()
    {
        if (IsRecording)
        {
            ToggleRecording();
        }

        _capture.Stop();
        if (IsListening) EnqueuePluginEvent(new(default, Reset: true));
        IsListening = false;
        _activeNotes.Clear();
        ActiveNotesChanged?.Invoke(_activeNotes);
        StatusText = "MIDI 监听已停止";
    }

    public void Dispose()
    {
        StopListening();
        _capture.NoteChanged -= OnNoteChanged;
        _capture.MessageReceived -= OnMidiMessage;
        _playbackMonitor.EventPlayed -= OnPlaybackEvent;
        _capture.Dispose();
        _pluginEvents.Writer.TryComplete();
        _pluginCancellation.Cancel();
        try
        {
            _pluginWorker.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        _pluginCancellation.Dispose();
        _pluginHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task LoadPluginAsync()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        StatusText = $"正在加载 {SelectedPlugin.Name}";
        await _pluginHost.LoadAsync(SelectedPlugin);
        StatusText = $"VST3 音色已加载：{SelectedPlugin.Name}";
    }

    private void EnqueuePluginEvent(PluginMidiEvent midiEvent)
    {
        if (_pluginHost.Snapshot.State != InstrumentHostState.Loaded)
        {
            return;
        }

        if (!_pluginEvents.Writer.TryWrite(midiEvent))
        {
            Dispatcher.UIThread.Post(() => StatusText = "VST3 MIDI 队列已满，已请求释放音符");
            Interlocked.Exchange(ref _resetPluginQueue, 1);
            // 消费者可能刚好读完最后一项；补一个唤醒标记，避免复位一直等到下次按键。
            _pluginEvents.Writer.TryWrite(new(default, Reset: true));
        }
    }

    // 在单一消费任务中保持插件 MIDI 事件顺序
    private async Task ProcessPluginEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var midiEvent in _pluginEvents.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                if (Interlocked.Exchange(ref _resetPluginQueue, 0) != 0)
                {
                    // 溢出后废弃失去配对关系的积压事件，再释放全部按键和踏板。
                    while (_pluginEvents.Reader.TryRead(out _)) { }
                    await _pluginHost.AllNotesOffAsync(cancellationToken);
                    continue;
                }
                if (midiEvent.Reset)
                    await _pluginHost.AllNotesOffAsync(cancellationToken);
                else
                    await _pluginHost.SendMessageAsync(midiEvent.Message, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 退出时 UI 线程可能正在等待 worker，错误提示不能反向等待 UI。
                Dispatcher.UIThread.Post(() => SetError(exception));
            }
        }
    }

    private void SetError(Exception exception) => StatusText = $"VST3 宿主失败：{exception.Message}";

    private readonly record struct PluginMidiEvent(MidiChannelMessage Message, bool Reset = false);
}
