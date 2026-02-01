using OBSReplay.Host.Services;
using Xunit;

namespace OBSReplay.Host.Tests.Services;

public class GameDetectionServiceTests
{
    [Theory]
    [InlineData("explorer")]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("msedge")]
    [InlineData("discord")]
    [InlineData("obs64")]
    [InlineData("obs32")]
    [InlineData("code")]
    [InlineData("devenv")]
    [InlineData("slack")]
    [InlineData("spotify")]
    public void IsIgnored_ReturnsTrueForKnownProcesses(string processName)
    {
        Assert.True(GameDetectionService.IsIgnored(processName));
    }

    [Theory]
    [InlineData("Explorer")] // case-insensitive
    [InlineData("CHROME")]
    [InlineData("OBS64")]
    public void IsIgnored_IsCaseInsensitive(string processName)
    {
        Assert.True(GameDetectionService.IsIgnored(processName));
    }

    [Theory]
    [InlineData("Cyberpunk2077")]
    [InlineData("csgo")]
    [InlineData("Overwatch")]
    [InlineData("minecraft")]
    [InlineData("League of Legends")]
    [InlineData("valorant")]
    public void IsIgnored_ReturnsFalseForGames(string processName)
    {
        Assert.False(GameDetectionService.IsIgnored(processName));
    }

    [Fact]
    public void PrepareGameFolder_ReturnNullWhenDisabled()
    {
        var svc = new GameDetectionService();
        var config = new OBSReplay.Host.Models.AppConfig { OrganizeByGame = false };
        Assert.Null(svc.PrepareGameFolder(config));
    }

    [Fact]
    public void PrepareGameFolder_FallbackToDesktop()
    {
        var svc = new GameDetectionService();
        var config = new OBSReplay.Host.Models.AppConfig { OrganizeByGame = true };
        // No foreground game detected and no last game, should fallback
        var result = svc.PrepareGameFolder(config);
        // Result is either a detected process, last game, or "Desktop" fallback
        Assert.NotNull(result);
    }

    [Fact]
    public void LastGame_InitiallyNull()
    {
        var svc = new GameDetectionService();
        Assert.Null(svc.LastGame);
    }
}
