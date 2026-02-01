using OBSReplay.Host.Services;
using Xunit;

namespace OBSReplay.Host.Tests.Services;

public class HotkeyServiceTests
{
    [Theory]
    [InlineData("f1", 0u, 0x70u)]
    [InlineData("f9", 0u, 0x78u)]
    [InlineData("f10", 0u, 0x79u)]
    [InlineData("f12", 0u, 0x7Bu)]
    [InlineData("f24", 0u, 0x87u)]
    public void ParseHotkey_FunctionKeys(string key, uint expectedMod, uint expectedVk)
    {
        bool result = HotkeyService.ParseHotkey(key, out uint mod, out uint vk);
        Assert.True(result);
        Assert.Equal(expectedMod, mod);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("num add", 0x6Bu)]
    [InlineData("num subtract", 0x6Du)]
    [InlineData("num multiply", 0x6Au)]
    [InlineData("num divide", 0x6Fu)]
    [InlineData("num 0", 0x60u)]
    [InlineData("num 5", 0x65u)]
    [InlineData("num 9", 0x69u)]
    public void ParseHotkey_NumpadKeys(string key, uint expectedVk)
    {
        bool result = HotkeyService.ParseHotkey(key, out _, out uint vk);
        Assert.True(result);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("space", 0x20u)]
    [InlineData("enter", 0x0Du)]
    [InlineData("escape", 0x1Bu)]
    [InlineData("tab", 0x09u)]
    [InlineData("delete", 0x2Eu)]
    [InlineData("insert", 0x2Du)]
    [InlineData("home", 0x24u)]
    [InlineData("end", 0x23u)]
    [InlineData("pageup", 0x21u)]
    [InlineData("pagedown", 0x22u)]
    [InlineData("up", 0x26u)]
    [InlineData("printscreen", 0x2Cu)]
    public void ParseHotkey_CommonKeys(string key, uint expectedVk)
    {
        bool result = HotkeyService.ParseHotkey(key, out _, out uint vk);
        Assert.True(result);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("z")]
    [InlineData("0")]
    [InlineData("9")]
    public void ParseHotkey_SingleCharKey_RejectedTooShort(string key)
    {
        // Single characters are < 2 chars, rejected as standalone hotkeys
        // (would conflict with normal typing)
        bool result = HotkeyService.ParseHotkey(key, out _, out _);
        Assert.False(result);
    }

    [Theory]
    [InlineData("ctrl+a", (uint)'A')]
    [InlineData("ctrl+z", (uint)'Z')]
    [InlineData("alt+0", (uint)'0')]
    [InlineData("shift+9", (uint)'9')]
    public void ParseHotkey_SingleCharWithModifier(string key, uint expectedVk)
    {
        bool result = HotkeyService.ParseHotkey(key, out _, out uint vk);
        Assert.True(result);
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void ParseHotkey_CtrlModifier()
    {
        bool result = HotkeyService.ParseHotkey("ctrl+s", out uint mod, out uint vk);
        Assert.True(result);
        Assert.Equal(NativeMethods.MOD_CONTROL, mod);
        Assert.Equal((uint)'S', vk);
    }

    [Fact]
    public void ParseHotkey_MultipleModifiers()
    {
        bool result = HotkeyService.ParseHotkey("ctrl+shift+f5", out uint mod, out uint vk);
        Assert.True(result);
        Assert.Equal(NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, mod);
        Assert.Equal(0x74u, vk); // VK_F5
    }

    [Fact]
    public void ParseHotkey_AltModifier()
    {
        bool result = HotkeyService.ParseHotkey("alt+f4", out uint mod, out uint vk);
        Assert.True(result);
        Assert.Equal(NativeMethods.MOD_ALT, mod);
        Assert.Equal(0x73u, vk); // VK_F4
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")] // too short (< 2 chars)
    [InlineData(null)]
    public void ParseHotkey_InvalidReturnsFalse(string? key)
    {
        bool result = HotkeyService.ParseHotkey(key!, out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void ParseHotkey_UnknownKeyReturnsZeroVk()
    {
        bool result = HotkeyService.ParseHotkey("nonsensekey", out _, out uint vk);
        Assert.False(result);
        Assert.Equal(0u, vk);
    }

    [Fact]
    public void ParseHotkey_CaseInsensitive()
    {
        HotkeyService.ParseHotkey("F10", out _, out uint vkUpper);
        HotkeyService.ParseHotkey("f10", out _, out uint vkLower);
        Assert.Equal(vkUpper, vkLower);
    }

    [Fact]
    public void ParseHotkey_ControlAlias()
    {
        HotkeyService.ParseHotkey("ctrl+a", out uint mod1, out _);
        HotkeyService.ParseHotkey("control+a", out uint mod2, out _);
        Assert.Equal(mod1, mod2);
    }
}
