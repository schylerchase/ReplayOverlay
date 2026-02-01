#include "OverlayDataModel.h"
#include <cmath>
#include <algorithm>
#include <cctype>

// Fader math (matches C# AudioMathService)
static int MulToFader(double mul)
{
    if (mul <= 0.0) return 0;
    double db = 20.0 * log10(mul);
    if (db < -96.0) return 0;
    if (db > 6.0)   return 100;
    double normalized = (db - (-96.0)) / 102.0;
    double fader = pow(normalized, 1.0 / 3.0);
    return static_cast<int>(round(fader * 100.0));
}

static double FaderToMul(int pct)
{
    if (pct <= 0) return 0.0;
    if (pct >= 100) return pow(10.0, 6.0 / 20.0);
    double f = pct / 100.0;
    double normalized = f * f * f;
    double db = normalized * 102.0 + (-96.0);
    return pow(10.0, db / 20.0);
}

static std::string HumanizeHotkeyName(const std::string& raw)
{
    std::string name = raw;
    auto dotPos = name.rfind('.');
    if (dotPos != std::string::npos)
        name = name.substr(dotPos + 1);

    std::string result;
    for (size_t i = 0; i < name.size(); i++)
    {
        if (i > 0 && isupper(name[i]) && !isupper(name[i - 1]))
            result += ' ';
        result += name[i];
    }
    return result;
}

static Rml::String FormatFloat(double val, int decimals)
{
    char buf[64];
    if (decimals == 0)
        snprintf(buf, sizeof(buf), "%.0f", val);
    else if (decimals == 1)
        snprintf(buf, sizeof(buf), "%.1f", val);
    else
        snprintf(buf, sizeof(buf), "%.2f", val);
    return buf;
}

bool OverlayDataModel::Init(Rml::Context* ctx, OverlayState* state,
                             std::vector<IpcMessage>* outActions)
{
    m_state = state;
    m_actions = outActions;

    auto constructor = ctx->CreateDataModel("overlay");
    if (!constructor)
        return false;

    // Register struct types for arrays
    if (auto h = constructor.RegisterStruct<SceneItem>())
        h.RegisterMember("name", &SceneItem::name);

    if (auto h = constructor.RegisterStruct<SourceItem>())
    {
        h.RegisterMember("id", &SourceItem::id);
        h.RegisterMember("name", &SourceItem::name);
        h.RegisterMember("visible", &SourceItem::visible);
        h.RegisterMember("locked", &SourceItem::locked);
        h.RegisterMember("kind", &SourceItem::kind);
    }

    if (auto h = constructor.RegisterStruct<AudioItem>())
    {
        h.RegisterMember("name", &AudioItem::name);
        h.RegisterMember("volumeMul", &AudioItem::volumeMul);
        h.RegisterMember("muted", &AudioItem::muted);
        h.RegisterMember("faderVal", &AudioItem::faderVal);
    }

    if (auto h = constructor.RegisterStruct<FilterItem>())
    {
        h.RegisterMember("name", &FilterItem::name);
        h.RegisterMember("kind", &FilterItem::kind);
        h.RegisterMember("enabled", &FilterItem::enabled);
    }

    if (auto h = constructor.RegisterStruct<HotkeyItem>())
    {
        h.RegisterMember("rawName", &HotkeyItem::rawName);
        h.RegisterMember("displayName", &HotkeyItem::displayName);
    }

    // Register array types
    constructor.RegisterArray<Rml::Vector<SceneItem>>();
    constructor.RegisterArray<Rml::Vector<SourceItem>>();
    constructor.RegisterArray<Rml::Vector<AudioItem>>();
    constructor.RegisterArray<Rml::Vector<FilterItem>>();
    constructor.RegisterArray<Rml::Vector<HotkeyItem>>();
    constructor.RegisterArray<Rml::Vector<Rml::String>>();

    // Bind scalars
    constructor.Bind("active_tab", &m_activeTab);
    constructor.Bind("connected", &m_connected);
    constructor.Bind("current_scene", &m_currentScene);
    constructor.Bind("is_streaming", &m_isStreaming);
    constructor.Bind("is_recording", &m_isRecording);
    constructor.Bind("is_recording_paused", &m_isRecordingPaused);
    constructor.Bind("is_buffer_active", &m_isBufferActive);
    constructor.Bind("has_active_capture", &m_hasActiveCapture);
    constructor.Bind("current_profile", &m_currentProfile);
    constructor.Bind("current_collection", &m_currentCollection);
    constructor.Bind("current_transition", &m_currentTransition);
    constructor.Bind("transition_dur_ms", &m_transitionDurMs);
    constructor.Bind("studio_mode", &m_studioModeEnabled);
    constructor.Bind("preview_scene", &m_previewScene);
    constructor.Bind("toggle_hotkey", &m_toggleHotkey);
    constructor.Bind("save_hotkey", &m_saveHotkey);

    // Bind arrays
    constructor.Bind("scenes", &m_scenes);
    constructor.Bind("sources", &m_sources);
    constructor.Bind("audio_items", &m_audioItems);
    constructor.Bind("profiles", &m_profiles);
    constructor.Bind("collections", &m_collections);
    constructor.Bind("transitions_list", &m_transitions);
    constructor.Bind("filters", &m_filters);
    constructor.Bind("filter_sources", &m_filterSources);
    constructor.Bind("input_kinds", &m_inputKinds);
    constructor.Bind("filter_kinds", &m_filterKinds);
    constructor.Bind("hotkeys", &m_hotkeys);

    // UI-only state
    constructor.Bind("selected_source_id", &m_selectedSourceId);
    constructor.Bind("selected_source_name", &m_selectedSourceName);
    constructor.Bind("expanded_audio", &m_expandedAudioSource);
    constructor.Bind("filter_selected_source", &m_filterSelectedSource);
    constructor.Bind("filter_selected_idx", &m_filterSelectedIdx);
    constructor.Bind("hotkey_filter", &m_hotkeyFilter);

    // Stats
    constructor.Bind("stat_fps", &m_statFps);
    constructor.Bind("stat_cpu", &m_statCpu);
    constructor.Bind("stat_memory", &m_statMemory);
    constructor.Bind("stat_frame_time", &m_statFrameTime);
    constructor.Bind("stat_disk", &m_statDisk);
    constructor.Bind("stat_render_skip", &m_statRenderSkip);
    constructor.Bind("stat_output_skip", &m_statOutputSkip);
    constructor.Bind("fps_color", &m_fpsColor);
    constructor.Bind("cpu_color", &m_cpuColor);
    constructor.Bind("disk_color", &m_diskColor);
    constructor.Bind("render_skip_color", &m_renderSkipColor);
    constructor.Bind("output_skip_color", &m_outputSkipColor);

    // Audio advanced
    constructor.Bind("has_advanced", &m_hasAdvanced);
    constructor.Bind("adv_sync_ms", &m_advSyncMs);
    constructor.Bind("adv_balance", &m_advBalance);
    constructor.Bind("adv_monitor_type", &m_advMonitorType);

    // Notification
    constructor.Bind("notif_active", &m_notifActive);
    constructor.Bind("notif_text", &m_notifText);
    constructor.Bind("notif_color", &m_notifColor);
    constructor.Bind("notif_alpha", &m_notifAlpha);

    // REC indicator
    constructor.Bind("rec_active", &m_recActive);
    constructor.Bind("rec_dot_visible", &m_recDotVisible);
    constructor.Bind("rec_position", &m_recPosition);

    // Settings
    constructor.Bind("settings_show_notif", &m_settingsShowNotif);
    constructor.Bind("settings_notif_msg", &m_settingsNotifMsg);
    constructor.Bind("settings_notif_dur", &m_settingsNotifDur);
    constructor.Bind("settings_show_rec", &m_settingsShowRec);
    constructor.Bind("settings_rec_pos_idx", &m_settingsRecPosIdx);

    // Bind event callbacks
    constructor.BindEventCallback("switch_tab", &OverlayDataModel::OnSwitchTab, this);
    constructor.BindEventCallback("toggle_stream", &OverlayDataModel::OnToggleStream, this);
    constructor.BindEventCallback("toggle_record", &OverlayDataModel::OnToggleRecord, this);
    constructor.BindEventCallback("toggle_buffer", &OverlayDataModel::OnToggleBuffer, this);
    constructor.BindEventCallback("save_replay", &OverlayDataModel::OnSaveReplay, this);
    constructor.BindEventCallback("toggle_pause", &OverlayDataModel::OnTogglePause, this);
    constructor.BindEventCallback("switch_scene", &OverlayDataModel::OnSwitchScene, this);
    constructor.BindEventCallback("toggle_source", &OverlayDataModel::OnToggleSource, this);
    constructor.BindEventCallback("close_overlay", &OverlayDataModel::OnCloseOverlay, this);
    constructor.BindEventCallback("set_profile", &OverlayDataModel::OnSetProfile, this);
    constructor.BindEventCallback("set_collection", &OverlayDataModel::OnSetCollection, this);

    // Audio
    constructor.BindEventCallback("toggle_mute", &OverlayDataModel::OnToggleMute, this);
    constructor.BindEventCallback("set_volume", &OverlayDataModel::OnSetVolume, this);
    constructor.BindEventCallback("expand_audio", &OverlayDataModel::OnExpandAudio, this);
    constructor.BindEventCallback("set_sync_offset", &OverlayDataModel::OnSetSyncOffset, this);
    constructor.BindEventCallback("set_balance", &OverlayDataModel::OnSetBalance, this);
    constructor.BindEventCallback("set_monitor_type", &OverlayDataModel::OnSetMonitorType, this);
    constructor.BindEventCallback("set_tracks", &OverlayDataModel::OnSetTracks, this);

    // Sources
    constructor.BindEventCallback("select_source", &OverlayDataModel::OnSelectSource, this);
    constructor.BindEventCallback("source_up", &OverlayDataModel::OnSourceUp, this);
    constructor.BindEventCallback("source_down", &OverlayDataModel::OnSourceDown, this);
    constructor.BindEventCallback("source_dup", &OverlayDataModel::OnSourceDup, this);
    constructor.BindEventCallback("source_rename", &OverlayDataModel::OnSourceRename, this);
    constructor.BindEventCallback("source_delete", &OverlayDataModel::OnSourceDelete, this);
    constructor.BindEventCallback("source_create", &OverlayDataModel::OnSourceCreate, this);
    constructor.BindEventCallback("toggle_lock", &OverlayDataModel::OnToggleLock, this);

    // Filters
    constructor.BindEventCallback("select_filter_source", &OverlayDataModel::OnSelectFilterSource, this);
    constructor.BindEventCallback("select_filter", &OverlayDataModel::OnSelectFilter, this);
    constructor.BindEventCallback("toggle_filter", &OverlayDataModel::OnToggleFilter, this);
    constructor.BindEventCallback("filter_up", &OverlayDataModel::OnFilterUp, this);
    constructor.BindEventCallback("filter_down", &OverlayDataModel::OnFilterDown, this);
    constructor.BindEventCallback("filter_delete", &OverlayDataModel::OnFilterDelete, this);
    constructor.BindEventCallback("filter_create", &OverlayDataModel::OnFilterCreate, this);
    constructor.BindEventCallback("refresh_filters", &OverlayDataModel::OnRefreshFilters, this);

    // Transitions
    constructor.BindEventCallback("set_transition", &OverlayDataModel::OnSetTransition, this);
    constructor.BindEventCallback("set_transition_dur", &OverlayDataModel::OnSetTransitionDuration, this);
    constructor.BindEventCallback("toggle_studio_mode", &OverlayDataModel::OnToggleStudioMode, this);
    constructor.BindEventCallback("set_preview_scene", &OverlayDataModel::OnSetPreviewScene, this);
    constructor.BindEventCallback("trigger_transition", &OverlayDataModel::OnTriggerTransition, this);

    // Stats
    constructor.BindEventCallback("trigger_hotkey", &OverlayDataModel::OnTriggerHotkey, this);

    // Settings
    constructor.BindEventCallback("apply_settings", &OverlayDataModel::OnApplySettings, this);
    constructor.BindEventCallback("open_settings", &OverlayDataModel::OnOpenSettings, this);

    // Scene CRUD
    constructor.BindEventCallback("create_scene", &OverlayDataModel::OnCreateScene, this);
    constructor.BindEventCallback("rename_scene", &OverlayDataModel::OnRenameScene, this);
    constructor.BindEventCallback("delete_scene", &OverlayDataModel::OnDeleteScene, this);

    m_handle = constructor.GetModelHandle();
    return true;
}

bool OverlayDataModel::Debounce(const std::string& key, double interval)
{
    auto it = m_debounceTimers.find(key);
    if (it != m_debounceTimers.end() && (m_now - it->second) < interval)
        return false;
    m_debounceTimers[key] = m_now;
    return true;
}

void OverlayDataModel::SyncFromState()
{
    if (!m_state || !m_handle) return;

    bool dirty = false;

    // Connection status
    Rml::String newConn = m_state->connected ? "Connected" : "Disconnected";
    if (m_connected != newConn) { m_connected = newConn; m_handle.DirtyVariable("connected"); }

    // Simple scalars
    auto syncStr = [&](Rml::String& local, const std::string& src, const char* var) {
        if (local != Rml::String(src.c_str())) { local = src.c_str(); m_handle.DirtyVariable(var); }
    };
    auto syncBool = [&](bool& local, bool src, const char* var) {
        if (local != src) { local = src; m_handle.DirtyVariable(var); }
    };

    syncStr(m_currentScene, m_state->currentScene, "current_scene");
    syncBool(m_isStreaming, m_state->isStreaming, "is_streaming");
    syncBool(m_isRecording, m_state->isRecording, "is_recording");
    syncBool(m_isRecordingPaused, m_state->isRecordingPaused, "is_recording_paused");
    syncBool(m_isBufferActive, m_state->isBufferActive, "is_buffer_active");
    syncBool(m_hasActiveCapture, m_state->hasActiveCapture.value_or(false), "has_active_capture");
    syncStr(m_currentProfile, m_state->currentProfile, "current_profile");
    syncStr(m_currentCollection, m_state->currentSceneCollection, "current_collection");
    syncStr(m_currentTransition, m_state->currentTransition, "current_transition");
    syncBool(m_studioModeEnabled, m_state->studioModeEnabled, "studio_mode");
    syncStr(m_previewScene, m_state->previewScene, "preview_scene");
    syncStr(m_toggleHotkey, m_state->toggleHotkey, "toggle_hotkey");
    syncStr(m_saveHotkey, m_state->saveHotkey, "save_hotkey");

    // Transition duration (with debounce)
    if ((m_now - m_lastDurChange) >= SliderDebounceS)
    {
        if (m_transitionDurMs != m_state->transitionDurationMs)
        {
            m_transitionDurMs = m_state->transitionDurationMs;
            m_handle.DirtyVariable("transition_dur_ms");
        }
    }

    // Scenes
    {
        bool changed = m_scenes.size() != m_state->scenes.size();
        if (!changed)
        {
            for (size_t i = 0; i < m_scenes.size(); i++)
                if (m_scenes[i].name != Rml::String(m_state->scenes[i].c_str())) { changed = true; break; }
        }
        if (changed)
        {
            m_scenes.clear();
            for (auto& s : m_state->scenes)
                m_scenes.push_back({s.c_str()});
            m_handle.DirtyVariable("scenes");
        }
    }

    // Sources
    {
        bool changed = m_sources.size() != m_state->sources.size();
        if (!changed)
        {
            for (size_t i = 0; i < m_sources.size(); i++)
            {
                auto& a = m_sources[i];
                auto& b = m_state->sources[i];
                if (a.id != b.id || a.name != Rml::String(b.name.c_str()) ||
                    a.visible != b.isVisible || a.locked != b.isLocked)
                { changed = true; break; }
            }
        }
        if (changed)
        {
            m_sources.clear();
            for (auto& s : m_state->sources)
                m_sources.push_back({s.id, s.name.c_str(), s.isVisible, s.isLocked, s.sourceKind.c_str()});
            m_handle.DirtyVariable("sources");
        }
    }

    // Audio
    {
        bool changed = m_audioItems.size() != m_state->audio.size();
        if (!changed)
        {
            for (size_t i = 0; i < m_audioItems.size(); i++)
            {
                auto& a = m_audioItems[i];
                auto& b = m_state->audio[i];
                int serverFader = MulToFader(b.volumeMul);
                // Check debounce
                auto dit = m_audioDebounce.find(std::string(a.name.c_str()));
                bool inDebounce = (dit != m_audioDebounce.end() &&
                                   dit->second.userFaderVal >= 0 &&
                                   (m_now - dit->second.lastChange) < SliderDebounceS);
                int displayFader = inDebounce ? dit->second.userFaderVal : serverFader;

                if (a.name != Rml::String(b.name.c_str()) ||
                    a.muted != b.isMuted ||
                    a.faderVal != displayFader)
                { changed = true; break; }
            }
        }
        if (changed)
        {
            m_audioItems.clear();
            for (auto& a : m_state->audio)
            {
                int fader = MulToFader(a.volumeMul);
                auto dit = m_audioDebounce.find(a.name);
                if (dit != m_audioDebounce.end() && dit->second.userFaderVal >= 0 &&
                    (m_now - dit->second.lastChange) < SliderDebounceS)
                    fader = dit->second.userFaderVal;
                m_audioItems.push_back({a.name.c_str(), a.volumeMul, a.isMuted, fader});
            }
            m_handle.DirtyVariable("audio_items");
        }
    }

    // Profiles
    {
        bool changed = m_profiles.size() != m_state->profiles.size();
        if (!changed)
            for (size_t i = 0; i < m_profiles.size(); i++)
                if (m_profiles[i] != Rml::String(m_state->profiles[i].c_str())) { changed = true; break; }
        if (changed)
        {
            m_profiles.clear();
            for (auto& p : m_state->profiles) m_profiles.push_back(p.c_str());
            m_handle.DirtyVariable("profiles");
        }
    }

    // Collections
    {
        bool changed = m_collections.size() != m_state->sceneCollections.size();
        if (!changed)
            for (size_t i = 0; i < m_collections.size(); i++)
                if (m_collections[i] != Rml::String(m_state->sceneCollections[i].c_str())) { changed = true; break; }
        if (changed)
        {
            m_collections.clear();
            for (auto& c : m_state->sceneCollections) m_collections.push_back(c.c_str());
            m_handle.DirtyVariable("collections");
        }
    }

    // Transitions
    {
        bool changed = m_transitions.size() != m_state->transitions.size();
        if (!changed)
            for (size_t i = 0; i < m_transitions.size(); i++)
                if (m_transitions[i] != Rml::String(m_state->transitions[i].c_str())) { changed = true; break; }
        if (changed)
        {
            m_transitions.clear();
            for (auto& t : m_state->transitions) m_transitions.push_back(t.c_str());
            m_handle.DirtyVariable("transitions_list");
        }
    }

    // Filters
    {
        bool changed = m_filters.size() != m_state->filters.size();
        if (!changed)
            for (size_t i = 0; i < m_filters.size(); i++)
            {
                auto& a = m_filters[i]; auto& b = m_state->filters[i];
                if (a.name != Rml::String(b.name.c_str()) || a.enabled != b.enabled) { changed = true; break; }
            }
        if (changed)
        {
            m_filters.clear();
            for (auto& f : m_state->filters)
                m_filters.push_back({f.name.c_str(), f.kind.c_str(), f.enabled});
            m_handle.DirtyVariable("filters");
        }
    }

    // Filter sources (combine sources + scenes)
    {
        Rml::Vector<Rml::String> newFilterSources;
        for (auto& src : m_state->sources) newFilterSources.push_back(src.name.c_str());
        for (auto& sc : m_state->scenes) newFilterSources.push_back(sc.c_str());
        if (newFilterSources.size() != m_filterSources.size())
        {
            m_filterSources = newFilterSources;
            m_handle.DirtyVariable("filter_sources");
        }
    }

    // Input kinds
    if (m_inputKinds.size() != m_state->inputKinds.size())
    {
        m_inputKinds.clear();
        for (auto& k : m_state->inputKinds) m_inputKinds.push_back(k.c_str());
        m_handle.DirtyVariable("input_kinds");
    }

    // Filter kinds
    if (m_filterKinds.size() != m_state->filterKinds.size())
    {
        m_filterKinds.clear();
        for (auto& k : m_state->filterKinds) m_filterKinds.push_back(k.c_str());
        m_handle.DirtyVariable("filter_kinds");
    }

    // Hotkeys
    if (m_hotkeys.size() != m_state->hotkeys.size())
    {
        m_hotkeys.clear();
        for (auto& h : m_state->hotkeys)
            m_hotkeys.push_back({h.c_str(), HumanizeHotkeyName(h).c_str()});
        m_handle.DirtyVariable("hotkeys");
    }

    // Stats
    {
        auto& s = m_state->stats;
        auto newFps = FormatFloat(s.activeFps, 1);
        if (m_statFps != newFps) { m_statFps = newFps; m_handle.DirtyVariable("stat_fps"); }

        auto newCpu = FormatFloat(s.cpuUsage, 1) + "%";
        if (m_statCpu != newCpu) { m_statCpu = newCpu; m_handle.DirtyVariable("stat_cpu"); }

        auto newMem = FormatFloat(s.memoryUsage, 0) + " MB";
        if (m_statMemory != newMem) { m_statMemory = newMem; m_handle.DirtyVariable("stat_memory"); }

        auto newFt = FormatFloat(s.averageFrameRenderTime, 2) + " ms";
        if (m_statFrameTime != newFt) { m_statFrameTime = newFt; m_handle.DirtyVariable("stat_frame_time"); }

        double diskGb = s.availableDiskSpace / 1024.0;
        auto newDisk = FormatFloat(diskGb, 1) + " GB";
        if (m_statDisk != newDisk) { m_statDisk = newDisk; m_handle.DirtyVariable("stat_disk"); }

        char buf[64];
        snprintf(buf, sizeof(buf), "%d/%d", s.renderSkippedFrames, s.renderTotalFrames);
        Rml::String newRS = buf;
        if (m_statRenderSkip != newRS) { m_statRenderSkip = newRS; m_handle.DirtyVariable("stat_render_skip"); }

        snprintf(buf, sizeof(buf), "%d/%d", s.outputSkippedFrames, s.outputTotalFrames);
        Rml::String newOS = buf;
        if (m_statOutputSkip != newOS) { m_statOutputSkip = newOS; m_handle.DirtyVariable("stat_output_skip"); }

        // Color coding
        Rml::String fc = s.activeFps > 55 ? "#4ecca3" : (s.activeFps > 30 ? "#f0c040" : "#e94560");
        if (m_fpsColor != fc) { m_fpsColor = fc; m_handle.DirtyVariable("fps_color"); }

        Rml::String cc = s.cpuUsage < 50 ? "#4ecca3" : (s.cpuUsage < 80 ? "#f0c040" : "#e94560");
        if (m_cpuColor != cc) { m_cpuColor = cc; m_handle.DirtyVariable("cpu_color"); }

        Rml::String dc = diskGb < 1.0 ? "#e94560" : (diskGb < 5.0 ? "#f0c040" : "#eaeaea");
        if (m_diskColor != dc) { m_diskColor = dc; m_handle.DirtyVariable("disk_color"); }

        Rml::String rsc = s.renderSkippedFrames > 0 ? "#e94560" : "#eaeaea";
        if (m_renderSkipColor != rsc) { m_renderSkipColor = rsc; m_handle.DirtyVariable("render_skip_color"); }

        Rml::String osc = s.outputSkippedFrames > 0 ? "#e94560" : "#eaeaea";
        if (m_outputSkipColor != osc) { m_outputSkipColor = osc; m_handle.DirtyVariable("output_skip_color"); }
    }

    // Load settings on first sync
    if (!m_settingsLoaded)
    {
        m_settingsShowNotif = m_state->showNotifications;
        m_settingsNotifMsg = m_state->notificationMessage.c_str();
        m_settingsNotifDur = static_cast<float>(m_state->notificationDuration);
        m_settingsShowRec = m_state->showRecIndicator;

        const char* positions[] = { "top-left", "top-center", "top-right", "bottom-left", "bottom-center", "bottom-right" };
        m_settingsRecPosIdx = 0;
        for (int i = 0; i < 6; i++)
            if (m_state->recIndicatorPosition == positions[i]) { m_settingsRecPosIdx = i; break; }

        m_settingsLoaded = true;
        m_handle.DirtyVariable("settings_show_notif");
        m_handle.DirtyVariable("settings_notif_msg");
        m_handle.DirtyVariable("settings_notif_dur");
        m_handle.DirtyVariable("settings_show_rec");
        m_handle.DirtyVariable("settings_rec_pos_idx");
    }

    // Auto-request stats when on stats tab
    if (m_activeTab == "stats")
    {
        if (!m_state->statsPending && (m_now - m_state->statsRequestTime) > 1.0)
        {
            m_state->statsPending = true;
            m_state->statsRequestTime = m_now;
            m_actions->push_back({"get_stats", {}});
        }
        if (m_state->hotkeys.empty() && !m_state->hotkeysPending)
        {
            m_state->hotkeysPending = true;
            m_actions->push_back({"get_hotkeys", {}});
        }
    }

    // Auto-request audio advanced when on audio tab
    if (m_activeTab == "audio")
    {
        if (m_state->audioAdvanced.empty() && !m_state->audioAdvancedPending && !m_state->audio.empty())
        {
            m_state->audioAdvancedPending = true;
            m_state->audioAdvancedRequestTime = m_now;
            m_actions->push_back({"get_audio_advanced", {}});
        }
        if (!m_state->audioAdvancedPending && !m_state->audio.empty() &&
            (m_now - m_state->audioAdvancedRequestTime) > 5.0)
        {
            m_state->audioAdvancedPending = true;
            m_state->audioAdvancedRequestTime = m_now;
            m_actions->push_back({"get_audio_advanced", {}});
        }

        // Sync advanced data for expanded source
        if (!m_expandedAudioSource.empty())
        {
            AudioAdvancedState* adv = nullptr;
            for (auto& a : m_state->audioAdvanced)
                if (a.name == std::string(m_expandedAudioSource.c_str())) { adv = &a; break; }

            bool hadAdv = m_hasAdvanced;
            m_hasAdvanced = (adv != nullptr);
            if (m_hasAdvanced != hadAdv) m_handle.DirtyVariable("has_advanced");

            if (adv)
            {
                auto& db = m_advAudioDebounce[std::string(m_expandedAudioSource.c_str())];
                bool syncInDb = (m_now - db.lastSyncChange) < SliderDebounceS;
                bool balInDb = (m_now - db.lastBalChange) < SliderDebounceS;

                int syncVal = syncInDb ? db.userSyncMs : adv->syncOffsetMs;
                float balVal = static_cast<float>(balInDb ? db.userBalance : adv->balance);
                int monVal = adv->monitorType;

                if (m_advSyncMs != syncVal) { m_advSyncMs = syncVal; m_handle.DirtyVariable("adv_sync_ms"); }
                if (m_advBalance != balVal) { m_advBalance = balVal; m_handle.DirtyVariable("adv_balance"); }
                if (m_advMonitorType != monVal) { m_advMonitorType = monVal; m_handle.DirtyVariable("adv_monitor_type"); }
            }
        }
        else
        {
            if (m_hasAdvanced) { m_hasAdvanced = false; m_handle.DirtyVariable("has_advanced"); }
        }
    }

    // Auto-request filters when filter tab is open and source selected
    if (m_activeTab == "filters" && !m_filterSelectedSource.empty())
    {
        std::string srcStr(m_filterSelectedSource.c_str());
        if (m_state->filtersSource != srcStr && !m_state->filtersPending)
        {
            m_state->filtersPending = true;
            m_state->filtersSource = srcStr;
            nlohmann::json payload;
            payload["source"] = srcStr;
            m_actions->push_back({"get_filters", payload});
        }
    }
}

// --- Notification system ---

void OverlayDataModel::ShowNotification(const std::string& text, const std::string& colorHex, float duration)
{
    m_notifActive = true;
    m_notifText = text.c_str();
    m_notifColor = colorHex.c_str();
    m_notifAlpha = 1.0f;
    m_notifTimer = 0.0f;
    m_notifDuration = duration;
    m_handle.DirtyVariable("notif_active");
    m_handle.DirtyVariable("notif_text");
    m_handle.DirtyVariable("notif_color");
    m_handle.DirtyVariable("notif_alpha");
}

void OverlayDataModel::UpdateNotification(float dt)
{
    if (!m_notifActive) return;

    m_notifTimer += dt;
    float fadeStart = m_notifDuration * 0.7f;

    if (m_notifTimer >= m_notifDuration)
    {
        m_notifActive = false;
        m_notifAlpha = 0.0f;
        m_handle.DirtyVariable("notif_active");
        m_handle.DirtyVariable("notif_alpha");
    }
    else if (m_notifTimer > fadeStart)
    {
        float fadeProgress = (m_notifTimer - fadeStart) / (m_notifDuration - fadeStart);
        m_notifAlpha = 1.0f - fadeProgress;
        m_handle.DirtyVariable("notif_alpha");
    }
}

// --- REC indicator ---

void OverlayDataModel::SetRecIndicator(bool active, const std::string& position)
{
    if (m_recActive != active)
    {
        m_recActive = active;
        m_recBlinkTimer = 0.0f;
        m_recDotVisible = true;
        m_handle.DirtyVariable("rec_active");
        m_handle.DirtyVariable("rec_dot_visible");
    }
    Rml::String pos = position.c_str();
    if (m_recPosition != pos)
    {
        m_recPosition = pos;
        m_handle.DirtyVariable("rec_position");
    }
}

void OverlayDataModel::UpdateRecIndicator(float dt)
{
    if (!m_recActive) return;

    m_recBlinkTimer += dt;
    if (m_recBlinkTimer >= RecBlinkInterval)
    {
        m_recBlinkTimer -= RecBlinkInterval;
        m_recDotVisible = !m_recDotVisible;
        m_handle.DirtyVariable("rec_dot_visible");
    }
}

// --- Event callbacks ---

void OverlayDataModel::OnSwitchTab(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args)
{
    if (args.empty()) return;
    m_activeTab = args[0].Get<Rml::String>();
    handle.DirtyVariable("active_tab");

    // Default filter source when entering filters tab
    if (m_activeTab == "filters" && m_filterSelectedSource.empty() && !m_filterSources.empty())
    {
        m_filterSelectedSource = m_filterSources[0];
        handle.DirtyVariable("filter_selected_source");
    }
}

void OverlayDataModel::OnToggleStream(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    if (!Debounce("stream", ButtonDebounceS)) return;
    m_actions->push_back({"toggle_stream", {}});
}

void OverlayDataModel::OnToggleRecord(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    if (!Debounce("record", ButtonDebounceS)) return;
    m_actions->push_back({"toggle_record", {}});
}

void OverlayDataModel::OnToggleBuffer(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    if (!Debounce("buffer", ButtonDebounceS)) return;
    m_actions->push_back({"toggle_buffer", {}});
}

void OverlayDataModel::OnSaveReplay(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    if (!Debounce("save", ButtonDebounceS)) return;
    m_actions->push_back({"save_replay", {}});
}

void OverlayDataModel::OnTogglePause(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    if (!Debounce("pause", ButtonDebounceS)) return;
    m_actions->push_back({"toggle_record_pause", {}});
}

void OverlayDataModel::OnSwitchScene(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"switch_scene", payload});
}

void OverlayDataModel::OnToggleSource(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    int itemId = args[0].Get<int>();
    bool visible = args[1].Get<int>() != 0;
    nlohmann::json payload;
    payload["scene"] = m_state->currentScene;
    payload["itemId"] = itemId;
    payload["visible"] = !visible; // Toggle
    m_actions->push_back({"toggle_source", payload});
}

void OverlayDataModel::OnCloseOverlay(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    m_actions->push_back({"close_overlay", {}});
}

void OverlayDataModel::OnSetProfile(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"set_profile", payload});
}

void OverlayDataModel::OnSetCollection(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"set_scene_collection", payload});
}

// --- Audio events ---

void OverlayDataModel::OnToggleMute(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"toggle_mute", payload});
}

void OverlayDataModel::OnSetVolume(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    std::string name = args[0].Get<Rml::String>().c_str();
    int faderVal = args[1].Get<int>();

    m_audioDebounce[name] = {m_now, faderVal};
    double mul = FaderToMul(faderVal);
    nlohmann::json payload;
    payload["name"] = name;
    payload["volumeMul"] = mul;
    m_actions->push_back({"set_volume", payload});
    handle.DirtyVariable("audio_items");
}

void OverlayDataModel::OnExpandAudio(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    Rml::String name = args[0].Get<Rml::String>();
    if (m_expandedAudioSource == name)
        m_expandedAudioSource = "";
    else
        m_expandedAudioSource = name;
    handle.DirtyVariable("expanded_audio");
    handle.DirtyVariable("has_advanced");
}

void OverlayDataModel::OnSetSyncOffset(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    std::string name = args[0].Get<Rml::String>().c_str();
    int syncMs = args[1].Get<int>();
    m_advAudioDebounce[name].lastSyncChange = m_now;
    m_advAudioDebounce[name].userSyncMs = syncMs;
    nlohmann::json payload;
    payload["name"] = name;
    payload["offsetMs"] = syncMs;
    m_actions->push_back({"set_audio_sync_offset", payload});
}

void OverlayDataModel::OnSetBalance(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    std::string name = args[0].Get<Rml::String>().c_str();
    double bal = args[1].Get<double>();
    m_advAudioDebounce[name].lastBalChange = m_now;
    m_advAudioDebounce[name].userBalance = bal;
    nlohmann::json payload;
    payload["name"] = name;
    payload["balance"] = bal;
    m_actions->push_back({"set_audio_balance", payload});
}

void OverlayDataModel::OnSetMonitorType(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    payload["monitorType"] = args[1].Get<int>();
    m_actions->push_back({"set_audio_monitor_type", payload});
}

void OverlayDataModel::OnSetTracks(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 3) return;
    std::string name = args[0].Get<Rml::String>().c_str();
    int trackIdx = args[1].Get<int>();
    bool val = args[2].Get<int>() != 0;

    // Find and update the advanced state
    for (auto& adv : m_state->audioAdvanced)
    {
        if (adv.name == name && trackIdx >= 0 && trackIdx < 6)
        {
            adv.tracks[trackIdx] = !val; // Toggle
            nlohmann::json payload;
            payload["name"] = name;
            nlohmann::json tArr = nlohmann::json::array();
            for (int i = 0; i < 6; i++) tArr.push_back(adv.tracks[i]);
            payload["tracks"] = tArr;
            m_actions->push_back({"set_audio_tracks", payload});
            break;
        }
    }
}

// --- Source management events ---

void OverlayDataModel::OnSelectSource(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    int id = args[0].Get<int>();
    if (m_selectedSourceId == id)
    {
        m_selectedSourceId = -1;
        m_selectedSourceName = "";
    }
    else
    {
        m_selectedSourceId = id;
        for (auto& s : m_state->sources)
            if (s.id == id) { m_selectedSourceName = s.name.c_str(); break; }
    }
    handle.DirtyVariable("selected_source_id");
    handle.DirtyVariable("selected_source_name");
}

void OverlayDataModel::OnSourceUp(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    if (m_selectedSourceId < 0) return;
    int selIdx = -1;
    for (int i = 0; i < (int)m_state->sources.size(); i++)
        if (m_state->sources[i].id == m_selectedSourceId) { selIdx = i; break; }
    if (selIdx <= 0) return;

    int obsIdx = (int)m_state->sources.size() - 1 - selIdx;
    nlohmann::json payload;
    payload["scene"] = m_state->currentScene;
    payload["itemId"] = m_selectedSourceId;
    payload["index"] = obsIdx + 1;
    m_actions->push_back({"reorder_source", payload});
}

void OverlayDataModel::OnSourceDown(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    if (m_selectedSourceId < 0) return;
    int selIdx = -1;
    for (int i = 0; i < (int)m_state->sources.size(); i++)
        if (m_state->sources[i].id == m_selectedSourceId) { selIdx = i; break; }
    if (selIdx < 0 || selIdx >= (int)m_state->sources.size() - 1) return;

    int obsIdx = (int)m_state->sources.size() - 1 - selIdx;
    nlohmann::json payload;
    payload["scene"] = m_state->currentScene;
    payload["itemId"] = m_selectedSourceId;
    payload["index"] = obsIdx - 1;
    m_actions->push_back({"reorder_source", payload});
}

void OverlayDataModel::OnSourceDup(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    if (m_selectedSourceId < 0) return;
    nlohmann::json payload;
    payload["scene"] = m_state->currentScene;
    payload["itemId"] = m_selectedSourceId;
    m_actions->push_back({"duplicate_source", payload});
}

void OverlayDataModel::OnSourceRename(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    payload["newName"] = args[1].Get<Rml::String>().c_str();
    m_actions->push_back({"rename_source", payload});
}

void OverlayDataModel::OnSourceDelete(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList&)
{
    if (m_selectedSourceId < 0) return;
    nlohmann::json payload;
    payload["scene"] = m_state->currentScene;
    payload["itemId"] = m_selectedSourceId;
    m_actions->push_back({"remove_source", payload});
    m_selectedSourceId = -1;
    m_selectedSourceName = "";
    handle.DirtyVariable("selected_source_id");
    handle.DirtyVariable("selected_source_name");
}

void OverlayDataModel::OnSourceCreate(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    nlohmann::json payload;
    payload["scene"] = m_state->currentScene;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    payload["kind"] = args[1].Get<Rml::String>().c_str();
    m_actions->push_back({"create_source", payload});

    if (m_state->inputKinds.empty() && !m_state->inputKindsPending)
    {
        m_state->inputKindsPending = true;
        m_actions->push_back({"get_input_kinds", {}});
    }
}

void OverlayDataModel::OnToggleLock(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    nlohmann::json payload;
    payload["scene"] = m_state->currentScene;
    payload["itemId"] = args[0].Get<int>();
    payload["locked"] = args[1].Get<int>() == 0; // Toggle: if currently locked(1), unlock(false)
    m_actions->push_back({"set_source_locked", payload});
}

// --- Filter events ---

void OverlayDataModel::OnSelectFilterSource(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    m_filterSelectedSource = args[0].Get<Rml::String>();
    m_filterSelectedIdx = -1;
    handle.DirtyVariable("filter_selected_source");
    handle.DirtyVariable("filter_selected_idx");

    m_state->filtersPending = true;
    m_state->filtersSource = std::string(m_filterSelectedSource.c_str());
    nlohmann::json payload;
    payload["source"] = m_state->filtersSource;
    m_actions->push_back({"get_filters", payload});
}

void OverlayDataModel::OnSelectFilter(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    int idx = args[0].Get<int>();
    m_filterSelectedIdx = (m_filterSelectedIdx == idx) ? -1 : idx;
    handle.DirtyVariable("filter_selected_idx");
}

void OverlayDataModel::OnToggleFilter(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    std::string filterName = args[0].Get<Rml::String>().c_str();
    bool enabled = args[1].Get<int>() != 0;
    nlohmann::json payload;
    payload["source"] = std::string(m_filterSelectedSource.c_str());
    payload["filter"] = filterName;
    payload["enabled"] = !enabled; // Toggle
    m_actions->push_back({"set_filter_enabled", payload});
}

void OverlayDataModel::OnFilterUp(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList&)
{
    if (m_filterSelectedIdx <= 0 || m_filterSelectedIdx >= (int)m_state->filters.size()) return;
    auto& sel = m_state->filters[m_filterSelectedIdx];
    nlohmann::json payload;
    payload["source"] = std::string(m_filterSelectedSource.c_str());
    payload["filter"] = sel.name;
    payload["index"] = m_filterSelectedIdx - 1;
    m_actions->push_back({"set_filter_index", payload});
    m_filterSelectedIdx--;
    handle.DirtyVariable("filter_selected_idx");
}

void OverlayDataModel::OnFilterDown(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList&)
{
    if (m_filterSelectedIdx < 0 || m_filterSelectedIdx >= (int)m_state->filters.size() - 1) return;
    auto& sel = m_state->filters[m_filterSelectedIdx];
    nlohmann::json payload;
    payload["source"] = std::string(m_filterSelectedSource.c_str());
    payload["filter"] = sel.name;
    payload["index"] = m_filterSelectedIdx + 1;
    m_actions->push_back({"set_filter_index", payload});
    m_filterSelectedIdx++;
    handle.DirtyVariable("filter_selected_idx");
}

void OverlayDataModel::OnFilterDelete(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList&)
{
    if (m_filterSelectedIdx < 0 || m_filterSelectedIdx >= (int)m_state->filters.size()) return;
    auto& sel = m_state->filters[m_filterSelectedIdx];
    nlohmann::json payload;
    payload["source"] = std::string(m_filterSelectedSource.c_str());
    payload["filter"] = sel.name;
    m_actions->push_back({"remove_filter", payload});
    m_filterSelectedIdx = -1;
    handle.DirtyVariable("filter_selected_idx");

    // Re-request filters
    m_state->filtersPending = true;
    nlohmann::json rp;
    rp["source"] = std::string(m_filterSelectedSource.c_str());
    m_actions->push_back({"get_filters", rp});
}

void OverlayDataModel::OnFilterCreate(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    nlohmann::json payload;
    payload["source"] = std::string(m_filterSelectedSource.c_str());
    payload["name"] = args[0].Get<Rml::String>().c_str();
    payload["kind"] = args[1].Get<Rml::String>().c_str();
    m_actions->push_back({"create_filter", payload});

    m_state->filtersPending = true;
    nlohmann::json rp;
    rp["source"] = std::string(m_filterSelectedSource.c_str());
    m_actions->push_back({"get_filters", rp});
}

void OverlayDataModel::OnRefreshFilters(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    m_state->filtersPending = true;
    nlohmann::json payload;
    payload["source"] = std::string(m_filterSelectedSource.c_str());
    m_actions->push_back({"get_filters", payload});
}

// --- Transition events ---

void OverlayDataModel::OnSetTransition(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"set_transition", payload});
}

void OverlayDataModel::OnSetTransitionDuration(Rml::DataModelHandle handle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    int durMs = args[0].Get<int>();
    m_lastDurChange = m_now;
    m_userDurMs = durMs;
    m_transitionDurMs = durMs;
    handle.DirtyVariable("transition_dur_ms");
    nlohmann::json payload;
    payload["duration"] = durMs;
    m_actions->push_back({"set_transition_duration", payload});
}

void OverlayDataModel::OnToggleStudioMode(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    nlohmann::json payload;
    payload["enabled"] = !m_state->studioModeEnabled;
    m_actions->push_back({"toggle_studio_mode", payload});
}

void OverlayDataModel::OnSetPreviewScene(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"set_preview_scene", payload});
}

void OverlayDataModel::OnTriggerTransition(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    m_actions->push_back({"trigger_transition", {}});
}

// --- Stats events ---

void OverlayDataModel::OnTriggerHotkey(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"trigger_hotkey", payload});
}

// --- Settings events ---

void OverlayDataModel::OnApplySettings(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    const char* positions[] = { "top-left", "top-center", "top-right", "bottom-left", "bottom-center", "bottom-right" };
    int posIdx = (m_settingsRecPosIdx >= 0 && m_settingsRecPosIdx < 6) ? m_settingsRecPosIdx : 0;

    nlohmann::json payload;
    payload["showNotifications"] = m_settingsShowNotif;
    payload["notificationMessage"] = std::string(m_settingsNotifMsg.c_str());
    payload["notificationDuration"] = static_cast<double>(m_settingsNotifDur);
    payload["showRecIndicator"] = m_settingsShowRec;
    payload["recIndicatorPosition"] = std::string(positions[posIdx]);
    m_actions->push_back({"save_settings", payload});

    // Update local state
    m_state->showNotifications = m_settingsShowNotif;
    m_state->notificationMessage = std::string(m_settingsNotifMsg.c_str());
    m_state->notificationDuration = static_cast<double>(m_settingsNotifDur);
    m_state->showRecIndicator = m_settingsShowRec;
    m_state->recIndicatorPosition = std::string(positions[posIdx]);
}

void OverlayDataModel::OnOpenSettings(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
{
    m_actions->push_back({"open_settings", {}});
}

// --- Scene CRUD ---

void OverlayDataModel::OnCreateScene(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"create_scene", payload});
}

void OverlayDataModel::OnRenameScene(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.size() < 2) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    payload["newName"] = args[1].Get<Rml::String>().c_str();
    m_actions->push_back({"rename_scene", payload});
}

void OverlayDataModel::OnDeleteScene(Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
{
    if (args.empty()) return;
    nlohmann::json payload;
    payload["name"] = args[0].Get<Rml::String>().c_str();
    m_actions->push_back({"remove_scene", payload});
}
