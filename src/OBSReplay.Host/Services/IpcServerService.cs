using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using OBSReplay.Host.Models;

namespace OBSReplay.Host.Services;

public class IpcServerService : IDisposable
{
    private NamedPipeServerStream? _pipe;
    private CancellationTokenSource? _cts;
    private Thread? _readerThread;
    private readonly object _writeLock = new();
    private volatile bool _clientConnected;

    public bool IsClientConnected => _clientConnected;

    public event Action<IpcMessage>? MessageReceived;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _readerThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "IPC-Server"
        };
        _readerThread.Start();
    }

    private void ListenLoop()
    {
        while (_cts is { IsCancellationRequested: false })
        {
            try
            {
                _pipe?.Dispose();
                _pipe = new NamedPipeServerStream(
                    Constants.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                Debug.WriteLine("IPC: Waiting for overlay connection...");
                _pipe.WaitForConnection();
                _clientConnected = true;
                Debug.WriteLine("IPC: Overlay connected.");

                ReadMessages();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IPC listen error: {ex.Message}");
                _clientConnected = false;
                Thread.Sleep(1000); // backoff before retry
            }
        }
    }

    private void ReadMessages()
    {
        try
        {
            while (_pipe is { IsConnected: true } && _cts is { IsCancellationRequested: false })
            {
                var msg = ReadMessage();
                if (msg != null)
                    MessageReceived?.Invoke(msg);
                else
                    break; // pipe closed
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC read error: {ex.Message}");
        }
        finally
        {
            _clientConnected = false;
            Debug.WriteLine("IPC: Overlay disconnected.");
        }
    }

    private IpcMessage? ReadMessage()
    {
        if (_pipe == null) return null;

        // Read 4-byte length prefix (little-endian)
        var lenBuf = new byte[4];
        var bytesRead = ReadExact(_pipe, lenBuf, 4);
        if (bytesRead < 4) return null;

        var length = BitConverter.ToInt32(lenBuf, 0);
        if (length <= 0 || length > 10 * 1024 * 1024) // 10MB safety limit
            return null;

        // Read JSON body
        var bodyBuf = new byte[length];
        bytesRead = ReadExact(_pipe, bodyBuf, length);
        if (bytesRead < length) return null;

        var json = Encoding.UTF8.GetString(bodyBuf);
        return JsonSerializer.Deserialize<IpcMessage>(json);
    }

    private static int ReadExact(Stream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, totalRead, count - totalRead);
            if (read == 0) return totalRead; // pipe closed
            totalRead += read;
        }
        return totalRead;
    }

    public bool SendMessage(IpcMessage message)
    {
        if (_pipe == null || !_clientConnected)
            return false;

        try
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);
            var lenPrefix = BitConverter.GetBytes(body.Length);

            lock (_writeLock)
            {
                _pipe.Write(lenPrefix, 0, 4);
                _pipe.Write(body, 0, body.Length);
                _pipe.Flush();
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC send error: {ex.Message}");
            _clientConnected = false;
            return false;
        }
    }

    public bool SendStateUpdate(ObsState state)
    {
        return SendMessage(IpcMessage.Create("state_update", state));
    }

    public bool SendPreviewFrame(string base64Data)
    {
        return SendMessage(IpcMessage.Create("preview_frame", new { base64 = base64Data }));
    }

    public bool SendShowOverlay() => SendMessage(IpcMessage.Create("show_overlay"));
    public bool SendHideOverlay() => SendMessage(IpcMessage.Create("hide_overlay"));
    public bool SendShutdown() => SendMessage(IpcMessage.Create("shutdown"));

    public bool SendNotification(string text, string color, double duration)
    {
        return SendMessage(IpcMessage.Create("show_notification", new { text, color, duration }));
    }

    public bool SendRecIndicator(bool active)
    {
        return SendMessage(IpcMessage.Create("rec_indicator", new { active }));
    }

    public bool SendAudioAdvanced(List<AudioAdvancedInfo> info)
    {
        return SendMessage(IpcMessage.Create("audio_advanced", info));
    }

    public bool SendInputKinds(List<string> kinds)
    {
        return SendMessage(IpcMessage.Create("input_kinds", kinds));
    }

    public bool SendFilters(List<FilterInfo> filters)
    {
        return SendMessage(IpcMessage.Create("filters_response", filters));
    }

    public bool SendFilterKinds(List<string> kinds)
    {
        return SendMessage(IpcMessage.Create("filter_kinds", kinds));
    }

    public bool SendStats(ObsStatsData stats)
    {
        return SendMessage(IpcMessage.Create("stats_response", stats));
    }

    public bool SendHotkeys(List<string> hotkeys)
    {
        return SendMessage(IpcMessage.Create("hotkeys_response", hotkeys));
    }

    public bool SendConfigUpdate(AppConfig config)
    {
        return SendMessage(IpcMessage.Create("config_update", new
        {
            toggleHotkey = config.ToggleHotkey,
            saveHotkey = config.SaveHotkey,
            recIndicatorPosition = config.RecIndicatorPosition,
            showRecIndicator = config.ShowRecIndicator,
            showNotifications = config.ShowNotifications,
            notificationDuration = config.NotificationDuration,
            notificationMessage = config.NotificationMessage,
        }));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _pipe?.Dispose(); } catch { /* ignore */ }
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
