using System.Diagnostics;
using System.IO;
using ReplayOverlay.Host.Models;

namespace ReplayOverlay.Host.Services;

public class OverlayProcessService : IDisposable
{
    private Process? _process;
    private readonly string _exePath;
    private volatile bool _shouldRun;
    private readonly object _lock = new();

    public bool IsRunning => _process is { HasExited: false };

    public OverlayProcessService()
    {
        // Look for the overlay exe next to the host exe
        var hostDir = AppDomain.CurrentDomain.BaseDirectory;
        _exePath = Path.Combine(hostDir, Constants.OverlayExeName);
    }

    public void Start()
    {
        lock (_lock)
        {
            if (IsRunning)
                return;

            if (!File.Exists(_exePath))
            {
                Debug.WriteLine($"Overlay executable not found: {_exePath}");
                return;
            }

            _shouldRun = true;

            try
            {
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _exePath,
                        Arguments = $"--pipe {Constants.PipeName}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                _process.Exited += OnProcessExited;
                _process.Start();
                Debug.WriteLine($"Overlay process started (PID: {_process.Id})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start overlay: {ex.Message}");
            }
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        Debug.WriteLine("Overlay process exited.");

        if (_shouldRun)
        {
            // Auto-restart after short delay
            Task.Delay(2000).ContinueWith(_ =>
            {
                if (_shouldRun)
                {
                    Debug.WriteLine("Auto-restarting overlay...");
                    Start();
                }
            });
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _shouldRun = false;

            if (_process is { HasExited: false })
            {
                try
                {
                    // Give it a moment to exit gracefully (IPC shutdown sent by caller)
                    if (!_process.WaitForExit(3000))
                    {
                        _process.Kill();
                        _process.WaitForExit(2000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error stopping overlay: {ex.Message}");
                }
            }
        }
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Dispose()
    {
        Stop();
        lock (_lock) { _process?.Dispose(); }
        GC.SuppressFinalize(this);
    }
}
