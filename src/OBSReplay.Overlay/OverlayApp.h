#pragma once
#include <string>
#include "WindowManager.h"
#include "DxRenderer.h"
#include "IpcClient.h"
#include "OverlayState.h"
#include "PreviewRenderer.h"
#include "OverlayDataModel.h"

class OverlayApp
{
public:
    bool Init(const std::string& pipeName);
    void Shutdown();
    bool Tick(); // Returns false when app should exit

private:
    void ProcessIpcMessages();
    void SendPendingActions();
    double GetElapsedTime() const;

    WindowManager         m_window;
    DxRenderer            m_renderer;
    IpcClient             m_ipc;
    OverlayState          m_state;
    PreviewRenderer       m_preview;
    OverlayDataModel      m_dataModel;

    std::string           m_pipeName;
    std::vector<IpcMessage> m_pendingActions;
    bool                  m_shouldExit = false;
    float                 m_reconnectTimer = 0.0f;
    static constexpr float ReconnectIntervalS = 2.0f;

    // QPC timer for delta time
    double m_timerFrequency = 0.0;
    long long m_lastFrameTime = 0;
    float m_deltaTime = 0.0f;
};
