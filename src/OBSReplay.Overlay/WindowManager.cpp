#include "WindowManager.h"
#include <RmlUi/Core/Context.h>
#include <RmlUi/Core/Element.h>
#include <dwmapi.h>

#pragma comment(lib, "dwmapi.lib")

// Store instance pointer for WndProc static callback
static WindowManager* s_instance = nullptr;

bool WindowManager::Init(int width, int height, const wchar_t* title)
{
    s_instance = this;

    int screenW = GetSystemMetrics(SM_CXSCREEN);
    int screenH = GetSystemMetrics(SM_CYSCREEN);
    m_width = screenW;
    m_height = screenH;

    WNDCLASSEXW wc = {};
    wc.cbSize        = sizeof(wc);
    wc.style         = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc   = WndProc;
    wc.hInstance      = GetModuleHandle(nullptr);
    wc.lpszClassName  = L"OBSReplayOverlayClass";
    wc.hCursor        = LoadCursor(nullptr, IDC_ARROW);

    if (!RegisterClassExW(&wc))
        return false;

    DWORD exStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST |
                    WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

    m_hwnd = CreateWindowExW(
        exStyle, wc.lpszClassName, title, WS_POPUP,
        0, 0, m_width, m_height,
        nullptr, nullptr, wc.hInstance, nullptr);

    if (!m_hwnd)
        return false;

    m_isClickThrough = true;

    SetLayeredWindowAttributes(m_hwnd, 0, 255, LWA_ALPHA);

    MARGINS margins = { -1, -1, -1, -1 };
    DwmExtendFrameIntoClientArea(m_hwnd, &margins);

    ShowWindow(m_hwnd, SW_SHOWNA);
    return true;
}

void WindowManager::Shutdown()
{
    if (m_hwnd)
    {
        DestroyWindow(m_hwnd);
        m_hwnd = nullptr;
    }
    UnregisterClassW(L"OBSReplayOverlayClass", GetModuleHandle(nullptr));
    s_instance = nullptr;
}

void WindowManager::SetVisible(bool visible)
{
    m_panelVisible = visible;
    m_visible = visible;
    if (!visible && m_hwnd && !m_isClickThrough)
    {
        LONG_PTR ex = GetWindowLongPtrW(m_hwnd, GWL_EXSTYLE);
        SetWindowLongPtrW(m_hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT);
        m_isClickThrough = true;
    }
}

void WindowManager::SetPosition(int x, int y)
{
    if (m_hwnd)
        SetWindowPos(m_hwnd, nullptr, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
}

void WindowManager::SetPanelRect(int x, int y, int w, int h)
{
    m_panelRect = { x, y, w, h };
}

void WindowManager::SetTopmost(bool topmost)
{
    if (!m_hwnd) return;
    SetWindowPos(m_hwnd,
        topmost ? HWND_TOPMOST : HWND_NOTOPMOST,
        0, 0, 0, 0,
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
}

void WindowManager::UpdateClickThrough()
{
    if (!m_hwnd) return;

    bool wantInput = false;

    if (m_panelVisible && m_panelRect.w > 0 && m_panelRect.h > 0)
    {
        POINT cursor;
        if (GetCursorPos(&cursor))
        {
            const int pad = 20;
            wantInput = (cursor.x >= m_panelRect.x - pad &&
                         cursor.x <  m_panelRect.x + m_panelRect.w + pad &&
                         cursor.y >= m_panelRect.y - pad &&
                         cursor.y <  m_panelRect.y + m_panelRect.h + pad);

            // Also check if RmlUi has an element under the cursor
            // (handles popups/dropdowns extending outside panel rect)
            if (!wantInput && m_rmlContext)
            {
                auto* hover = m_rmlContext->GetHoverElement();
                if (hover && hover != m_rmlContext->GetRootElement())
                    wantInput = true;
            }
        }
    }

    if (wantInput && m_isClickThrough)
    {
        LONG_PTR ex = GetWindowLongPtrW(m_hwnd, GWL_EXSTYLE);
        SetWindowLongPtrW(m_hwnd, GWL_EXSTYLE, ex & ~WS_EX_TRANSPARENT);
        m_isClickThrough = false;
    }
    else if (!wantInput && !m_isClickThrough)
    {
        LONG_PTR ex = GetWindowLongPtrW(m_hwnd, GWL_EXSTYLE);
        SetWindowLongPtrW(m_hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT);
        m_isClickThrough = true;
    }
}

bool WindowManager::ProcessMessages()
{
    MSG msg;
    while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
    {
        if (msg.message == WM_QUIT)
            return false;
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    return true;
}

LRESULT CALLBACK WindowManager::WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    Rml::Context* ctx = s_instance ? s_instance->m_rmlContext : nullptr;

    if (ctx)
    {
        switch (msg)
        {
        case WM_LBUTTONDOWN:
            ctx->ProcessMouseButtonDown(0, GetKeyModifierState());
            SetCapture(hwnd);
            return 0;
        case WM_LBUTTONUP:
            ReleaseCapture();
            ctx->ProcessMouseButtonUp(0, GetKeyModifierState());
            return 0;
        case WM_RBUTTONDOWN:
            ctx->ProcessMouseButtonDown(1, GetKeyModifierState());
            return 0;
        case WM_RBUTTONUP:
            ctx->ProcessMouseButtonUp(1, GetKeyModifierState());
            return 0;
        case WM_MBUTTONDOWN:
            ctx->ProcessMouseButtonDown(2, GetKeyModifierState());
            return 0;
        case WM_MBUTTONUP:
            ctx->ProcessMouseButtonUp(2, GetKeyModifierState());
            return 0;
        case WM_MOUSEMOVE:
            ctx->ProcessMouseMove(
                static_cast<int>((short)LOWORD(lParam)),
                static_cast<int>((short)HIWORD(lParam)),
                GetKeyModifierState());
            return 0;
        case WM_MOUSEWHEEL:
            ctx->ProcessMouseWheel(
                static_cast<float>((short)HIWORD(wParam)) / static_cast<float>(-WHEEL_DELTA),
                GetKeyModifierState());
            return 0;
        case WM_MOUSELEAVE:
            ctx->ProcessMouseLeave();
            return 0;
        case WM_KEYDOWN:
            ctx->ProcessKeyDown(ConvertKey(static_cast<int>(wParam)), GetKeyModifierState());
            return 0;
        case WM_KEYUP:
            ctx->ProcessKeyUp(ConvertKey(static_cast<int>(wParam)), GetKeyModifierState());
            return 0;
        case WM_CHAR:
        {
            const wchar_t c = static_cast<wchar_t>(wParam);
            // Only printable characters and newline
            if ((c >= 32 || c == '\n') && c != 127)
            {
                // Convert \r to \n
                Rml::Character character = (c == '\r')
                    ? static_cast<Rml::Character>('\n')
                    : static_cast<Rml::Character>(c);
                ctx->ProcessTextInput(character);
            }
            return 0;
        }
        }
    }

    switch (msg)
    {
    case WM_SIZE:
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    case WM_CLOSE:
        ShowWindow(hwnd, SW_HIDE);
        return 0;
    }

    return DefWindowProc(hwnd, msg, wParam, lParam);
}

int WindowManager::GetKeyModifierState()
{
    int state = 0;
    if (GetKeyState(VK_CAPITAL) & 1)
        state |= Rml::Input::KM_CAPSLOCK;
    if (GetKeyState(VK_NUMLOCK) & 1)
        state |= Rml::Input::KM_NUMLOCK;
    if (HIWORD(GetKeyState(VK_SHIFT)) & 1)
        state |= Rml::Input::KM_SHIFT;
    if (HIWORD(GetKeyState(VK_CONTROL)) & 1)
        state |= Rml::Input::KM_CTRL;
    if (HIWORD(GetKeyState(VK_MENU)) & 1)
        state |= Rml::Input::KM_ALT;
    return state;
}

Rml::Input::KeyIdentifier WindowManager::ConvertKey(int win32Key)
{
    // clang-format off
    switch (win32Key)
    {
    case 'A': return Rml::Input::KI_A;
    case 'B': return Rml::Input::KI_B;
    case 'C': return Rml::Input::KI_C;
    case 'D': return Rml::Input::KI_D;
    case 'E': return Rml::Input::KI_E;
    case 'F': return Rml::Input::KI_F;
    case 'G': return Rml::Input::KI_G;
    case 'H': return Rml::Input::KI_H;
    case 'I': return Rml::Input::KI_I;
    case 'J': return Rml::Input::KI_J;
    case 'K': return Rml::Input::KI_K;
    case 'L': return Rml::Input::KI_L;
    case 'M': return Rml::Input::KI_M;
    case 'N': return Rml::Input::KI_N;
    case 'O': return Rml::Input::KI_O;
    case 'P': return Rml::Input::KI_P;
    case 'Q': return Rml::Input::KI_Q;
    case 'R': return Rml::Input::KI_R;
    case 'S': return Rml::Input::KI_S;
    case 'T': return Rml::Input::KI_T;
    case 'U': return Rml::Input::KI_U;
    case 'V': return Rml::Input::KI_V;
    case 'W': return Rml::Input::KI_W;
    case 'X': return Rml::Input::KI_X;
    case 'Y': return Rml::Input::KI_Y;
    case 'Z': return Rml::Input::KI_Z;
    case '0': return Rml::Input::KI_0;
    case '1': return Rml::Input::KI_1;
    case '2': return Rml::Input::KI_2;
    case '3': return Rml::Input::KI_3;
    case '4': return Rml::Input::KI_4;
    case '5': return Rml::Input::KI_5;
    case '6': return Rml::Input::KI_6;
    case '7': return Rml::Input::KI_7;
    case '8': return Rml::Input::KI_8;
    case '9': return Rml::Input::KI_9;
    case VK_BACK:    return Rml::Input::KI_BACK;
    case VK_TAB:     return Rml::Input::KI_TAB;
    case VK_CLEAR:   return Rml::Input::KI_CLEAR;
    case VK_RETURN:  return Rml::Input::KI_RETURN;
    case VK_PAUSE:   return Rml::Input::KI_PAUSE;
    case VK_CAPITAL: return Rml::Input::KI_CAPITAL;
    case VK_ESCAPE:  return Rml::Input::KI_ESCAPE;
    case VK_SPACE:   return Rml::Input::KI_SPACE;
    case VK_PRIOR:   return Rml::Input::KI_PRIOR;
    case VK_NEXT:    return Rml::Input::KI_NEXT;
    case VK_END:     return Rml::Input::KI_END;
    case VK_HOME:    return Rml::Input::KI_HOME;
    case VK_LEFT:    return Rml::Input::KI_LEFT;
    case VK_UP:      return Rml::Input::KI_UP;
    case VK_RIGHT:   return Rml::Input::KI_RIGHT;
    case VK_DOWN:    return Rml::Input::KI_DOWN;
    case VK_INSERT:  return Rml::Input::KI_INSERT;
    case VK_DELETE:  return Rml::Input::KI_DELETE;
    case VK_LWIN:    return Rml::Input::KI_LWIN;
    case VK_RWIN:    return Rml::Input::KI_RWIN;
    case VK_NUMPAD0: return Rml::Input::KI_NUMPAD0;
    case VK_NUMPAD1: return Rml::Input::KI_NUMPAD1;
    case VK_NUMPAD2: return Rml::Input::KI_NUMPAD2;
    case VK_NUMPAD3: return Rml::Input::KI_NUMPAD3;
    case VK_NUMPAD4: return Rml::Input::KI_NUMPAD4;
    case VK_NUMPAD5: return Rml::Input::KI_NUMPAD5;
    case VK_NUMPAD6: return Rml::Input::KI_NUMPAD6;
    case VK_NUMPAD7: return Rml::Input::KI_NUMPAD7;
    case VK_NUMPAD8: return Rml::Input::KI_NUMPAD8;
    case VK_NUMPAD9: return Rml::Input::KI_NUMPAD9;
    case VK_MULTIPLY:  return Rml::Input::KI_MULTIPLY;
    case VK_ADD:       return Rml::Input::KI_ADD;
    case VK_SEPARATOR: return Rml::Input::KI_SEPARATOR;
    case VK_SUBTRACT:  return Rml::Input::KI_SUBTRACT;
    case VK_DECIMAL:   return Rml::Input::KI_DECIMAL;
    case VK_DIVIDE:    return Rml::Input::KI_DIVIDE;
    case VK_F1:  return Rml::Input::KI_F1;
    case VK_F2:  return Rml::Input::KI_F2;
    case VK_F3:  return Rml::Input::KI_F3;
    case VK_F4:  return Rml::Input::KI_F4;
    case VK_F5:  return Rml::Input::KI_F5;
    case VK_F6:  return Rml::Input::KI_F6;
    case VK_F7:  return Rml::Input::KI_F7;
    case VK_F8:  return Rml::Input::KI_F8;
    case VK_F9:  return Rml::Input::KI_F9;
    case VK_F10: return Rml::Input::KI_F10;
    case VK_F11: return Rml::Input::KI_F11;
    case VK_F12: return Rml::Input::KI_F12;
    case VK_NUMLOCK: return Rml::Input::KI_NUMLOCK;
    case VK_SCROLL:  return Rml::Input::KI_SCROLL;
    case VK_SHIFT:   return Rml::Input::KI_LSHIFT;
    case VK_CONTROL: return Rml::Input::KI_LCONTROL;
    case VK_MENU:    return Rml::Input::KI_LMENU;
    case VK_OEM_1:      return Rml::Input::KI_OEM_1;
    case VK_OEM_PLUS:   return Rml::Input::KI_OEM_PLUS;
    case VK_OEM_COMMA:  return Rml::Input::KI_OEM_COMMA;
    case VK_OEM_MINUS:  return Rml::Input::KI_OEM_MINUS;
    case VK_OEM_PERIOD: return Rml::Input::KI_OEM_PERIOD;
    case VK_OEM_2:      return Rml::Input::KI_OEM_2;
    case VK_OEM_3:      return Rml::Input::KI_OEM_3;
    case VK_OEM_4:      return Rml::Input::KI_OEM_4;
    case VK_OEM_5:      return Rml::Input::KI_OEM_5;
    case VK_OEM_6:      return Rml::Input::KI_OEM_6;
    case VK_OEM_7:      return Rml::Input::KI_OEM_7;
    }
    // clang-format on
    return Rml::Input::KI_UNKNOWN;
}
