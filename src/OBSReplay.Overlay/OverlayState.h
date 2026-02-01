#pragma once
#include <string>
#include <vector>
#include <optional>
#include <mutex>
#include <nlohmann/json.hpp>

struct SceneItemState
{
    int id = 0;
    std::string name;
    bool isVisible = false;
    bool isLocked = false;
    std::string sourceKind;
};

struct AudioSourceState
{
    std::string name;
    double volumeMul = 1.0;
    bool isMuted = false;
};

struct FilterState
{
    std::string name;
    std::string kind;
    bool enabled = false;
    int index = 0;
};

struct StatsState
{
    double cpuUsage = 0.0;
    double memoryUsage = 0.0;
    double availableDiskSpace = 0.0;
    double activeFps = 0.0;
    double averageFrameRenderTime = 0.0;
    int renderSkippedFrames = 0;
    int renderTotalFrames = 0;
    int outputSkippedFrames = 0;
    int outputTotalFrames = 0;
};

struct AudioAdvancedState
{
    std::string name;
    int syncOffsetMs = 0;
    double balance = 0.5;
    int monitorType = 0; // 0=None, 1=MonitorOnly, 2=MonitorAndOutput
    bool tracks[6] = { false, false, false, false, false, false };
};

struct OverlayState
{
    bool connected = false;
    std::vector<std::string> scenes;
    std::string currentScene;
    std::vector<SceneItemState> sources;
    std::vector<AudioSourceState> audio;
    bool isStreaming = false;
    bool isRecording = false;
    bool isRecordingPaused = false;
    bool isBufferActive = false;
    std::optional<bool> hasActiveCapture;
    bool overlayVisible = false;

    // Transitions & studio mode
    std::string currentTransition;
    int transitionDurationMs = 300;
    std::vector<std::string> transitions;
    bool studioModeEnabled = false;
    std::string previewScene;

    // Profiles & collections
    std::string currentProfile;
    std::string currentSceneCollection;
    std::vector<std::string> profiles;
    std::vector<std::string> sceneCollections;

    // Advanced audio (on-demand)
    std::vector<AudioAdvancedState> audioAdvanced;
    bool audioAdvancedPending = false;
    double audioAdvancedRequestTime = 0.0;

    // Source management (on-demand)
    std::vector<std::string> inputKinds;
    bool inputKindsPending = false;

    // Filters (on-demand)
    std::vector<FilterState> filters;
    bool filtersPending = false;
    std::string filtersSource; // which source filters belong to
    std::vector<std::string> filterKinds;
    bool filterKindsPending = false;

    // Stats (on-demand)
    StatsState stats;
    bool statsPending = false;
    double statsRequestTime = 0.0;
    std::vector<std::string> hotkeys;
    bool hotkeysPending = false;

    // Config from host
    std::string toggleHotkey = "F10";
    std::string saveHotkey = "F9";
    std::string recIndicatorPosition = "top-left";
    bool showRecIndicator = true;
    bool showNotifications = true;
    double notificationDuration = 3.0;
    std::string notificationMessage = "REPLAY SAVED";

    void UpdateFromStateJson(const nlohmann::json& j)
    {
        if (j.contains("connected") && j["connected"].is_boolean())
            connected = j["connected"].get<bool>();
        if (j.contains("currentScene") && j["currentScene"].is_string())
            currentScene = j["currentScene"].get<std::string>();
        if (j.contains("isStreaming") && j["isStreaming"].is_boolean())
            isStreaming = j["isStreaming"].get<bool>();
        if (j.contains("isRecording") && j["isRecording"].is_boolean())
            isRecording = j["isRecording"].get<bool>();
        if (j.contains("isRecordingPaused") && j["isRecordingPaused"].is_boolean())
            isRecordingPaused = j["isRecordingPaused"].get<bool>();
        if (j.contains("isBufferActive") && j["isBufferActive"].is_boolean())
            isBufferActive = j["isBufferActive"].get<bool>();

        if (j.contains("hasActiveCapture") && !j["hasActiveCapture"].is_null())
            hasActiveCapture = j["hasActiveCapture"].get<bool>();
        else
            hasActiveCapture = std::nullopt;

        if (j.contains("scenes") && j["scenes"].is_array())
        {
            scenes.clear();
            for (auto& s : j["scenes"])
                if (s.is_string()) scenes.push_back(s.get<std::string>());
        }

        if (j.contains("sources") && j["sources"].is_array())
        {
            sources.clear();
            for (auto& s : j["sources"])
            {
                SceneItemState item;
                item.id = s.value("id", 0);
                item.name = s.value("name", "");
                item.isVisible = s.value("isVisible", false);
                item.isLocked = s.value("isLocked", false);
                item.sourceKind = s.value("sourceKind", "");
                sources.push_back(item);
            }
        }

        if (j.contains("audio") && j["audio"].is_array())
        {
            audio.clear();
            for (auto& a : j["audio"])
            {
                AudioSourceState src;
                src.name = a.value("name", "");
                src.volumeMul = a.value("volumeMul", 1.0);
                src.isMuted = a.value("isMuted", false);
                audio.push_back(src);
            }
        }

        // Transitions
        if (j.contains("currentTransition") && j["currentTransition"].is_string())
            currentTransition = j["currentTransition"].get<std::string>();
        if (j.contains("transitionDuration") && j["transitionDuration"].is_number())
            transitionDurationMs = j["transitionDuration"].get<int>();
        if (j.contains("studioModeEnabled") && j["studioModeEnabled"].is_boolean())
            studioModeEnabled = j["studioModeEnabled"].get<bool>();
        if (j.contains("previewScene") && j["previewScene"].is_string())
            previewScene = j["previewScene"].get<std::string>();

        if (j.contains("transitions") && j["transitions"].is_array())
        {
            transitions.clear();
            for (auto& t : j["transitions"])
                if (t.is_string()) transitions.push_back(t.get<std::string>());
        }

        // Profiles & collections
        if (j.contains("currentProfile") && j["currentProfile"].is_string())
            currentProfile = j["currentProfile"].get<std::string>();
        if (j.contains("currentSceneCollection") && j["currentSceneCollection"].is_string())
            currentSceneCollection = j["currentSceneCollection"].get<std::string>();

        if (j.contains("profiles") && j["profiles"].is_array())
        {
            profiles.clear();
            for (auto& p : j["profiles"])
                if (p.is_string()) profiles.push_back(p.get<std::string>());
        }
        if (j.contains("sceneCollections") && j["sceneCollections"].is_array())
        {
            sceneCollections.clear();
            for (auto& c : j["sceneCollections"])
                if (c.is_string()) sceneCollections.push_back(c.get<std::string>());
        }
    }

    void UpdateFromAudioAdvancedJson(const nlohmann::json& j)
    {
        audioAdvanced.clear();
        audioAdvancedPending = false;
        if (!j.is_array()) return;

        for (auto& item : j)
        {
            AudioAdvancedState adv;
            adv.name = item.value("name", "");
            adv.syncOffsetMs = item.value("syncOffsetMs", 0);
            adv.balance = item.value("balance", 0.5);
            adv.monitorType = item.value("monitorType", 0);

            if (item.contains("tracks") && item["tracks"].is_array())
            {
                auto& arr = item["tracks"];
                for (int i = 0; i < 6 && i < (int)arr.size(); i++)
                    adv.tracks[i] = arr[i].get<bool>();
            }
            audioAdvanced.push_back(adv);
        }
    }

    void UpdateFromInputKindsJson(const nlohmann::json& j)
    {
        inputKinds.clear();
        inputKindsPending = false;
        if (!j.is_array()) return;
        for (auto& k : j)
            if (k.is_string()) inputKinds.push_back(k.get<std::string>());
    }

    void UpdateFromFiltersJson(const nlohmann::json& j)
    {
        filters.clear();
        filtersPending = false;
        if (!j.is_array()) return;
        for (auto& f : j)
        {
            FilterState fs;
            fs.name = f.value("name", "");
            fs.kind = f.value("kind", "");
            fs.enabled = f.value("enabled", false);
            fs.index = f.value("index", 0);
            filters.push_back(fs);
        }
    }

    void UpdateFromFilterKindsJson(const nlohmann::json& j)
    {
        filterKinds.clear();
        filterKindsPending = false;
        if (!j.is_array()) return;
        for (auto& k : j)
            if (k.is_string()) filterKinds.push_back(k.get<std::string>());
    }

    void UpdateFromStatsJson(const nlohmann::json& j)
    {
        statsPending = false;
        stats.cpuUsage = j.value("cpuUsage", 0.0);
        stats.memoryUsage = j.value("memoryUsage", 0.0);
        stats.availableDiskSpace = j.value("availableDiskSpace", 0.0);
        stats.activeFps = j.value("activeFps", 0.0);
        stats.averageFrameRenderTime = j.value("averageFrameRenderTime", 0.0);
        stats.renderSkippedFrames = j.value("renderSkippedFrames", 0);
        stats.renderTotalFrames = j.value("renderTotalFrames", 0);
        stats.outputSkippedFrames = j.value("outputSkippedFrames", 0);
        stats.outputTotalFrames = j.value("outputTotalFrames", 0);
    }

    void UpdateFromHotkeysJson(const nlohmann::json& j)
    {
        hotkeys.clear();
        hotkeysPending = false;
        if (!j.is_array()) return;
        for (auto& h : j)
            if (h.is_string()) hotkeys.push_back(h.get<std::string>());
    }

    void UpdateFromConfigJson(const nlohmann::json& j)
    {
        if (j.contains("toggleHotkey") && j["toggleHotkey"].is_string())
            toggleHotkey = j["toggleHotkey"].get<std::string>();
        if (j.contains("saveHotkey") && j["saveHotkey"].is_string())
            saveHotkey = j["saveHotkey"].get<std::string>();
        if (j.contains("recIndicatorPosition") && j["recIndicatorPosition"].is_string())
            recIndicatorPosition = j["recIndicatorPosition"].get<std::string>();
        if (j.contains("showRecIndicator") && j["showRecIndicator"].is_boolean())
            showRecIndicator = j["showRecIndicator"].get<bool>();
        if (j.contains("showNotifications") && j["showNotifications"].is_boolean())
            showNotifications = j["showNotifications"].get<bool>();
        if (j.contains("notificationDuration") && j["notificationDuration"].is_number())
            notificationDuration = j["notificationDuration"].get<double>();
        if (j.contains("notificationMessage") && j["notificationMessage"].is_string())
            notificationMessage = j["notificationMessage"].get<std::string>();
    }
};
