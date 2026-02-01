using System.Diagnostics;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using OBSReplay.Host.Models;

namespace OBSReplay.Host.Services;

public class ObsWebSocketService : IDisposable
{
    private readonly OBSWebsocket _obs = new();
    private readonly object _lock = new();

    /// <summary>
    /// Timestamp of the last successful API call. Used to detect stale connections.
    /// </summary>
    public DateTime LastSuccessfulCall { get; private set; } = DateTime.MinValue;

    public bool IsConnected => _obs.IsConnected;

    public bool Connect(int port, string password)
    {
        try
        {
            // If already connected and healthy, don't reconnect
            if (_obs.IsConnected)
            {
                try
                {
                    _obs.GetVersion();
                    LastSuccessfulCall = DateTime.UtcNow;
                    return true;
                }
                catch
                {
                    // Connection is stale, disconnect and retry below
                    Debug.WriteLine("OBS connection stale, reconnecting...");
                }
            }

            // Disconnect any stale connection before reconnecting
            try { _obs.Disconnect(); } catch { }
            Thread.Sleep(300);

            var url = $"ws://127.0.0.1:{port}";
            _obs.ConnectAsync(url, password);
            // ConnectAsync is actually synchronous despite the name in v5.0.1

            if (_obs.IsConnected)
            {
                LastSuccessfulCall = DateTime.UtcNow;
                // Verify connection is actually working with a lightweight call
                _obs.GetVersion();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OBS connect failed: {ex.Message}");
            return false;
        }
    }

    public void Disconnect()
    {
        try { _obs.Disconnect(); }
        catch { /* ignore */ }
    }

    // --- Scenes ---

    public List<string> GetScenes()
    {
        return SafeCall(() =>
        {
            var info = _obs.GetSceneList();
            return info.Scenes.Select(s => s.Name).ToList();
        }, []);
    }

    public string? GetCurrentScene()
    {
        return SafeCall(() => _obs.GetCurrentProgramScene(), null);
    }

    public void SetScene(string name)
    {
        SafeAction(() => _obs.SetCurrentProgramScene(name));
    }

    public List<Models.SceneItem> GetSceneItems(string sceneName)
    {
        return SafeCall(() =>
        {
            var items = _obs.GetSceneItemList(sceneName);
            return items.Select(item => new Models.SceneItem
            {
                Id = item.ItemId,
                Name = item.SourceName,
                IsVisible = _obs.GetSceneItemEnabled(sceneName, item.ItemId)
            }).ToList();
        }, []);
    }

    public void SetSourceVisible(string sceneName, int itemId, bool visible)
    {
        SafeAction(() => _obs.SetSceneItemEnabled(sceneName, itemId, visible));
    }

    // --- Audio ---

    public List<string> GetAudioInputNames()
    {
        return SafeCall(() =>
        {
            var inputs = _obs.GetInputList();
            // Filter to audio-capable inputs by checking if they have volume
            var audioNames = new List<string>();
            foreach (var input in inputs)
            {
                try
                {
                    _obs.GetInputVolume(input.InputName);
                    audioNames.Add(input.InputName);
                }
                catch
                {
                    // Not an audio input
                }
            }
            return audioNames;
        }, []);
    }

    public List<Models.AudioSource> GetAudioLevels(List<string> names)
    {
        return SafeCall(() =>
        {
            var result = new List<Models.AudioSource>();
            foreach (var name in names)
            {
                try
                {
                    var vol = _obs.GetInputVolume(name);
                    var muted = _obs.GetInputMute(name);
                    result.Add(new Models.AudioSource
                    {
                        Name = name,
                        VolumeMul = vol.VolumeMul,
                        IsMuted = muted
                    });
                }
                catch
                {
                    result.Add(new Models.AudioSource
                    {
                        Name = name,
                        VolumeMul = 1.0,
                        IsMuted = false
                    });
                }
            }
            return result;
        }, []);
    }

    public void SetInputVolume(string name, double mul)
    {
        SafeAction(() => _obs.SetInputVolume(name, (float)mul));
    }

    public void ToggleMute(string name)
    {
        SafeAction(() => _obs.ToggleInputMute(name));
    }

    // --- Preview ---

    public string? GetScreenshotBase64(int width, int height)
    {
        return SafeCall(() =>
        {
            var scene = _obs.GetCurrentProgramScene();
            if (string.IsNullOrEmpty(scene))
                return null;

            var result = _obs.GetSourceScreenshot(scene, "png", width, height, -1);
            if (result == null || result.Length > Constants.MaxScreenshotBytes)
                return null;

            // obs-websocket-dotnet returns "data:image/png;base64,<data>"
            // Strip the data URI prefix if present
            var commaIdx = result.IndexOf(',');
            return commaIdx >= 0 ? result[(commaIdx + 1)..] : result;
        }, null);
    }

    public bool? HasActiveCapture()
    {
        return SafeCall<bool?>(() =>
        {
            var scene = _obs.GetCurrentProgramScene();
            if (string.IsNullOrEmpty(scene))
                return null;

            var items = _obs.GetSceneItemList(scene);
            foreach (var item in items)
            {
                var kind = item.SourceKind?.ToLowerInvariant() ?? "";
                if (kind.Contains("capture"))
                {
                    var active = _obs.GetSourceActive(item.SourceName);
                    if (active.VideoActive)
                        return true;
                }
            }
            return false;
        }, null);
    }

    // --- Stream/Record/Buffer Status ---

    public bool GetStreamStatus()
    {
        return SafeCall(() => _obs.GetStreamStatus().IsActive, false);
    }

    public bool GetRecordStatus()
    {
        return SafeCall(() => _obs.GetRecordStatus().IsRecording, false);
    }

    public bool GetBufferStatus()
    {
        return SafeCall(() => _obs.GetReplayBufferStatus(), false);
    }

    public bool GetVirtualCamStatus()
    {
        return SafeCall(() => _obs.GetVirtualCamStatus().IsActive, false);
    }

    // --- Controls ---

    public void ToggleStream() => SafeAction(() => _obs.ToggleStream());
    public void ToggleRecord() => SafeAction(() => _obs.ToggleRecord());
    public void ToggleBuffer() => SafeAction(() => _obs.ToggleReplayBuffer());
    public void StartBuffer() => SafeAction(() => _obs.StartReplayBuffer());
    public void StopBuffer() => SafeAction(() => _obs.StopReplayBuffer());

    public bool SaveBuffer()
    {
        return SafeCall(() =>
        {
            _obs.SaveReplayBuffer();
            return true;
        }, false);
    }

    public void ToggleVirtualCam() => SafeAction(() => _obs.ToggleVirtualCam());
    public void ToggleRecordPause() => SafeAction(() => _obs.ToggleRecordPause());

    // --- Advanced Audio ---

    public List<Models.AudioAdvancedInfo> GetAudioAdvancedInfo(List<string> names)
    {
        return SafeCall(() =>
        {
            var result = new List<Models.AudioAdvancedInfo>();
            foreach (var name in names)
            {
                try
                {
                    var info = new Models.AudioAdvancedInfo { Name = name };

                    try
                    {
                        // OBS stores sync offset in nanoseconds, convert to ms
                        var offsetNs = _obs.GetInputAudioSyncOffset(name);
                        info.SyncOffsetMs = (int)(offsetNs / 1_000_000);
                    }
                    catch { /* leave default 0 */ }

                    try
                    {
                        info.Balance = _obs.GetInputAudioBalance(name);
                    }
                    catch { /* leave default 0.5 */ }

                    try
                    {
                        var monitorType = _obs.GetInputAudioMonitorType(name);
                        info.MonitorType = monitorType switch
                        {
                            "OBS_MONITORING_TYPE_MONITOR_ONLY" => 1,
                            "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT" => 2,
                            _ => 0 // OBS_MONITORING_TYPE_NONE
                        };
                    }
                    catch { /* leave default 0 */ }

                    try
                    {
                        var tracks = _obs.GetInputAudioTracks(name);
                        info.Tracks[0] = tracks.IsTrack1Active;
                        info.Tracks[1] = tracks.IsTrack2Active;
                        info.Tracks[2] = tracks.IsTrack3Active;
                        info.Tracks[3] = tracks.IsTrack4Active;
                        info.Tracks[4] = tracks.IsTrack5Active;
                        info.Tracks[5] = tracks.IsTrack6Active;
                    }
                    catch { /* leave default all-false */ }

                    result.Add(info);
                }
                catch { /* skip this source entirely */ }
            }
            return result;
        }, []);
    }

    public void SetInputAudioSyncOffset(string name, int offsetMs)
    {
        // Convert ms to nanoseconds for OBS
        SafeAction(() => _obs.SetInputAudioSyncOffset(name, offsetMs * 1_000_000));
    }

    public void SetInputAudioBalance(string name, double balance)
    {
        SafeAction(() => _obs.SetInputAudioBalance(name, balance));
    }

    public void SetInputAudioMonitorType(string name, int monitorType)
    {
        var typeStr = monitorType switch
        {
            1 => "OBS_MONITORING_TYPE_MONITOR_ONLY",
            2 => "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT",
            _ => "OBS_MONITORING_TYPE_NONE"
        };
        SafeAction(() => _obs.SetInputAudioMonitorType(name, typeStr));
    }

    public void SetInputAudioTracks(string name, bool[] tracks)
    {
        SafeAction(() =>
        {
            var st = new OBSWebsocketDotNet.Types.SourceTracks
            {
                IsTrack1Active = tracks.Length > 0 && tracks[0],
                IsTrack2Active = tracks.Length > 1 && tracks[1],
                IsTrack3Active = tracks.Length > 2 && tracks[2],
                IsTrack4Active = tracks.Length > 3 && tracks[3],
                IsTrack5Active = tracks.Length > 4 && tracks[4],
                IsTrack6Active = tracks.Length > 5 && tracks[5],
            };
            _obs.SetInputAudioTracks(name, st);
        });
    }

    // --- Source Management (Phase 3) ---

    public List<string> GetInputKindList()
    {
        return SafeCall(() => _obs.GetInputKindList(true).ToList(), []);
    }

    public int CreateInput(string sceneName, string inputName, string inputKind)
    {
        return SafeCall(() => _obs.CreateInput(sceneName, inputName, inputKind, null, true), -1);
    }

    public void RemoveSceneItem(string sceneName, int sceneItemId)
    {
        SafeAction(() => _obs.RemoveSceneItem(sceneName, sceneItemId));
    }

    public void DuplicateSceneItem(string sceneName, int sceneItemId)
    {
        SafeAction(() => _obs.DuplicateSceneItem(sceneName, sceneItemId, sceneName));
    }

    public void SetSceneItemIndex(string sceneName, int sceneItemId, int index)
    {
        SafeAction(() => _obs.SetSceneItemIndex(sceneName, sceneItemId, index));
    }

    public bool GetSceneItemLocked(string sceneName, int sceneItemId)
    {
        return SafeCall(() => _obs.GetSceneItemLocked(sceneName, sceneItemId), false);
    }

    public void SetSceneItemLocked(string sceneName, int sceneItemId, bool locked)
    {
        SafeAction(() => _obs.SetSceneItemLocked(sceneName, sceneItemId, locked));
    }

    public void SetInputName(string inputName, string newName)
    {
        SafeAction(() => _obs.SetInputName(inputName, newName));
    }

    // --- Scene Management (Phase 7) ---

    public void CreateScene(string name) => SafeAction(() => _obs.CreateScene(name));
    public void RemoveScene(string name) => SafeAction(() => _obs.RemoveScene(name));
    public void RenameScene(string name, string newName) => SafeAction(() => _obs.SetSceneName(name, newName));

    // --- Filters (Phase 4) ---

    public List<Models.FilterInfo> GetSourceFilters(string sourceName)
    {
        return SafeCall(() =>
        {
            var filters = _obs.GetSourceFilterList(sourceName);
            var result = new List<Models.FilterInfo>();
            int idx = 0;
            foreach (var f in filters)
            {
                result.Add(new Models.FilterInfo
                {
                    Name = f.Name,
                    Kind = f.Kind,
                    Enabled = f.IsEnabled,
                    Index = idx++
                });
            }
            return result;
        }, []);
    }

    public List<string> GetFilterKindList()
    {
        // obs-websocket-dotnet has no GetSourceFilterKindList wrapper;
        // use the raw SendRequest to call GetSourceFilterKindList.
        return SafeCall(() =>
        {
            var resp = _obs.SendRequest("GetSourceFilterKindList", null);
            if (resp != null && resp["sourceFilterKinds"] is Newtonsoft.Json.Linq.JArray arr)
                return arr.Select(t => t.ToString()).ToList();
            return new List<string>();
        }, []);
    }

    public void SetFilterEnabled(string sourceName, string filterName, bool enabled)
    {
        SafeAction(() => _obs.SetSourceFilterEnabled(sourceName, filterName, enabled));
    }

    public void SetFilterIndex(string sourceName, string filterName, int index)
    {
        SafeAction(() => _obs.SetSourceFilterIndex(sourceName, filterName, index));
    }

    public void CreateFilter(string sourceName, string filterName, string filterKind)
    {
        SafeAction(() => _obs.CreateSourceFilter(sourceName, filterName, filterKind, new Newtonsoft.Json.Linq.JObject()));
    }

    public void RemoveFilter(string sourceName, string filterName)
    {
        SafeAction(() => _obs.RemoveSourceFilter(sourceName, filterName));
    }

    public void RenameFilter(string sourceName, string filterName, string newName)
    {
        SafeAction(() => _obs.SetSourceFilterName(sourceName, filterName, newName));
    }

    // --- Transitions (Phase 5) ---

    public void SetCurrentTransition(string name) => SafeAction(() => _obs.SetCurrentSceneTransition(name));
    public void SetTransitionDuration(int ms) => SafeAction(() => _obs.SetCurrentSceneTransitionDuration(ms));
    public void SetStudioModeEnabled(bool enabled) => SafeAction(() => _obs.SetStudioModeEnabled(enabled));
    public void SetCurrentPreviewScene(string name) => SafeAction(() => _obs.SetCurrentPreviewScene(name));
    public void TriggerStudioModeTransition() => SafeAction(() => _obs.TriggerStudioModeTransition());

    // --- Stats (Phase 6) ---

    public Models.ObsStatsData? GetStats()
    {
        return SafeCall<Models.ObsStatsData?>(() =>
        {
            var s = _obs.GetStats();
            return new Models.ObsStatsData
            {
                CpuUsage = s.CpuUsage,
                MemoryUsage = s.MemoryUsage,
                AvailableDiskSpace = s.FreeDiskSpace,
                ActiveFps = s.FPS,
                AverageFrameRenderTime = s.AverageFrameTime,
                RenderSkippedFrames = (int)s.RenderMissedFrames,
                RenderTotalFrames = (int)s.RenderTotalFrames,
                OutputSkippedFrames = (int)s.OutputSkippedFrames,
                OutputTotalFrames = (int)s.OutputTotalFrames
            };
        }, null);
    }

    public List<string> GetHotkeyList()
    {
        return SafeCall(() => _obs.GetHotkeyList().ToList(), []);
    }

    public void TriggerHotkey(string name) => SafeAction(() => _obs.TriggerHotkeyByName(name));

    // --- Profiles & Collections ---

    public void SetCurrentProfile(string name) => SafeAction(() => _obs.SetCurrentProfile(name));
    public void SetCurrentSceneCollection(string name) => SafeAction(() => _obs.SetCurrentSceneCollection(name));

    // --- Directory ---

    public string? GetRecordDirectory()
    {
        return SafeCall(() => _obs.GetRecordDirectory(), null);
    }

    // --- Fetch full state snapshot ---

    public ObsState FetchState(
        List<string>? cachedScenes,
        List<string>? cachedAudioNames,
        string? lastScene)
    {
        var state = new ObsState { Connected = IsConnected };

        if (!IsConnected)
            return state;

        try
        {
            lock (_lock)
            {
                // Quick connection health check - if the socket itself is gone,
                // bail out immediately rather than making individual API calls
                if (!_obs.IsConnected)
                {
                    state.Connected = false;
                    return state;
                }

                // Scenes
                if (cachedScenes != null)
                {
                    state.Scenes = cachedScenes;
                }
                else
                {
                    try
                    {
                        var info = _obs.GetSceneList();
                        state.Scenes = info.Scenes.Select(s => s.Name).ToList();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"FetchState: GetSceneList failed: {ex.Message}");
                    }
                }

                try { state.CurrentScene = _obs.GetCurrentProgramScene(); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FetchState: GetCurrentProgramScene failed: {ex.Message}");
                }

                // Sources
                if (!string.IsNullOrEmpty(state.CurrentScene))
                {
                    try
                    {
                        var items = _obs.GetSceneItemList(state.CurrentScene);
                        state.Sources = items.Select(item =>
                        {
                            var si = new Models.SceneItem
                            {
                                Id = item.ItemId,
                                Name = item.SourceName,
                                IsVisible = _obs.GetSceneItemEnabled(state.CurrentScene, item.ItemId),
                                SourceKind = item.SourceKind ?? ""
                            };
                            try { si.IsLocked = _obs.GetSceneItemLocked(state.CurrentScene, item.ItemId); }
                            catch { /* leave default false */ }
                            return si;
                        }).ToList();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"FetchState: GetSceneItemList failed: {ex.Message}");
                    }
                }

                // Audio
                var audioNames = cachedAudioNames;
                if (audioNames == null)
                {
                    try
                    {
                        var inputs = _obs.GetInputList();
                        audioNames = new List<string>();
                        foreach (var input in inputs)
                        {
                            try
                            {
                                _obs.GetInputVolume(input.InputName);
                                audioNames.Add(input.InputName);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"FetchState: GetInputList failed: {ex.Message}");
                        audioNames = cachedAudioNames ?? [];
                    }
                }
                state.Audio = new List<Models.AudioSource>();
                foreach (var name in audioNames)
                {
                    try
                    {
                        var vol = _obs.GetInputVolume(name);
                        var muted = _obs.GetInputMute(name);
                        state.Audio.Add(new Models.AudioSource
                        {
                            Name = name,
                            VolumeMul = vol.VolumeMul,
                            IsMuted = muted
                        });
                    }
                    catch
                    {
                        state.Audio.Add(new Models.AudioSource
                        {
                            Name = name,
                            VolumeMul = 1.0,
                            IsMuted = false
                        });
                    }
                }

                // Status - each wrapped individually so one failure doesn't kill the rest
                try { state.IsStreaming = _obs.GetStreamStatus().IsActive; }
                catch { /* leave default false */ }

                try
                {
                    var recStatus = _obs.GetRecordStatus();
                    state.IsRecording = recStatus.IsRecording;
                    state.IsRecordingPaused = recStatus.IsRecordingPaused;
                }
                catch { /* leave default false */ }

                try { state.IsBufferActive = _obs.GetReplayBufferStatus(); }
                catch { /* leave default false */ }

                // Transitions
                try
                {
                    var transInfo = _obs.GetSceneTransitionList();
                    state.Transitions = transInfo.Transitions
                        .Select(t => t.Name).ToList();
                    state.CurrentTransition = transInfo.CurrentTransition;
                }
                catch { /* leave defaults */ }

                try
                {
                    var currentTrans = _obs.GetCurrentSceneTransition();
                    state.TransitionDuration = currentTrans.Duration ?? 0;
                }
                catch { /* leave default 0 */ }

                // Studio mode
                try { state.StudioModeEnabled = _obs.GetStudioModeEnabled(); }
                catch { /* leave default false */ }

                if (state.StudioModeEnabled)
                {
                    try { state.PreviewScene = _obs.GetCurrentPreviewScene(); }
                    catch { /* leave null */ }
                }

                // Profiles & collections
                try
                {
                    var profiles = _obs.GetProfileList();
                    state.CurrentProfile = profiles.CurrentProfileName;
                    state.Profiles = profiles.Profiles.ToList();
                }
                catch { /* leave defaults */ }

                try
                {
                    state.CurrentSceneCollection = _obs.GetCurrentSceneCollection();
                    state.SceneCollections = _obs.GetSceneCollectionList();
                }
                catch { /* leave defaults */ }

                // Capture check (only when buffer active)
                if (state.IsBufferActive && !string.IsNullOrEmpty(state.CurrentScene))
                {
                    try
                    {
                        var items = _obs.GetSceneItemList(state.CurrentScene);
                        foreach (var item in items)
                        {
                            var kind = item.SourceKind?.ToLowerInvariant() ?? "";
                            if (kind.Contains("capture"))
                            {
                                var active = _obs.GetSourceActive(item.SourceName);
                                if (active.VideoActive)
                                {
                                    state.HasActiveCapture = true;
                                    break;
                                }
                            }
                        }
                        state.HasActiveCapture ??= false;
                    }
                    catch { /* leave null */ }
                }
            }

            // Connection is still alive - report as connected even if some
            // individual API calls failed. Only _obs.IsConnected determines
            // true connection state.
            state.Connected = _obs.IsConnected;
            if (state.Connected)
                LastSuccessfulCall = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            // Catch-all for truly unexpected errors (shouldn't reach here
            // since individual calls are wrapped above)
            Debug.WriteLine($"FetchState unexpected error: {ex.Message}");
            state.Connected = _obs.IsConnected;
        }

        return state;
    }

    // --- Helpers ---

    private T SafeCall<T>(Func<T> func, T defaultValue)
    {
        try
        {
            T result;
            lock (_lock)
                result = func();
            LastSuccessfulCall = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OBS call failed: {ex.Message}");
            return defaultValue;
        }
    }

    private void SafeAction(Action action)
    {
        try
        {
            lock (_lock)
                action();
            LastSuccessfulCall = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OBS action failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
