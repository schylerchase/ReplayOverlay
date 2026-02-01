#pragma once
#include <RmlUi/Core.h>
#include "OverlayState.h"
#include "IpcClient.h"
#include <string>
#include <vector>
#include <unordered_map>
#include <functional>

class OverlayDataModel
{
public:
    bool Init(Rml::Context* ctx, OverlayState* state,
              std::vector<IpcMessage>* outActions);

    // Push changes from OverlayState into the data model (call after IPC updates)
    void SyncFromState();

    // Provide elapsed time for debounce logic
    void SetElapsedTime(double t) { m_now = t; }

    // Notification system
    void ShowNotification(const std::string& text, const std::string& colorHex, float duration);
    void UpdateNotification(float dt);

    // REC indicator
    void SetRecIndicator(bool active, const std::string& position);
    void UpdateRecIndicator(float dt);

private:
    // Event callbacks (called from RML data-event-click)
    void OnSwitchTab(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnToggleStream(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnToggleRecord(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnToggleBuffer(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSaveReplay(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnTogglePause(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSwitchScene(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnToggleSource(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnCloseOverlay(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetProfile(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetCollection(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);

    // Audio events
    void OnToggleMute(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetVolume(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnExpandAudio(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetSyncOffset(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetBalance(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetMonitorType(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetTracks(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);

    // Source management events
    void OnSelectSource(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSourceUp(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSourceDown(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSourceDup(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSourceRename(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSourceDelete(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSourceCreate(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnToggleLock(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);

    // Filter events
    void OnSelectFilterSource(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSelectFilter(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnToggleFilter(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnFilterUp(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnFilterDown(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnFilterDelete(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnFilterCreate(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnRefreshFilters(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);

    // Transition events
    void OnSetTransition(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetTransitionDuration(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnToggleStudioMode(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnSetPreviewScene(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnTriggerTransition(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);

    // Stats events
    void OnTriggerHotkey(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);

    // Settings events
    void OnApplySettings(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnOpenSettings(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);

    // Scene CRUD
    void OnCreateScene(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnRenameScene(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);
    void OnDeleteScene(Rml::DataModelHandle handle, Rml::Event& ev, const Rml::VariantList& args);

    // Debounce helper
    bool Debounce(const std::string& key, double interval = 2.0);

    Rml::DataModelHandle m_handle;
    OverlayState* m_state = nullptr;
    std::vector<IpcMessage>* m_actions = nullptr;
    double m_now = 0.0;

    // UI-only state (not in OverlayState)
    Rml::String m_activeTab = "main";
    int m_selectedSourceId = -1;
    Rml::String m_selectedSourceName;
    Rml::String m_expandedAudioSource;
    Rml::String m_filterSelectedSource;
    int m_filterSelectedIdx = -1;
    Rml::String m_hotkeyFilter;

    // Notification state
    bool m_notifActive = false;
    Rml::String m_notifText;
    Rml::String m_notifColor = "#4ecca3";
    float m_notifAlpha = 0.0f;
    float m_notifTimer = 0.0f;
    float m_notifDuration = 3.0f;

    // REC indicator state
    bool m_recActive = false;
    bool m_recDotVisible = true;
    Rml::String m_recPosition = "top-left";
    float m_recBlinkTimer = 0.0f;
    static constexpr float RecBlinkInterval = 0.5f;

    // Settings form state
    bool m_settingsShowNotif = true;
    Rml::String m_settingsNotifMsg = "REPLAY SAVED";
    float m_settingsNotifDur = 3.0f;
    bool m_settingsShowRec = true;
    int m_settingsRecPosIdx = 0;
    bool m_settingsLoaded = false;

    // Debounce tracking
    std::unordered_map<std::string, double> m_debounceTimers;
    static constexpr double ButtonDebounceS = 2.0;
    static constexpr double SliderDebounceS = 2.0;

    // Audio debounce
    struct AudioDebounce {
        double lastChange = 0.0;
        int userFaderVal = -1;
    };
    std::unordered_map<std::string, AudioDebounce> m_audioDebounce;

    struct AdvAudioDebounce {
        double lastSyncChange = 0.0;
        int userSyncMs = 0;
        double lastBalChange = 0.0;
        double userBalance = 0.5;
    };
    std::unordered_map<std::string, AdvAudioDebounce> m_advAudioDebounce;

    // Transition duration debounce
    double m_lastDurChange = 0.0;
    int m_userDurMs = 300;

    // Bound data (copies of OverlayState for RmlUi binding)
    // RmlUi needs stable pointers, so we maintain local copies and sync
    Rml::String m_connected;
    Rml::String m_currentScene;
    bool m_isStreaming = false;
    bool m_isRecording = false;
    bool m_isRecordingPaused = false;
    bool m_isBufferActive = false;
    bool m_hasActiveCapture = false;
    Rml::String m_currentProfile;
    Rml::String m_currentCollection;
    Rml::String m_currentTransition;
    int m_transitionDurMs = 300;
    bool m_studioModeEnabled = false;
    Rml::String m_previewScene;
    Rml::String m_toggleHotkey;
    Rml::String m_saveHotkey;

    // Bound arrays
    struct SceneItem { Rml::String name; };
    struct SourceItem { int id; Rml::String name; bool visible; bool locked; Rml::String kind; };
    struct AudioItem { Rml::String name; double volumeMul; bool muted; int faderVal; };
    struct FilterItem { Rml::String name; Rml::String kind; bool enabled; };
    struct HotkeyItem { Rml::String rawName; Rml::String displayName; };

    Rml::Vector<SceneItem> m_scenes;
    Rml::Vector<SourceItem> m_sources;
    Rml::Vector<AudioItem> m_audioItems;
    Rml::Vector<Rml::String> m_profiles;
    Rml::Vector<Rml::String> m_collections;
    Rml::Vector<Rml::String> m_transitions;
    Rml::Vector<FilterItem> m_filters;
    Rml::Vector<Rml::String> m_filterSources;
    Rml::Vector<Rml::String> m_inputKinds;
    Rml::Vector<Rml::String> m_filterKinds;
    Rml::Vector<HotkeyItem> m_hotkeys;

    // Stats
    Rml::String m_statFps;
    Rml::String m_statCpu;
    Rml::String m_statMemory;
    Rml::String m_statFrameTime;
    Rml::String m_statDisk;
    Rml::String m_statRenderSkip;
    Rml::String m_statOutputSkip;
    Rml::String m_fpsColor;
    Rml::String m_cpuColor;
    Rml::String m_diskColor;
    Rml::String m_renderSkipColor;
    Rml::String m_outputSkipColor;

    // Audio advanced for expanded source
    bool m_hasAdvanced = false;
    int m_advSyncMs = 0;
    float m_advBalance = 0.5f;
    int m_advMonitorType = 0;
    bool m_advTracks[6] = {};
};
