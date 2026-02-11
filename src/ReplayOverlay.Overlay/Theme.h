#pragma once

// Theme color constants - used by C++ code for notification color parsing, etc.
// CSS styling is handled in RmlUi RCSS files.
namespace Theme
{
    struct Color { float r, g, b, a; };

    constexpr Color Background    = { 0.102f, 0.102f, 0.180f, 1.0f }; // #1a1a2e
    constexpr Color Secondary     = { 0.086f, 0.129f, 0.243f, 1.0f }; // #16213e
    constexpr Color Border        = { 0.173f, 0.243f, 0.314f, 1.0f }; // #2c3e50
    constexpr Color Text          = { 0.918f, 0.918f, 0.918f, 1.0f }; // #eaeaea
    constexpr Color TextSecondary = { 0.498f, 0.549f, 0.553f, 1.0f }; // #7f8c8d
    constexpr Color Accent        = { 0.306f, 0.800f, 0.639f, 1.0f }; // #4ecca3
    constexpr Color Alert         = { 0.914f, 0.271f, 0.376f, 1.0f }; // #e94560
    constexpr Color Warning       = { 0.953f, 0.612f, 0.071f, 1.0f }; // #f39c12
    constexpr Color Black         = { 0.0f,   0.0f,   0.0f,   1.0f };

    // Derived button colors
    constexpr Color ButtonBg      = Secondary;
    constexpr Color ButtonHover   = { 0.059f, 0.208f, 0.376f, 1.0f }; // #0f3460
    constexpr Color ActiveBg      = { 0.102f, 0.290f, 0.227f, 1.0f }; // #1a4a3a
    constexpr Color RecordBg      = { 0.290f, 0.102f, 0.165f, 1.0f }; // #4a1a2a
}
