using ReplayOverlay.Host.Services;
using Xunit;

namespace ReplayOverlay.Host.Tests.Services;

public class AudioMathServiceTests
{
    [Fact]
    public void MulToFader_ZeroReturnsZero()
    {
        Assert.Equal(0, AudioMathService.MulToFader(0.0));
    }

    [Fact]
    public void MulToFader_NegativeReturnsZero()
    {
        Assert.Equal(0, AudioMathService.MulToFader(-1.0));
    }

    [Fact]
    public void MulToFader_UnityReturnsApprox98()
    {
        // 1.0 linear = 0 dB. normalized = 96/102 ≈ 0.941, cubic root ≈ 0.980, * 100 = 98
        int result = AudioMathService.MulToFader(1.0);
        Assert.InRange(result, 97, 99);
    }

    [Fact]
    public void MulToFader_MaxReturns100()
    {
        // +6 dB = 10^(6/20) ≈ 1.995
        double maxMul = System.Math.Pow(10.0, 6.0 / 20.0);
        Assert.Equal(100, AudioMathService.MulToFader(maxMul));
    }

    [Fact]
    public void MulToFader_VeryLargeReturns100()
    {
        Assert.Equal(100, AudioMathService.MulToFader(100.0));
    }

    [Fact]
    public void FaderToMul_ZeroReturnsZero()
    {
        Assert.Equal(0.0, AudioMathService.FaderToMul(0));
    }

    [Fact]
    public void FaderToMul_100ReturnsMaxMul()
    {
        double expected = System.Math.Pow(10.0, 6.0 / 20.0); // ~1.995
        double result = AudioMathService.FaderToMul(100);
        Assert.InRange(result, expected - 0.01, expected + 0.01);
    }

    [Fact]
    public void RoundTrip_FaderToMulToFader()
    {
        // For values 10, 25, 50, 75, 90 - round trip should be within ±1
        foreach (int fader in new[] { 10, 25, 50, 75, 90 })
        {
            double mul = AudioMathService.FaderToMul(fader);
            int roundTrip = AudioMathService.MulToFader(mul);
            Assert.InRange(roundTrip, fader - 1, fader + 1);
        }
    }

    [Fact]
    public void FaderToMul_50PercentIsNotHalfLinear()
    {
        // Due to cubic curve, 50% fader should NOT be 0.5 linear
        // 50% fader -> 0.125 normalized -> -83.25 dB -> very small multiplier
        double mul = AudioMathService.FaderToMul(50);
        Assert.NotEqual(0.5, mul, 2);
        Assert.True(mul < 0.01, $"50% fader gave {mul}, expected < 0.01 (deep in the quiet range)");
    }

    [Fact]
    public void MulToFader_MonotonicallyIncreasing()
    {
        int prev = -1;
        for (double mul = 0.0; mul <= 2.0; mul += 0.1)
        {
            int fader = AudioMathService.MulToFader(mul);
            Assert.True(fader >= prev, $"Not monotonic at mul={mul}: {fader} < {prev}");
            prev = fader;
        }
    }
}
