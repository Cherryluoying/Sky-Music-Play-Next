// 模块：SkyMusic.Infrastructure 插件领域 Vst3ProcessHost
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkyMusic.Core.Plugins;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Plugins;

public sealed class Vst3ProcessHost : IInstrumentPluginHost
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly Vst3PluginCatalog _catalog;
    private readonly Vst3ProcessHostOptions _options;
    private Process? _process;
    private Task<string>? _standardError;
    private long _requestId;
    private InstrumentHostSnapshot _snapshot = new(InstrumentHostState.Stopped);
    private bool _disposed;

    public Vst3ProcessHost(Vst3ProcessHostOptions options, Vst3PluginCatalog? catalog = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _catalog = catalog ?? new Vst3PluginCatalog();
    }

    public InstrumentHostSnapshot Snapshot => _snapshot;

    public event Action<InstrumentHostSnapshot>? Changed;

    public IReadOnlyList<InstrumentPluginInfo> DiscoverPlugins(IEnumerable<string> searchPaths) =>
        _catalog.Discover(searchPaths);

    // 启动隔离宿主并加载选中的 VST3 插件
    public async ValueTask LoadAsync(InstrumentPluginInfo plugin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (!string.Equals(plugin.Format, "VST3", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported instrument plugin format: {plugin.Format}");
        }

        UpdateSnapshot(new InstrumentHostSnapshot(InstrumentHostState.Starting));
        try
        {
            await SendAsync(new HostRequest(NextId(), "load", plugin.Path), cancellationToken);
            UpdateSnapshot(new InstrumentHostSnapshot(InstrumentHostState.Loaded, plugin));
        }
        catch (Exception exception)
        {
            UpdateSnapshot(new InstrumentHostSnapshot(InstrumentHostState.Faulted, Error: exception.Message));
            throw;
        }
    }

    public async ValueTask UnloadAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null || _process.HasExited)
        {
            UpdateSnapshot(new InstrumentHostSnapshot(InstrumentHostState.Stopped));
            return;
        }

        await SendAsync(new HostRequest(NextId(), "unload"), cancellationToken);
        UpdateSnapshot(new InstrumentHostSnapshot(InstrumentHostState.Ready));
    }

    public ValueTask NoteOnAsync(
        int note,
        byte velocity = 100,
        int channel = 0,
        CancellationToken cancellationToken = default) =>
        SendMidiAsync("noteOn", note, velocity, channel, cancellationToken);

    public ValueTask NoteOffAsync(
        int note,
        byte velocity = 0,
        int channel = 0,
        CancellationToken cancellationToken = default) =>
        SendMidiAsync("noteOff", note, velocity, channel, cancellationToken);

    public async ValueTask AllNotesOffAsync(CancellationToken cancellationToken = default)
    {
        EnsurePluginLoaded();
        await SendAsync(new HostRequest(NextId(), "allNotesOff"), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_process is { HasExited: false })
        {
            try
            {
                await SendAsync(new HostRequest(NextId(), "shutdown"), CancellationToken.None, allowDisposed: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
                TryKillHost();
            }
        }

        _process?.Dispose();
        _commandGate.Dispose();
        UpdateSnapshot(new InstrumentHostSnapshot(InstrumentHostState.Stopped));
    }

    // 将 MIDI 消息串行发送到插件宿主
    private async ValueTask SendMidiAsync(
        string type,
        int note,
        byte velocity,
        int channel,
        CancellationToken cancellationToken)
    {
        EnsurePluginLoaded();
        if (note is < 0 or > 127)
        {
            throw new ArgumentOutOfRangeException(nameof(note));
        }

        if (channel is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        await SendAsync(new HostRequest(NextId(), type, Note: note, Velocity: velocity, Channel: channel), cancellationToken);
    }

    // 通过标准输入输出完成带编号的请求响应
    private async ValueTask SendAsync(
        HostRequest request,
        CancellationToken cancellationToken,
        bool allowDisposed = false)
    {
        if (!allowDisposed)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        await _commandGate.WaitAsync(cancellationToken);
        try
        {
            var process = EnsureHostStarted();
            using var timeout = new CancellationTokenSource(_options.EffectiveCommandTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions).AsMemory(), linked.Token);
                await process.StandardInput.FlushAsync(linked.Token);
                var response = await ReadResponseAsync(process, request.Id, linked.Token);

                if (!response.Ok)
                {
                    throw new InvalidOperationException(response.Error ?? "VST3 host command failed");
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                TryKillHost();
                throw new TimeoutException($"VST3 host command '{request.Type}' timed out");
            }
            catch
            {
                if (process.HasExited)
                {
                    TryKillHost();
                }

                throw;
            }
        }
        finally
        {
            _commandGate.Release();
        }
    }

    // 复用存活宿主或创建新的隔离进程
    private Process EnsureHostStarted()
    {
        if (_process is { HasExited: false })
        {
            return _process;
        }

        var executablePath = Path.GetFullPath(_options.HostExecutablePath);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Native VST3 host executable was not found", executablePath);
        }

        _process?.Dispose();
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        if (!_process.Start())
        {
            throw new InvalidOperationException("Native VST3 host could not be started");
        }

        _standardError = _process.StandardError.ReadToEndAsync();
        return _process;
    }

    private async Task<string> GetHostExitMessageAsync(Process process)
    {
        await process.WaitForExitAsync();
        var error = _standardError is null ? string.Empty : await _standardError;
        return string.IsNullOrWhiteSpace(error)
            ? $"VST3 host exited unexpectedly with code {process.ExitCode}"
            : $"VST3 host exited unexpectedly with code {process.ExitCode}: {error.Trim()}";
    }

    private async Task<HostResponse> ReadResponseAsync(
        Process process,
        long requestId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                throw new InvalidOperationException(await GetHostExitMessageAsync(process));
            }

            try
            {
                var response = JsonSerializer.Deserialize<HostResponse>(line, JsonOptions);
                if (response is not null && response.Id == requestId)
                {
                    return response;
                }
            }
            catch (JsonException)
            {
                // 部分第三方插件会向宿主标准输出写日志
            }
        }
    }

    private void TryKillHost()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(true);
                _process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            _standardError = null;
        }
    }

    private void EnsurePluginLoaded()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_snapshot.State != InstrumentHostState.Loaded)
        {
            throw new InvalidOperationException("No VST3 instrument is loaded");
        }
    }

    private long NextId() => Interlocked.Increment(ref _requestId);

    private void UpdateSnapshot(InstrumentHostSnapshot snapshot)
    {
        _snapshot = snapshot;
        Changed?.Invoke(snapshot);
    }

    private sealed record HostRequest(
        long Id,
        string Type,
        string? Path = null,
        int? Note = null,
        byte? Velocity = null,
        int? Channel = null);

    private sealed record HostResponse(long Id, bool Ok, string? Name = null, string? Error = null);
}
