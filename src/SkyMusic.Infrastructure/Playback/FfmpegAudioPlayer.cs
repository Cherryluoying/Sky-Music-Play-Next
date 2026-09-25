// 模块：SkyMusic.Infrastructure FFmpeg 解码与 NAudio 输出
using System.Diagnostics;
using System.Globalization;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SkyMusic.Infrastructure.Media;
using PlaybackState = SkyMusic.Core.Playback.PlaybackState;

namespace SkyMusic.Infrastructure.Playback;

internal sealed class FfmpegAudioPlayer : IDisposable
{
    private static readonly WaveFormat OutputFormat = new(48_000, 16, 2);
    private readonly object _gate = new();
    private readonly string _ffmpegPath;
    private Process? _decoder;
    private WaveOutEvent? _output;
    private CancellationTokenSource? _decoderCancellation;
    private Stopwatch _clock = new();
    private string? _sourcePath;
    private TimeSpan _positionBase;
    private TimeSpan _duration;
    private PlaybackState _state = PlaybackState.Stopped;
    private bool _disposed;
    private float _volume = 1;
    private VolumeSampleProvider? _gain;

    // 增益作用于送往声卡的 PCM，切歌、暂停和定位后也保持用户设定。
    public double Volume
    {
        get { lock (_gate) return _volume; }
        set
        {
            lock (_gate)
            {
                _volume = (float)(double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1);
                if (_gain is not null) _gain.Volume = _volume;
            }
        }
    }

    public FfmpegAudioPlayer(string ffmpegPath)
    {
        _ffmpegPath = Path.GetFullPath(ffmpegPath);
    }

    public PlaybackState State
    {
        get { lock (_gate) return _state; }
    }

    public TimeSpan Duration
    {
        get { lock (_gate) return _duration; }
    }

    public TimeSpan Position
    {
        get
        {
            lock (_gate)
            {
                var position = _state == PlaybackState.Playing
                    ? _positionBase + _clock.Elapsed
                    : _positionBase;
                return _duration > TimeSpan.Zero && position > _duration ? _duration : position;
            }
        }
    }

    // 加载只准备媒体信息；真正解码从播放时开始，避免暂停状态预读丢帧。
    public void Load(string sourcePath, TimeSpan knownDuration, bool autoplay)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            StopDecoder();
            _sourcePath = Path.GetFullPath(sourcePath);
            if (!File.Exists(_sourcePath))
            {
                throw new FileNotFoundException("音频文件不存在。", _sourcePath);
            }
            if (!File.Exists(_ffmpegPath))
            {
                throw new FileNotFoundException("未找到 FFmpeg，请在设置中配置 FFmpeg 路径。", _ffmpegPath);
            }

            // 00:01 是旧界面的未知时长占位值，不能当成真实媒体时长复用。
            var probedDuration = knownDuration > TimeSpan.FromSeconds(1)
                ? TimeSpan.Zero
                : FfmpegMediaDurationProbe.Probe(_ffmpegPath, _sourcePath);
            _duration = probedDuration > TimeSpan.Zero ? probedDuration : knownDuration;
            _positionBase = TimeSpan.Zero;
            _state = PlaybackState.Paused;
            if (autoplay)
            {
                StartDecoder();
            }
        }
    }

    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_sourcePath is null)
            {
                return;
            }
            if (_duration > TimeSpan.Zero && _positionBase >= _duration)
            {
                _positionBase = TimeSpan.Zero;
            }
            if (_state != PlaybackState.Playing)
            {
                StartDecoder();
            }
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_state != PlaybackState.Playing)
            {
                return;
            }
            _positionBase = PositionCore();
            StopDecoder();
            _state = PlaybackState.Paused;
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_gate)
        {
            var wasPlaying = _state == PlaybackState.Playing;
            var upper = _duration > TimeSpan.Zero ? _duration : TimeSpan.MaxValue;
            _positionBase = position < TimeSpan.Zero ? TimeSpan.Zero : position > upper ? upper : position;
            StopDecoder();
            _state = PlaybackState.Paused;
            if (wasPlaying && (_duration <= TimeSpan.Zero || _positionBase < _duration))
            {
                StartDecoder();
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopDecoder();
            _positionBase = TimeSpan.Zero;
            _state = PlaybackState.Stopped;
        }
    }

    // 由统一播放器计时器调用，到达结尾后释放解码进程和音频设备。
    public void CompleteIfNeeded()
    {
        lock (_gate)
        {
            if (_state != PlaybackState.Playing || _duration <= TimeSpan.Zero || PositionCore() < _duration)
            {
                return;
            }
            _positionBase = _duration;
            StopDecoder();
            _state = PlaybackState.Stopped;
        }
    }

    private void StartDecoder()
    {
        StopDecoder();
        var sourcePath = _sourcePath ?? throw new InvalidOperationException("尚未加载音频文件。");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("-hide_banner");
        process.StartInfo.ArgumentList.Add("-loglevel");
        process.StartInfo.ArgumentList.Add("error");
        process.StartInfo.ArgumentList.Add("-nostdin");
        if (_positionBase > TimeSpan.Zero)
        {
            process.StartInfo.ArgumentList.Add("-ss");
            process.StartInfo.ArgumentList.Add(_positionBase.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        }
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(sourcePath);
        process.StartInfo.ArgumentList.Add("-map");
        process.StartInfo.ArgumentList.Add("0:a:0");
        process.StartInfo.ArgumentList.Add("-vn");
        process.StartInfo.ArgumentList.Add("-sn");
        process.StartInfo.ArgumentList.Add("-dn");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("s16le");
        process.StartInfo.ArgumentList.Add("-acodec");
        process.StartInfo.ArgumentList.Add("pcm_s16le");
        process.StartInfo.ArgumentList.Add("-ar");
        process.StartInfo.ArgumentList.Add("48000");
        process.StartInfo.ArgumentList.Add("-ac");
        process.StartInfo.ArgumentList.Add("2");
        process.StartInfo.ArgumentList.Add("pipe:1");
        if (!process.Start())
        {
            throw new IOException("FFmpeg 解码进程无法启动。");
        }

        var buffer = new BufferedWaveProvider(OutputFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(4),
            DiscardOnBufferOverflow = false,
            ReadFully = true
        };
        var output = new WaveOutEvent { DesiredLatency = 120, NumberOfBuffers = 3 };
        output.Init(CreateVolumeProvider(buffer));
        var cancellation = new CancellationTokenSource();
        _decoder = process;
        _output = output;
        _decoderCancellation = cancellation;
        _ = PumpPcmAsync(process, buffer, cancellation.Token);
        _ = process.StandardError.ReadToEndAsync(cancellation.Token);
        output.Play();
        _clock.Restart();
        _state = PlaybackState.Playing;
    }

    // 输出设备前统一施加软件增益；保留独立入口便于直接验证 PCM，不依赖本机声卡。
    internal ISampleProvider CreateVolumeProvider(IWaveProvider source)
    {
        lock (_gate)
        {
            _gain = new VolumeSampleProvider(source.ToSampleProvider()) { Volume = _volume };
            return _gain;
        }
    }

    // 解码线程限制预读量，让 FFmpeg 与声卡消费速度保持同步。
    private static async Task PumpPcmAsync(
        Process process,
        BufferedWaveProvider buffer,
        CancellationToken cancellationToken)
    {
        var bytes = new byte[32 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (buffer.BufferedDuration > TimeSpan.FromSeconds(3))
                {
                    await Task.Delay(20, cancellationToken);
                    continue;
                }
                var read = await process.StandardOutput.BaseStream.ReadAsync(bytes, cancellationToken);
                if (read == 0)
                {
                    break;
                }
                buffer.AddSamples(bytes, 0, read);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or InvalidOperationException)
        {
            // 切歌、暂停和跳转都会主动终止当前解码流。
        }
    }

    private TimeSpan PositionCore()
    {
        var position = _state == PlaybackState.Playing ? _positionBase + _clock.Elapsed : _positionBase;
        return _duration > TimeSpan.Zero && position > _duration ? _duration : position;
    }

    private void StopDecoder()
    {
        _clock.Stop();
        _decoderCancellation?.Cancel();
        _decoderCancellation?.Dispose();
        _decoderCancellation = null;
        try
        {
            _output?.Stop();
        }
        catch (Exception)
        {
            // 输出设备可能已被系统移除，清理流程仍需继续。
        }
        _output?.Dispose();
        _output = null;
        _gain = null;
        if (_decoder is not null)
        {
            try
            {
                if (!_decoder.HasExited)
                {
                    _decoder.Kill(true);
                }
            }
            catch (InvalidOperationException)
            {
            }
            _decoder.Dispose();
            _decoder = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            StopDecoder();
        }
    }
}
