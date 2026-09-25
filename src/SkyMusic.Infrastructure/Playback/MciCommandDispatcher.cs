// 模块：系统 MIDI 命令线程，确保设备在同一线程打开、控制和关闭。
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace SkyMusic.Infrastructure.Playback;

internal interface IMciCommands : IDisposable
{
    string Execute(string command);
}

internal sealed class MciCommandDispatcher : IMciCommands
{
    private readonly BlockingCollection<(string Command, TaskCompletionSource<string> Completion)> _commands = new();
    private readonly Thread _thread;
    private readonly Func<string, string> _send;

    internal MciCommandDispatcher(Func<string, string>? send = null)
    {
        _send = send ?? SendNative;
        _thread = new Thread(Run) { IsBackground = true, Name = "SkyMusic.SystemMidi" };
        _thread.Start();
    }

    // 异步播放操作可能在不同线程恢复；设备命令始终交给固定线程处理。
    public string Execute(string command)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Add((command, completion));
        return completion.Task.GetAwaiter().GetResult();
    }

    private void Run()
    {
        foreach (var (command, completion) in _commands.GetConsumingEnumerable())
        {
            try { completion.SetResult(_send(command)); }
            catch (Exception error) { completion.SetException(error); }
        }
    }

    // 不吞掉停止/关闭错误：关闭未确认时，调用者不能启用第二个音源。
    private static string SendNative(string command)
    {
        var result = new StringBuilder(256);
        var error = MciSendString(command, result, result.Capacity, IntPtr.Zero);
        if (error == 0) return result.ToString();
        var message = new StringBuilder(256);
        MciGetErrorString(error, message, message.Capacity);
        throw new IOException($"系统 MIDI 命令失败（{error}）：{message}");
    }

    public void Dispose()
    {
        _commands.CompleteAdding();
        _thread.Join();
        _commands.Dispose();
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
    private static extern int MciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciGetErrorStringW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MciGetErrorString(int errorCode, StringBuilder errorText, int errorTextSize);
}
