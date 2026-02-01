#pragma once
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <RmlUi/Core/Input.h>
#include <RmlUi/Core/Types.h>

namespace Rml { class Context; }

struct OverlayRect { int x, y, w, h; };

class WindowManager
{
public:
    bool Init(int width, int height, const wchar_t* title);
    void Shutdown();

    HWND GetHwnd() const { return m_hwnd; }
    int  GetWidth() const { return m_width; }
    int  GetHeight() const { return m_height; }

    void SetVisible(bool visible);
    void SetPosition(int x, int y);
    bool ProcessMessages(); // Returns false if WM_QUIT received

    // Set the region where the overlay panel is drawn (for hit testing)
    void SetPanelRect(int x, int y, int w, int h);

    // Call each frame: toggles WS_EX_TRANSPARENT based on mouse position vs panel rect
    void UpdateClickThrough();

    // Temporarily drop topmost so other app windows (settings) can be used
    void SetTopmost(bool topmost);

    // Set RmlUi context for input forwarding
    void SetRmlContext(Rml::Context* ctx) { m_rmlContext = ctx; }

    // Win32 VK to RmlUi key identifier conversion
    static Rml::Input::KeyIdentifier ConvertKey(int win32Key);
    static int GetKeyModifierState();

private:
    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

    HWND m_hwnd = nullptr;
    int  m_width = 340;
    int  m_height = 500;
    bool m_visible = false;

    // Panel rect for click-through hit testing
    OverlayRect m_panelRect = {};
    bool m_panelVisible = false;
    bool m_isClickThrough = true; // Current WS_EX_TRANSPARENT state

    // RmlUi context for input and hover detection
    Rml::Context* m_rmlContext = nullptr;
};
