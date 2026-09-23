// 模块：SkyMusic.Infrastructure 通用模型 NativeInstrumentPreviewService
using System.Collections.Concurrent;
using SkyMusic.Core.GameScores;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Audio;

public sealed class NativeInstrumentPreviewService(IInstrumentAssetCatalog assets) : IInstrumentPreviewService
{
    private const int MaximumSamples = 1024;
    private readonly ConcurrentDictionary<string, Lazy<Task<uint>>> _sampleSlots =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _decodeGate = new(2, 2);
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _nativeGate = new();
    private IntPtr _preview;
    private int _nextSlot = -1;
    private int _disposeStarted;
    private bool _disposed;

    // 在非实时线程预解码当前乐器采样
    public async ValueTask PreloadAsync(
        GameScoreProfile profile,
        string instrumentId,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var instrument = assets.Find(profile, instrumentId);
            if (instrument is null)
                return;

            var loads = instrument.SamplePaths.Select(path => _sampleSlots.GetOrAdd(
                path,
                samplePath => new Lazy<Task<uint>>(
                    () => LoadAsync(samplePath, cancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value);
            await Task.WhenAll(loads).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    // 触发已缓存采样并在缺失时按需加载
    public async ValueTask PlayAsync(
        GameScoreProfile profile,
        string instrumentId,
        int noteIndex,
        int volume,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var path = assets.GetSamplePath(profile, instrumentId, noteIndex);
            if (path is null)
                throw new FileNotFoundException("乐器采样不存在", path);

            var slot = await _sampleSlots.GetOrAdd(
                path,
                samplePath => new Lazy<Task<uint>>(
                    () => LoadAsync(samplePath, cancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var gain = Math.Clamp(volume / 127f, 0f, 1f);
            if (Trigger(slot, gain) != 0)
                throw new IOException("乐器采样无法播放");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    // 限制并发解码数量以避免切换乐器时阻塞
    private async Task<uint> LoadAsync(string path, CancellationToken cancellationToken)
    {
        await _decodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var slot = Interlocked.Increment(ref _nextSlot);
            if (slot >= MaximumSamples)
                throw new InvalidOperationException("乐器采样缓存已满");

            var result = await Task.Run(
                () => Load((uint)slot, path),
                cancellationToken).ConfigureAwait(false);
            if (result != 0)
                throw new InvalidDataException($"无法解码乐器采样 {Path.GetFileName(path)}");
            return (uint)slot;
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    private IntPtr GetPreview()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_preview != IntPtr.Zero)
            return _preview;

        if (NativeAudioPreview.skymusic_audio_preview_abi_version() != NativeAudioPreview.AbiVersion)
            throw new NotSupportedException("SkyMusic.AudioPreview ABI 不兼容");
        _preview = NativeAudioPreview.skymusic_audio_preview_create();
        if (_preview == IntPtr.Zero)
            throw new IOException("无法启动音频预览设备");
        return _preview;
    }

    private int Load(uint slot, string path)
    {
        lock (_nativeGate)
            return NativeAudioPreview.skymusic_audio_preview_load(GetPreview(), slot, path);
    }

    private int Trigger(uint slot, float gain)
    {
        lock (_nativeGate)
            return NativeAudioPreview.skymusic_audio_preview_trigger(GetPreview(), slot, gain);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        _operationGate.Wait();
        try
        {
            lock (_nativeGate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                if (_preview != IntPtr.Zero)
                {
                    NativeAudioPreview.skymusic_audio_preview_destroy(_preview);
                    _preview = IntPtr.Zero;
                }
            }
        }
        finally
        {
            _operationGate.Release();
            _operationGate.Dispose();
            _decodeGate.Dispose();
        }
    }
}
