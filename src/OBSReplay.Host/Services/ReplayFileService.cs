using System.Diagnostics;
using System.IO;
using OBSReplay.Host.Models;

namespace OBSReplay.Host.Services;

public class ReplayFileService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly HashSet<string> _recentFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// Set by GameDetectionService before a replay save. The next created video file
    /// will be moved into a subfolder with this name.
    /// Accessed from multiple threads; use Interlocked.Exchange for atomic read-and-clear.
    /// </summary>
    private volatile string? _pendingGame;
    public string? PendingGame
    {
        get => _pendingGame;
        set => _pendingGame = value;
    }

    public void Start(string watchFolder)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(watchFolder) || !Directory.Exists(watchFolder))
        {
            Debug.WriteLine($"Watch folder does not exist: {watchFolder}");
            return;
        }

        _watcher = new FileSystemWatcher(watchFolder)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnFileCreated;
        Debug.WriteLine($"Watching: {watchFolder}");
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public void Restart(string newFolder)
    {
        Stop();
        Start(newFolder);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (Directory.Exists(e.FullPath)) return; // skip directories

            var ext = Path.GetExtension(e.FullPath);
            if (!Constants.VideoExtensions.Contains(ext)) return;

            // Dedup: skip if recently seen
            lock (_lock)
            {
                if (_recentFiles.Contains(e.FullPath)) return;
                _recentFiles.Add(e.FullPath);
            }

            // Schedule cleanup of recent file entry
            _ = Task.Delay(TimeSpan.FromSeconds(Constants.RecentFileCleanupS)).ContinueWith(_ =>
            {
                lock (_lock) { _recentFiles.Remove(e.FullPath); }
            });

            // Organize into game subfolder (atomic read-and-clear to prevent two
            // FileCreated events from both reading the same pending game)
            var gameName = Interlocked.Exchange(ref _pendingGame, null);

            if (!string.IsNullOrEmpty(gameName))
            {
                // Run file move on background thread
                Task.Run(() => MoveToGameFolder(e.FullPath, gameName));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"File event error: {ex.Message}");
        }
    }

    private void MoveToGameFolder(string filePath, string gameName)
    {
        try
        {
            if (!WaitForFileComplete(filePath))
            {
                Debug.WriteLine($"File did not stabilize: {filePath}");
                return;
            }

            var dir = Path.GetDirectoryName(filePath);
            if (dir == null) return;

            // Sanitize game name for filesystem
            var safeName = SanitizeFolderName(gameName);
            var gameDir = Path.Combine(dir, safeName);
            Directory.CreateDirectory(gameDir);

            var destPath = Path.Combine(gameDir, Path.GetFileName(filePath));
            if (File.Exists(destPath))
            {
                Debug.WriteLine($"Destination already exists: {destPath}");
                return;
            }

            File.Move(filePath, destPath);
            Debug.WriteLine($"Moved replay to: {destPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to move replay: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for a file to stop being written to by monitoring size stability.
    /// Returns true if the file is stable, false on timeout.
    /// </summary>
    internal static bool WaitForFileComplete(string path)
    {
        long lastSize = -1;
        int stableCount = 0;
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalSeconds < Constants.FileCompletionTimeoutS)
        {
            if (!File.Exists(path)) return false;

            try
            {
                var info = new FileInfo(path);
                long currentSize = info.Length;

                if (currentSize == lastSize && currentSize > 0)
                {
                    stableCount++;
                    if (stableCount >= Constants.FileStableChecks)
                        return true;
                }
                else
                {
                    stableCount = 0;
                }

                lastSize = currentSize;
            }
            catch (Exception ex)
            {
                // File may be locked; keep waiting
                Debug.WriteLine($"WaitForFileComplete: File access error (retrying): {ex.Message}");
            }

            Thread.Sleep(TimeSpan.FromSeconds(Constants.FilePollIntervalS));
        }

        return false;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        // Prevent path traversal
        sanitized = sanitized.Replace("..", "").Trim();
        return string.IsNullOrEmpty(sanitized) ? "Desktop" : sanitized;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
