#include "OverlayApp.h"
#include <fstream>

static void DebugLog(const char* msg)
{
    char path[MAX_PATH];
    if (GetEnvironmentVariableA("LOCALAPPDATA", path, MAX_PATH) > 0)
    {
        std::string logPath = std::string(path) + "\\ReplayOverlay\\overlay_crash.log";
        std::ofstream f(logPath, std::ios::app);
        if (f.is_open())
            f << "  [IPC] " << msg << std::endl;
    }
}

bool OverlayApp::Init(const std::string& pipeName)
{
    m_pipeName = pipeName;

    // Init QPC timer
    LARGE_INTEGER freq, counter;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&counter);
    m_timerFrequency = static_cast<double>(freq.QuadPart);
    m_lastFrameTime = counter.QuadPart;

    // Create full-screen transparent overlay window
    if (!m_window.Init(0, 0, L"Replay Overlay"))
        return false;

    // Init DirectX + RmlUi
    if (!m_renderer.Init(m_window.GetHwnd(), m_window.GetWidth(), m_window.GetHeight()))
        return false;

    // Wire RmlUi context to WindowManager for input forwarding
    m_window.SetRmlContext(m_renderer.GetRmlContext());

    // Initialize data model (must be before loading document)
    if (!m_dataModel.Init(m_renderer.GetRmlContext(), &m_state, &m_pendingActions))
        return false;

    // Load the overlay document (uses data-model="overlay")
    m_renderer.LoadOverlayDocument();

    // Panel starts hidden until host sends show_overlay
    SetPanelHidden(true);

    // Connect to host via named pipe
    if (m_ipc.Connect(pipeName))
    {
        // Send ready signal
        m_ipc.SendMessage({"ready", {}});
    }

    return true;
}

void OverlayApp::Shutdown()
{
    m_renderer.ClearPreviewTexture();
    m_preview.Release();
    m_renderer.Shutdown();
    m_window.Shutdown();
    m_ipc.Disconnect();
}

double OverlayApp::GetElapsedTime() const
{
    LARGE_INTEGER counter;
    QueryPerformanceCounter(&counter);
    return static_cast<double>(counter.QuadPart) / m_timerFrequency;
}

bool OverlayApp::Tick()
{
    // Calculate delta time
    LARGE_INTEGER counter;
    QueryPerformanceCounter(&counter);
    m_deltaTime = static_cast<float>(
        static_cast<double>(counter.QuadPart - m_lastFrameTime) / m_timerFrequency);
    m_lastFrameTime = counter.QuadPart;

    double elapsed = GetElapsedTime();

    // Process Win32 messages
    if (!m_window.ProcessMessages())
        return false;

    if (m_shouldExit)
        return false;

    // Reconnect if disconnected
    if (!m_ipc.IsConnected())
    {
        m_reconnectTimer += m_deltaTime;
        if (m_reconnectTimer >= ReconnectIntervalS)
        {
            m_reconnectTimer = 0.0f;
            if (m_ipc.Connect(m_pipeName))
            {
                m_configReceived = false;
                m_renderer.ClearPreviewTexture();
                m_preview.Release();
                m_dataModel.SetHasPreview(false);
                m_ipc.SendMessage({"ready", {}});
            }
        }
    }

    // Process incoming IPC messages
    ProcessIpcMessages();

    // Sync data model from state (pushes changes to RmlUi bindings)
    m_dataModel.SetElapsedTime(elapsed);
    m_dataModel.SyncFromState();

    // Update notification/REC indicator animations
    m_dataModel.UpdateNotification(m_deltaTime);
    m_dataModel.UpdateRecIndicator(m_deltaTime);

    // Send any actions queued by the data model
    SendPendingActions();

    // Process data model changes (data-if element creation/destruction, layout)
    // Must happen BEFORE direct element manipulation so GetElementById returns
    // freshly (re)created elements from data-if tab switches.
    auto* ctx = m_renderer.GetRmlContext();
    if (ctx) ctx->Update();

    // Update element state directly (bypasses unreliable data-class-* bindings)
    if (ctx)
    {
        auto* body = ctx->GetRootElement();
        if (body)
        {
            // REC indicator: toggle hidden class + set position properties
            auto* recEl = body->GetElementById("rec-indicator");
            if (recEl)
            {
                bool active = m_dataModel.IsRecActive();
                std::string pos = m_dataModel.GetRecPosition();

                recEl->SetClass("hidden", !active);

                // Position via CSS classes -- removing a class cleanly
                // removes its properties from the cascade.
                recEl->SetClass("pos-tl", active && pos == "top-left");
                recEl->SetClass("pos-tc", active && pos == "top-center");
                recEl->SetClass("pos-tr", active && pos == "top-right");
                recEl->SetClass("pos-bl", active && pos == "bottom-left");
                recEl->SetClass("pos-bc", active && pos == "bottom-center");
                recEl->SetClass("pos-br", active && pos == "bottom-right");
            }

            // Preview: toggle image/placeholder visibility via SetProperty
            // Elements live inside data-if tab block; null when tab != 'main'
            auto* previewImg = body->GetElementById("preview-img");
            auto* previewPlaceholder = body->GetElementById("preview-placeholder");
            if (previewImg && previewPlaceholder)
            {
                if (m_dataModel.HasPreview())
                {
                    previewImg->SetProperty("display", "block");
                    previewPlaceholder->SetProperty("display", "none");

                    // Compute width from actual video aspect ratio to avoid
                    // RmlUi's cached 1x1 placeholder intrinsic dimensions.
                    int pw = m_preview.GetWidth();
                    int ph = m_preview.GetHeight();
                    if (pw > 0 && ph > 0)
                    {
                        // Fill container height (140dp), compute matching width
                        constexpr int containerH = 140;
                        int w = containerH * pw / ph;
                        previewImg->SetProperty("height", std::to_string(containerH) + "dp");
                        previewImg->SetProperty("width", std::to_string(w) + "dp");
                    }
                    else
                    {
                        previewImg->SetProperty("width", "100%");
                        previewImg->SetProperty("height", "100%");
                    }
                }
                else
                {
                    previewImg->SetProperty("display", "none");
                    previewPlaceholder->RemoveProperty("display");
                }
            }

            // Second Update() processes style/attribute changes made above
            // (display toggles, src changes) so layout is correct this frame.
            ctx->Update();

            // Update panel rect for click-through based on document element
            auto* panel = body->GetElementById("panel");
            if (!panel)
            {
                for (int i = 0; i < body->GetNumChildren(); i++)
                {
                    auto* child = body->GetChild(i);
                    if (child && child->GetClientWidth() > 100)
                    {
                        panel = child;
                        break;
                    }
                }
            }
            if (panel)
            {
                auto box = panel->GetAbsoluteOffset(Rml::BoxArea::Border);
                auto size = panel->GetBox().GetSize(Rml::BoxArea::Border);
                if (size.x > 0 && size.y > 0)
                {
                    m_window.SetPanelRect(
                        static_cast<int>(box.x), static_cast<int>(box.y),
                        static_cast<int>(size.x), static_cast<int>(size.y));
                }
            }
        }
    }

    // Render frame
    m_renderer.BeginFrame(0.0f, 0.0f, 0.0f, 0.0f);
    m_renderer.EndFrame();

    // Toggle WS_EX_TRANSPARENT based on mouse position vs panel rect
    m_window.UpdateClickThrough();

    return true;
}

void OverlayApp::ProcessIpcMessages()
{
    for (int i = 0; i < 100; i++)
    {
        auto msg = m_ipc.ReadMessage();
        if (!msg) break;

        const auto& type = msg->type;

        try
        {
            if (type == "state_update")
            {
                bool wasBuf = m_state.isBufferActive;
                m_state.UpdateFromStateJson(msg->payload);
                // Sync REC indicator when buffer status changes via state_update
                // (only after config_update so we have the correct position)
                if (m_configReceived && m_state.isBufferActive != wasBuf)
                {
                    m_dataModel.SetRecIndicator(
                        m_state.showRecIndicator && m_state.isBufferActive,
                        m_state.recIndicatorPosition);
                }
            }
            else if (type == "preview_frame")
            {
                if (msg->payload.contains("base64") && msg->payload["base64"].is_string())
                {
                    auto base64 = msg->payload["base64"].get<std::string>();
                    m_renderer.ClearPreviewTexture(); // detach before old SRV is freed
                    m_preview.UpdateFromBase64(m_renderer, base64);
                    if (m_preview.GetTexture())
                    {
                        m_renderer.SetPreviewTexture(
                            m_preview.GetTexture(), m_preview.GetWidth(), m_preview.GetHeight());
                        m_dataModel.SetHasPreview(true);
                    }
                    else
                    {
                        m_dataModel.SetHasPreview(false);
                    }
                }
            }
            else if (type == "config_update")
            {
                m_state.UpdateFromConfigJson(msg->payload);
                m_configReceived = true;
                // Update REC indicator from config (now has correct position)
                m_dataModel.SetRecIndicator(
                    m_state.showRecIndicator && m_state.isBufferActive,
                    m_state.recIndicatorPosition);
            }
            else if (type == "show_overlay")
            {
                m_state.overlayVisible = true;
                m_window.SetVisible(true);
                m_window.SetTopmost(true);
                SetPanelHidden(false);
            }
            else if (type == "hide_overlay")
            {
                m_state.overlayVisible = false;
                m_window.SetVisible(false);
                SetPanelHidden(true);
            }
            else if (type == "settings_opened")
            {
                m_state.overlayVisible = false;
                m_window.SetVisible(false);
                m_window.SetTopmost(false);
                SetPanelHidden(true);
            }
            else if (type == "settings_closed")
            {
                m_window.SetTopmost(true);
            }
            else if (type == "audio_advanced")
            {
                m_state.UpdateFromAudioAdvancedJson(msg->payload);
            }
            else if (type == "input_kinds")
            {
                m_state.UpdateFromInputKindsJson(msg->payload);
            }
            else if (type == "filters_response")
            {
                m_state.UpdateFromFiltersJson(msg->payload);
            }
            else if (type == "filter_kinds")
            {
                m_state.UpdateFromFilterKindsJson(msg->payload);
            }
            else if (type == "stats_response")
            {
                m_state.UpdateFromStatsJson(msg->payload);
            }
            else if (type == "hotkeys_response")
            {
                m_state.UpdateFromHotkeysJson(msg->payload);
            }
            else if (type == "show_notification")
            {
                if (m_state.showNotifications)
                {
                    std::string text = msg->payload.value("text", m_state.notificationMessage);
                    std::string color = msg->payload.value("color", "#4ecca3");
                    float dur = static_cast<float>(m_state.notificationDuration);
                    m_dataModel.ShowNotification(text, color, dur);
                }
            }
            else if (type == "rec_indicator")
            {
                if (!m_configReceived) continue; // Wait for config before using position
                bool active = msg->payload.value("active", false);
                std::string pos = msg->payload.value("position", m_state.recIndicatorPosition);
                if (m_state.showRecIndicator)
                    m_dataModel.SetRecIndicator(active, pos);
            }
            else if (type == "shutdown")
            {
                m_shouldExit = true;
            }
        }
        catch (const std::exception& ex)
        {
            DebugLog((std::string("Exception handling '") + type + "': " + ex.what()).c_str());
        }
        catch (...)
        {
            DebugLog((std::string("Unknown exception handling '") + type + "'").c_str());
        }
    }
}

void OverlayApp::SetPanelHidden(bool hidden)
{
    auto* ctx = m_renderer.GetRmlContext();
    if (!ctx) return;
    auto* body = ctx->GetRootElement();
    if (!body) return;
    auto* panel = body->GetElementById("panel");
    if (panel)
    {
        panel->SetClass("hidden", hidden);
        if (!hidden)
        {
            // Force margin recalculation after display:none -> display:flex
            // RmlUi doesn't reliably recompute margin:auto after visibility toggle
            panel->SetProperty("margin", "40dp auto 0 auto");
        }
    }
}

void OverlayApp::SendPendingActions()
{
    for (const auto& action : m_pendingActions)
        m_ipc.SendMessage(action);
    m_pendingActions.clear();
}
