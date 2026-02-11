#include "OverlayApp.h"
#include <string>
#include <fstream>
#include <ctime>

static void CrashLog(const char* msg)
{
    char path[MAX_PATH];
    if (GetEnvironmentVariableA("LOCALAPPDATA", path, MAX_PATH) > 0)
    {
        std::string dirPath = std::string(path) + "\\ReplayOverlay";
        CreateDirectoryA(dirPath.c_str(), nullptr); // Ensure directory exists (no-op if already present)
        std::string logPath = dirPath + "\\overlay_crash.log";
        std::ofstream f(logPath, std::ios::app);
        if (f.is_open())
        {
            time_t t = time(nullptr);
            char timeBuf[64];
            ctime_s(timeBuf, sizeof(timeBuf), &t);
            timeBuf[strlen(timeBuf) - 1] = '\0'; // remove newline
            f << "[" << timeBuf << "] " << msg << std::endl;
        }
    }
}

static LONG WINAPI CrashHandler(EXCEPTION_POINTERS* ep)
{
    char buf[256];
    snprintf(buf, sizeof(buf), "CRASH: code=0x%08X addr=%p",
        ep->ExceptionRecord->ExceptionCode,
        ep->ExceptionRecord->ExceptionAddress);
    CrashLog(buf);
    return EXCEPTION_EXECUTE_HANDLER;
}

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR lpCmdLine, int)
{
    SetUnhandledExceptionFilter(CrashHandler);
    CrashLog("Overlay starting");

    // Parse command line for --pipe <name>
    std::string pipeName = "ReplayOverlayPipe"; // default

    std::string cmdLine(lpCmdLine);
    auto pipePos = cmdLine.find("--pipe");
    if (pipePos != std::string::npos)
    {
        auto nameStart = cmdLine.find_first_not_of(' ', pipePos + 6);
        if (nameStart != std::string::npos)
        {
            auto nameEnd = cmdLine.find(' ', nameStart);
            pipeName = cmdLine.substr(nameStart,
                nameEnd == std::string::npos ? std::string::npos : nameEnd - nameStart);
        }
    }

    CrashLog("Init starting");
    OverlayApp app;
    if (!app.Init(pipeName))
    {
        CrashLog("Init failed");
        return 1;
    }
    CrashLog("Init complete, entering main loop");

    // Main loop - renders at VSync rate (~60fps)
    int frameCount = 0;
    while (app.Tick())
    {
        frameCount++;
        if (frameCount == 1 || frameCount == 10 || frameCount == 60)
        {
            char buf[64];
            snprintf(buf, sizeof(buf), "Frame %d OK", frameCount);
            CrashLog(buf);
        }
    }

    CrashLog("Main loop ended, shutting down");
    app.Shutdown();
    CrashLog("Shutdown complete");
    return 0;
}
