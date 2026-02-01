using System.IO;
using OBSReplay.Host.Services;
using Xunit;

namespace OBSReplay.Host.Tests.Services;

public class ReplayFileServiceTests : IDisposable
{
    private readonly string _testDir;

    public ReplayFileServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "OBSReplayTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void Start_WithValidFolder_DoesNotThrow()
    {
        using var svc = new ReplayFileService();
        svc.Start(_testDir);
        // No exception means success
    }

    [Fact]
    public void Start_WithInvalidFolder_DoesNotThrow()
    {
        using var svc = new ReplayFileService();
        svc.Start(@"C:\NonExistentFolder_12345");
        // Should silently handle non-existent folder
    }

    [Fact]
    public void Start_WithEmptyString_DoesNotThrow()
    {
        using var svc = new ReplayFileService();
        svc.Start("");
    }

    [Fact]
    public void Restart_SwitchesWatchFolder()
    {
        using var svc = new ReplayFileService();
        svc.Start(_testDir);

        var dir2 = Path.Combine(Path.GetTempPath(), "OBSReplayTests2_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir2);
        try
        {
            svc.Restart(dir2);
            // No exception means success
        }
        finally
        {
            Directory.Delete(dir2, true);
        }
    }

    [Fact]
    public void PendingGame_DefaultsToNull()
    {
        using var svc = new ReplayFileService();
        Assert.Null(svc.PendingGame);
    }

    [Fact]
    public void PendingGame_CanBeSetAndRead()
    {
        using var svc = new ReplayFileService();
        svc.PendingGame = "Cyberpunk2077";
        Assert.Equal("Cyberpunk2077", svc.PendingGame);
    }

    [Fact]
    public void WaitForFileComplete_NonExistentFile_ReturnsFalse()
    {
        var fakePath = Path.Combine(_testDir, "nonexistent.mp4");
        Assert.False(ReplayFileService.WaitForFileComplete(fakePath));
    }

    [Fact]
    public void WaitForFileComplete_StableFile_ReturnsTrue()
    {
        var testFile = Path.Combine(_testDir, "test_replay.mp4");
        File.WriteAllBytes(testFile, new byte[1024]);

        // File is already stable (not being written), should return true
        Assert.True(ReplayFileService.WaitForFileComplete(testFile));
    }

    [Fact]
    public void Dispose_DoesNotThrowWhenCalledMultipleTimes()
    {
        var svc = new ReplayFileService();
        svc.Start(_testDir);
        svc.Dispose();
        svc.Dispose(); // second dispose should not throw
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); }
        catch { /* cleanup best-effort */ }
    }
}
