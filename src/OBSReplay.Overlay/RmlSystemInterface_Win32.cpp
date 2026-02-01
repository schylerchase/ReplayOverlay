#include "RmlSystemInterface_Win32.h"
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <fstream>
#include <string>

double RmlSystemInterface_Win32::GetElapsedTime()
{
    if (!m_timerInitialized)
    {
        LARGE_INTEGER freq, counter;
        QueryPerformanceFrequency(&freq);
        QueryPerformanceCounter(&counter);
        m_frequency = static_cast<double>(freq.QuadPart);
        m_startTime = counter.QuadPart;
        m_timerInitialized = true;
    }

    LARGE_INTEGER counter;
    QueryPerformanceCounter(&counter);
    return static_cast<double>(counter.QuadPart - m_startTime) / m_frequency;
}

bool RmlSystemInterface_Win32::LogMessage(Rml::Log::Type type, const Rml::String& message)
{
    const char* typeStr = "";
    switch (type)
    {
    case Rml::Log::LT_ERROR:   typeStr = "ERROR"; break;
    case Rml::Log::LT_WARNING: typeStr = "WARN";  break;
    case Rml::Log::LT_INFO:    typeStr = "INFO";  break;
    case Rml::Log::LT_DEBUG:   typeStr = "DEBUG"; break;
    default: typeStr = "LOG"; break;
    }

    // Write to debug log file
    char path[MAX_PATH];
    if (GetEnvironmentVariableA("LOCALAPPDATA", path, MAX_PATH) > 0)
    {
        std::string logPath = std::string(path) + "\\ReplayOverlay\\rmlui.log";
        std::ofstream f(logPath, std::ios::app);
        if (f.is_open())
            f << "[" << typeStr << "] " << message << std::endl;
    }

    OutputDebugStringA("[RmlUi ");
    OutputDebugStringA(typeStr);
    OutputDebugStringA("] ");
    OutputDebugStringA(message.c_str());
    OutputDebugStringA("\n");

    return true; // true = message handled
}

void RmlSystemInterface_Win32::SetClipboardText(const Rml::String& text)
{
    if (!OpenClipboard(nullptr))
        return;

    EmptyClipboard();
    HGLOBAL hg = GlobalAlloc(GMEM_MOVEABLE, text.size() + 1);
    if (hg)
    {
        memcpy(GlobalLock(hg), text.c_str(), text.size() + 1);
        GlobalUnlock(hg);
        SetClipboardData(CF_TEXT, hg);
    }
    CloseClipboard();
}

void RmlSystemInterface_Win32::GetClipboardText(Rml::String& text)
{
    if (!OpenClipboard(nullptr))
        return;

    HANDLE hData = GetClipboardData(CF_TEXT);
    if (hData)
    {
        const char* data = static_cast<const char*>(GlobalLock(hData));
        if (data)
            text = data;
        GlobalUnlock(hData);
    }
    CloseClipboard();
}
