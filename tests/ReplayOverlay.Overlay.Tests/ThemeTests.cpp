#include <gtest/gtest.h>
#include "Theme.h"
#include <cmath>

static bool ColorApproxEqual(const Theme::Color& c, float r, float g, float b, float tolerance = 0.01f)
{
    return std::abs(c.r - r) < tolerance &&
           std::abs(c.g - g) < tolerance &&
           std::abs(c.b - b) < tolerance;
}

TEST(Theme, BackgroundMatchesHex1a1a2e)
{
    // #1a1a2e -> RGB(26, 26, 46) -> (0.102, 0.102, 0.180)
    EXPECT_TRUE(ColorApproxEqual(Theme::Background, 0.102f, 0.102f, 0.180f));
}

TEST(Theme, AccentMatchesHex4ecca3)
{
    // #4ecca3 -> RGB(78, 204, 163) -> (0.306, 0.800, 0.639)
    EXPECT_TRUE(ColorApproxEqual(Theme::Accent, 0.306f, 0.800f, 0.639f));
}

TEST(Theme, AlertMatchesHexe94560)
{
    // #e94560 -> RGB(233, 69, 96) -> (0.914, 0.271, 0.376)
    EXPECT_TRUE(ColorApproxEqual(Theme::Alert, 0.914f, 0.271f, 0.376f));
}

TEST(Theme, AllColorsHaveFullAlpha)
{
    EXPECT_FLOAT_EQ(Theme::Background.a, 1.0f);
    EXPECT_FLOAT_EQ(Theme::Secondary.a, 1.0f);
    EXPECT_FLOAT_EQ(Theme::Border.a, 1.0f);
    EXPECT_FLOAT_EQ(Theme::Text.a, 1.0f);
    EXPECT_FLOAT_EQ(Theme::TextSecondary.a, 1.0f);
    EXPECT_FLOAT_EQ(Theme::Accent.a, 1.0f);
    EXPECT_FLOAT_EQ(Theme::Alert.a, 1.0f);
    EXPECT_FLOAT_EQ(Theme::Warning.a, 1.0f);
}
