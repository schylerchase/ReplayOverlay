using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using OBSReplay.Host.Models;
using OBSReplay.Host.Services;
using OBSReplay.Host.Views;

namespace OBSReplay.Host;

public partial class App : System.Windows.Application
{
    private AppConfig _config = new();
    private readonly ConfigService _configService = new();
    private readonly ObsWebSocketService _obs = new();
    private readonly IpcServerService _ipc = new();
    private readonly OverlayProcessService _overlayProcess = new();
    private readonly HotkeyService _hotkeys = new();
    private readonly TrayIconService _tray = new();
    private readonly GameDetectionService _gameDetection = new();
    private readonly ReplayFileService _replayFiles = new();

    private DispatcherTimer? _statusTimer;
    private DispatcherTimer? _previewTimer;

    // Caches for expensive list fetches
    private List<string>? _scenesCache;
    private DateTime _scenesCacheTime;
    private List<string>? _audioNamesCache;
    private DateTime _audioNamesCacheTime;

    // Buffer monitoring
    private bool _lastBufferStatus;
    private bool _overlayVisible;

    // Save guard (0 = idle, 1 = in progress; atomically swapped via Interlocked)
    private int _saveInProgress;

    // Reconnection tracking (0 = idle, 1 = connecting; atomically swapped via Interlocked)
    private int _connecting;
    private DateTime _lastReconnectAttempt = DateTime.MinValue;
    private const double ReconnectIntervalS = 10.0;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load config
        _config = _configService.Load();

        // First run wizard
        if (_configService.IsFirstRun())
        {
            var wizard = new SetupWizardWindow();
            if (wizard.ShowDialog() == true && wizard.Result != null)
            {
                _config = wizard.Result;
                _configService.Save(_config);
            }
            else
            {
                _configService.Save(_config);
            }
        }

        // --- IPC ---
        _ipc.MessageReceived += OnOverlayMessage;
        _ipc.Start();

        // --- Overlay process ---
        _overlayProcess.Start();

        // --- Hotkeys ---
        _hotkeys.Initialize();
        _hotkeys.ToggleHotkeyPressed += OnToggleHotkey;
        _hotkeys.SaveHotkeyPressed += OnSaveHotkey;

        // --- Tray icon (before hotkey registration so balloon notifications work) ---
        _tray.Initialize();
        _tray.ShowOverlayClicked += () => ToggleOverlay();
        _tray.SaveReplayClicked += () => Task.Run(HandleSaveReplay);
        _tray.StartBufferClicked += () => Task.Run(() => _obs.StartBuffer());
        _tray.StopBufferClicked += () => Task.Run(() => _obs.StopBuffer());
        _tray.SettingsClicked += OnSettingsClicked;
        _tray.OpenLibraryClicked += _ => OpenLibrary();
        _tray.RestartClicked += OnRestart;
        _tray.ExitClicked += () => Shutdown();

        // --- Register hotkeys (after tray so balloon notifications work) ---
        LogToFile($"HotkeyEnabled={_config.HotkeyEnabled}, Toggle='{_config.ToggleHotkey}', Save='{_config.SaveHotkey}'");
        if (_config.HotkeyEnabled)
            RegisterHotkeysAndNotify(_config.ToggleHotkey, _config.SaveHotkey);
        else
            LogToFile("Hotkeys DISABLED in config -- skipping registration");

        // --- File watcher ---
        _replayFiles.Start(_config.WatchFolder);

        // --- OBS connection (background) ---
        Task.Run(ConnectToObs).ContinueWith(
            t => Debug.WriteLine($"ConnectToObs faulted: {t.Exception?.GetBaseException().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);

        // --- Status polling timer (1 Hz) ---
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.StatusIntervalMs)
        };
        _statusTimer.Tick += OnStatusTick;
        _statusTimer.Start();

        // --- Preview polling timer (4 Hz, starts paused) ---
        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.PreviewIntervalMs)
        };
        _previewTimer.Tick += OnPreviewTick;

        Debug.WriteLine("App started.");
    }

    // --- OBS Connection ---

    private async Task ConnectToObs()
    {
        // Auto-launch OBS if configured
        if (_config.AutoLaunchObs && AdminService.IsValidObsExecutable(_config.ObsPath))
        {
            if (!IsObsRunning())
            {
                try
                {
                    var obsDir = Path.GetDirectoryName(_config.ObsPath);
                    var psi = new ProcessStartInfo
                    {
                        FileName = _config.ObsPath,
                        Arguments = "--minimize-to-tray",
                        WorkingDirectory = obsDir ?? "",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    Debug.WriteLine("Launched OBS.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to launch OBS: {ex.Message}");
                }
            }
        }

        // Retry connection loop
        for (int attempt = 1; attempt <= Constants.ObsConnectRetries; attempt++)
        {
            Debug.WriteLine($"OBS connection attempt {attempt}/{Constants.ObsConnectRetries}...");

            if (_obs.Connect(_config.ObsPort, _config.ObsPassword))
            {
                Debug.WriteLine("Connected to OBS.");

                // Sync record directory
                if (_config.SyncObsFolder)
                {
                    // obs-websocket-dotnet doesn't have SetRecordDirectory;
                    // the Python app uses a custom request. Skip for now.
                }

                // Auto-start buffer
                if (_config.AutoStartBuffer)
                    await AutoStartBuffer();

                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(Constants.ObsConnectDelayS));
        }

        Debug.WriteLine("Could not connect to OBS after all retries.");
    }

    private async Task AutoStartBuffer()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5 + attempt * 1.0));

            if (_obs.GetBufferStatus())
            {
                Debug.WriteLine("Replay buffer already active.");
                return;
            }

            _obs.StartBuffer();
            await Task.Delay(500);

            if (_obs.GetBufferStatus())
            {
                Debug.WriteLine("Replay buffer started.");
                return;
            }
        }

        Debug.WriteLine("Failed to auto-start replay buffer.");
    }

    private static bool IsObsRunning()
    {
        try
        {
            var processes = Process.GetProcesses();
            return processes.Any(p =>
            {
                try
                {
                    var name = p.ProcessName.ToLowerInvariant();
                    return name is "obs64" or "obs32";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"IsObsRunning: Process query failed: {ex.Message}");
                    return false;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IsObsRunning failed: {ex.Message}");
            return false;
        }
    }

    // --- Polling ---

    // Polling guard (0 = idle, 1 = polling; atomically swapped via Interlocked)
    private int _polling;

    private void OnStatusTick(object? sender, EventArgs e)
    {
        // Don't stack polling calls (atomic check: skip if already polling or connecting)
        if (Interlocked.CompareExchange(ref _polling, 0, 0) != 0) return;
        if (Interlocked.CompareExchange(ref _connecting, 0, 0) != 0) return;

        if (!_obs.IsConnected)
        {
            // Attempt reconnection periodically (not too aggressively)
            if ((DateTime.UtcNow - _lastReconnectAttempt).TotalSeconds >= ReconnectIntervalS)
            {
                _lastReconnectAttempt = DateTime.UtcNow;
                // Atomically claim the connecting slot; bail if another thread got it first
                if (Interlocked.CompareExchange(ref _connecting, 1, 0) != 0) return;
                Task.Run(() =>
                {
                    try
                    {
                        Debug.WriteLine("Attempting OBS reconnect...");
                        _obs.Connect(_config.ObsPort, _config.ObsPassword);
                        if (_obs.IsConnected)
                        {
                            Debug.WriteLine("OBS reconnected successfully.");
                            // Clear caches so they refresh on next poll
                            _scenesCache = null;
                            _audioNamesCache = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OBS reconnect failed: {ex.Message}");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _connecting, 0);
                    }
                });
            }
            return;
        }

        // Atomically claim the polling slot; bail if another tick got it first
        if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0) return;

        Task.Run(() =>
        {
            try
            {
                // Refresh scene cache
                bool refreshScenes = _scenesCache == null ||
                    (DateTime.UtcNow - _scenesCacheTime).TotalSeconds > Constants.SceneCacheTtlS;
                bool refreshAudio = _audioNamesCache == null ||
                    (DateTime.UtcNow - _audioNamesCacheTime).TotalSeconds > Constants.AudioListCacheTtlS;

                // FetchState does all API calls in a single lock scope.
                // Individual API failures are handled gracefully inside FetchState
                // without declaring the whole connection dead.
                var state = _obs.FetchState(
                    refreshScenes ? null : _scenesCache,
                    refreshAudio ? null : _audioNamesCache,
                    null);

                if (state.Connected)
                {
                    // Update caches from successful fetch
                    if (refreshScenes && state.Scenes.Count > 0)
                    {
                        _scenesCache = state.Scenes;
                        _scenesCacheTime = DateTime.UtcNow;
                    }
                    if (refreshAudio && state.Audio.Count > 0)
                    {
                        _audioNamesCache = state.Audio.Select(a => a.Name).ToList();
                        _audioNamesCacheTime = DateTime.UtcNow;
                    }
                }

                _ipc.SendStateUpdate(state);

                // Monitor buffer status changes
                if (state.IsBufferActive != _lastBufferStatus)
                {
                    _lastBufferStatus = state.IsBufferActive;
                    _ipc.SendRecIndicator(state.IsBufferActive);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Status poll error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _polling, 0);
            }
        });
    }

    private void OnPreviewTick(object? sender, EventArgs e)
    {
        if (!_obs.IsConnected || !_overlayVisible)
            return;

        Task.Run(() =>
        {
            try
            {
                var base64 = _obs.GetScreenshotBase64(Constants.PreviewWidth, Constants.PreviewHeight);
                if (base64 != null)
                    _ipc.SendPreviewFrame(base64);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Preview tick error: {ex.Message}");
            }
        });
    }

    // --- Hotkey Handlers ---

    private void RegisterHotkeysAndNotify(string toggleKey, string saveKey)
    {
        var (toggleOk, saveOk) = _hotkeys.Register(toggleKey, saveKey);
        var status = $"toggle={toggleKey}({(toggleOk ? "OK" : "FAIL")}), save={saveKey}({(saveOk ? "OK" : "FAIL")})";
        LogToFile($"Hotkey registration result: {status}");

        if (!toggleOk || !saveOk)
        {
            var failed = new List<string>();
            if (!toggleOk) failed.Add($"Toggle ({HotkeyService.FormatHotkeyDisplay(toggleKey)})");
            if (!saveOk) failed.Add($"Save ({HotkeyService.FormatHotkeyDisplay(saveKey)})");
            _tray.ShowBalloon("Hotkey Registration Failed",
                $"Could not register: {string.Join(", ", failed)}. " +
                "Another app may be using the same key.",
                System.Windows.Forms.ToolTipIcon.Warning);
        }
        else
        {
            _tray.ShowBalloon("Hotkeys Active",
                $"Toggle: {HotkeyService.FormatHotkeyDisplay(toggleKey)}, " +
                $"Save: {HotkeyService.FormatHotkeyDisplay(saveKey)}",
                System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    private void OnToggleHotkey()
    {
        Dispatcher.BeginInvoke(ToggleOverlay);
    }

    private void OnSaveHotkey()
    {
        // Capture game before save
        _gameDetection.CaptureCurrentGame();
        var gameName = _gameDetection.PrepareGameFolder(_config);
        if (gameName != null)
            _replayFiles.PendingGame = gameName;

        Task.Run(HandleSaveReplay);
    }

    // --- Overlay Messages ---

    private void OnOverlayMessage(IpcMessage msg)
    {
        Dispatcher.BeginInvoke(() => HandleOverlayMessage(msg));
    }

    private void HandleOverlayMessage(IpcMessage msg)
    {
        switch (msg.Type)
        {
            case "ready":
                Debug.WriteLine("Overlay ready.");
                _ipc.SendConfigUpdate(_config);
                break;

            case "switch_scene":
                if (TryParsePayload(msg, out var sceneRoot)
                    && TryGetString(sceneRoot, "name", out var sceneName))
                {
                    Task.Run(() => _obs.SetScene(sceneName));
                }
                break;

            case "toggle_source":
                if (TryParsePayload(msg, out var srcRoot)
                    && TryGetString(srcRoot, "scene", out var srcScene)
                    && TryGetInt(srcRoot, "itemId", out var srcId)
                    && TryGetBool(srcRoot, "visible", out var srcVisible))
                {
                    if (srcId >= 0)
                        Task.Run(() => _obs.SetSourceVisible(srcScene, srcId, srcVisible));
                }
                break;

            case "set_volume":
                if (TryParsePayload(msg, out var volRoot)
                    && TryGetString(volRoot, "name", out var volName)
                    && TryGetDouble(volRoot, "volumeMul", out var volMul))
                {
                    volMul = Math.Max(volMul, 0.0);
                    if (volName.Length > 0)
                        Task.Run(() => _obs.SetInputVolume(volName, volMul));
                }
                break;

            case "toggle_mute":
                if (TryParsePayload(msg, out var muteRoot)
                    && TryGetString(muteRoot, "name", out var muteName)
                    && muteName.Length > 0)
                {
                    Task.Run(() => _obs.ToggleMute(muteName));
                }
                break;

            case "toggle_stream":
                Task.Run(() => _obs.ToggleStream());
                break;

            case "toggle_record":
                Task.Run(() => _obs.ToggleRecord());
                break;

            case "toggle_buffer":
                Task.Run(() => _obs.ToggleBuffer());
                break;

            case "toggle_record_pause":
                Task.Run(() => _obs.ToggleRecordPause());
                break;

            case "toggle_virtual_cam":
                Task.Run(() => _obs.ToggleVirtualCam());
                break;

            case "save_replay":
                _gameDetection.CaptureCurrentGame();
                var game = _gameDetection.PrepareGameFolder(_config);
                if (game != null) _replayFiles.PendingGame = game;
                Task.Run(HandleSaveReplay);
                break;

            case "set_profile":
                if (TryParsePayload(msg, out var profileRoot)
                    && TryGetString(profileRoot, "name", out var profileName)
                    && profileName.Length > 0)
                {
                    Task.Run(() => _obs.SetCurrentProfile(profileName));
                }
                break;

            case "set_scene_collection":
                if (TryParsePayload(msg, out var collRoot)
                    && TryGetString(collRoot, "name", out var collName)
                    && collName.Length > 0)
                {
                    Task.Run(() => _obs.SetCurrentSceneCollection(collName));
                }
                break;

            case "get_audio_advanced":
                Task.Run(() =>
                {
                    var audioNames = _audioNamesCache ?? [];
                    var info = _obs.GetAudioAdvancedInfo(audioNames);
                    _ipc.SendAudioAdvanced(info);
                });
                break;

            case "set_audio_sync_offset":
                if (TryParsePayload(msg, out var syncRoot)
                    && TryGetString(syncRoot, "name", out var syncName)
                    && TryGetInt(syncRoot, "offsetMs", out var syncMs)
                    && syncName.Length > 0)
                {
                    Task.Run(() => _obs.SetInputAudioSyncOffset(syncName, syncMs));
                }
                break;

            case "set_audio_balance":
                if (TryParsePayload(msg, out var balRoot)
                    && TryGetString(balRoot, "name", out var balName)
                    && TryGetDouble(balRoot, "balance", out var balVal)
                    && balName.Length > 0)
                {
                    balVal = Math.Clamp(balVal, 0.0, 1.0);
                    Task.Run(() => _obs.SetInputAudioBalance(balName, balVal));
                }
                break;

            case "set_audio_monitor_type":
                if (TryParsePayload(msg, out var monRoot)
                    && TryGetString(monRoot, "name", out var monName)
                    && TryGetInt(monRoot, "monitorType", out var monType)
                    && monName.Length > 0)
                {
                    monType = Math.Clamp(monType, 0, 2);
                    Task.Run(() => _obs.SetInputAudioMonitorType(monName, monType));
                }
                break;

            case "set_audio_tracks":
                if (TryParsePayload(msg, out var trkRoot)
                    && TryGetString(trkRoot, "name", out var trkName)
                    && trkName.Length > 0
                    && trkRoot.TryGetProperty("tracks", out var trkArr)
                    && trkArr.ValueKind == JsonValueKind.Array)
                {
                    var tracks = new bool[6];
                    for (int i = 0; i < 6 && i < trkArr.GetArrayLength(); i++)
                    {
                        if (trkArr[i].ValueKind == JsonValueKind.True || trkArr[i].ValueKind == JsonValueKind.False)
                            tracks[i] = trkArr[i].GetBoolean();
                    }
                    Task.Run(() => _obs.SetInputAudioTracks(trkName, tracks));
                }
                break;

            // --- Source Management (Phase 3) ---
            case "get_input_kinds":
                Task.Run(() =>
                {
                    var kinds = _obs.GetInputKindList();
                    _ipc.SendInputKinds(kinds);
                });
                break;

            case "create_source":
                if (TryParsePayload(msg, out var csRoot)
                    && TryGetString(csRoot, "scene", out var csScene)
                    && TryGetString(csRoot, "name", out var csName)
                    && TryGetString(csRoot, "kind", out var csKind)
                    && csScene.Length > 0 && csName.Length > 0 && csKind.Length > 0)
                {
                    Task.Run(() => { _obs.CreateInput(csScene, csName, csKind); _scenesCache = null; });
                }
                break;

            case "remove_source":
                if (TryParsePayload(msg, out var rsRoot)
                    && TryGetString(rsRoot, "scene", out var rsScene)
                    && TryGetInt(rsRoot, "itemId", out var rsId)
                    && rsScene.Length > 0 && rsId >= 0)
                {
                    Task.Run(() => _obs.RemoveSceneItem(rsScene, rsId));
                }
                break;

            case "duplicate_source":
                if (TryParsePayload(msg, out var dsRoot)
                    && TryGetString(dsRoot, "scene", out var dsScene)
                    && TryGetInt(dsRoot, "itemId", out var dsId)
                    && dsScene.Length > 0 && dsId >= 0)
                {
                    Task.Run(() => _obs.DuplicateSceneItem(dsScene, dsId));
                }
                break;

            case "reorder_source":
                if (TryParsePayload(msg, out var roRoot)
                    && TryGetString(roRoot, "scene", out var roScene)
                    && TryGetInt(roRoot, "itemId", out var roId)
                    && TryGetInt(roRoot, "index", out var roIdx)
                    && roScene.Length > 0 && roId >= 0 && roIdx >= 0)
                {
                    Task.Run(() => _obs.SetSceneItemIndex(roScene, roId, roIdx));
                }
                break;

            case "set_source_locked":
                if (TryParsePayload(msg, out var slRoot)
                    && TryGetString(slRoot, "scene", out var slScene)
                    && TryGetInt(slRoot, "itemId", out var slId)
                    && TryGetBool(slRoot, "locked", out var slLocked)
                    && slScene.Length > 0 && slId >= 0)
                {
                    Task.Run(() => _obs.SetSceneItemLocked(slScene, slId, slLocked));
                }
                break;

            case "rename_source":
                if (TryParsePayload(msg, out var rnRoot)
                    && TryGetString(rnRoot, "name", out var rnName)
                    && TryGetString(rnRoot, "newName", out var rnNew)
                    && rnName.Length > 0 && rnNew.Length > 0)
                {
                    Task.Run(() => _obs.SetInputName(rnName, rnNew));
                }
                break;

            // --- Scene Management (Phase 7) ---
            case "create_scene":
                if (TryParsePayload(msg, out var cscRoot)
                    && TryGetString(cscRoot, "name", out var cscName)
                    && cscName.Length > 0)
                {
                    Task.Run(() => { _obs.CreateScene(cscName); _scenesCache = null; });
                }
                break;

            case "remove_scene":
                if (TryParsePayload(msg, out var rmscRoot)
                    && TryGetString(rmscRoot, "name", out var rmscName)
                    && rmscName.Length > 0)
                {
                    Task.Run(() => { _obs.RemoveScene(rmscName); _scenesCache = null; });
                }
                break;

            case "rename_scene":
                if (TryParsePayload(msg, out var rnscRoot)
                    && TryGetString(rnscRoot, "name", out var rnscOld)
                    && TryGetString(rnscRoot, "newName", out var rnscNew)
                    && rnscOld.Length > 0 && rnscNew.Length > 0)
                {
                    Task.Run(() => { _obs.RenameScene(rnscOld, rnscNew); _scenesCache = null; });
                }
                break;

            // --- Filters (Phase 4) ---
            case "get_filters":
                if (TryParsePayload(msg, out var gfRoot)
                    && TryGetString(gfRoot, "source", out var gfSource)
                    && gfSource.Length > 0)
                {
                    Task.Run(() => _ipc.SendFilters(_obs.GetSourceFilters(gfSource)));
                }
                break;

            case "get_filter_kinds":
                Task.Run(() => _ipc.SendFilterKinds(_obs.GetFilterKindList()));
                break;

            case "set_filter_enabled":
                if (TryParsePayload(msg, out var feRoot)
                    && TryGetString(feRoot, "source", out var feSrc)
                    && TryGetString(feRoot, "filter", out var feFilter)
                    && TryGetBool(feRoot, "enabled", out var feEnabled)
                    && feSrc.Length > 0 && feFilter.Length > 0)
                {
                    Task.Run(() => _obs.SetFilterEnabled(feSrc, feFilter, feEnabled));
                }
                break;

            case "set_filter_index":
                if (TryParsePayload(msg, out var fiRoot)
                    && TryGetString(fiRoot, "source", out var fiSrc)
                    && TryGetString(fiRoot, "filter", out var fiFilter)
                    && TryGetInt(fiRoot, "index", out var fiIdx)
                    && fiSrc.Length > 0 && fiFilter.Length > 0 && fiIdx >= 0)
                {
                    Task.Run(() => _obs.SetFilterIndex(fiSrc, fiFilter, fiIdx));
                }
                break;

            case "create_filter":
                if (TryParsePayload(msg, out var cfRoot)
                    && TryGetString(cfRoot, "source", out var cfSrc)
                    && TryGetString(cfRoot, "name", out var cfName2)
                    && TryGetString(cfRoot, "kind", out var cfKind)
                    && cfSrc.Length > 0 && cfName2.Length > 0 && cfKind.Length > 0)
                {
                    Task.Run(() => _obs.CreateFilter(cfSrc, cfName2, cfKind));
                }
                break;

            case "remove_filter":
                if (TryParsePayload(msg, out var rfRoot)
                    && TryGetString(rfRoot, "source", out var rfSrc)
                    && TryGetString(rfRoot, "filter", out var rfFilter)
                    && rfSrc.Length > 0 && rfFilter.Length > 0)
                {
                    Task.Run(() => _obs.RemoveFilter(rfSrc, rfFilter));
                }
                break;

            case "rename_filter":
                if (TryParsePayload(msg, out var rnfRoot)
                    && TryGetString(rnfRoot, "source", out var rnfSrc)
                    && TryGetString(rnfRoot, "filter", out var rnfFilter)
                    && TryGetString(rnfRoot, "newName", out var rnfNew)
                    && rnfSrc.Length > 0 && rnfFilter.Length > 0 && rnfNew.Length > 0)
                {
                    Task.Run(() => _obs.RenameFilter(rnfSrc, rnfFilter, rnfNew));
                }
                break;

            // --- Transitions (Phase 5) ---
            case "set_transition":
                if (TryParsePayload(msg, out var stRoot)
                    && TryGetString(stRoot, "name", out var stName)
                    && stName.Length > 0)
                {
                    Task.Run(() => _obs.SetCurrentTransition(stName));
                }
                break;

            case "set_transition_duration":
                if (TryParsePayload(msg, out var sdRoot)
                    && TryGetInt(sdRoot, "duration", out var sdDur))
                {
                    sdDur = Math.Max(sdDur, 0);
                    Task.Run(() => _obs.SetTransitionDuration(sdDur));
                }
                break;

            case "toggle_studio_mode":
                if (TryParsePayload(msg, out var smRoot)
                    && TryGetBool(smRoot, "enabled", out var smEnabled))
                {
                    Task.Run(() => _obs.SetStudioModeEnabled(smEnabled));
                }
                break;

            case "set_preview_scene":
                if (TryParsePayload(msg, out var psRoot)
                    && TryGetString(psRoot, "name", out var psName)
                    && psName.Length > 0)
                {
                    Task.Run(() => _obs.SetCurrentPreviewScene(psName));
                }
                break;

            case "trigger_transition":
                Task.Run(() => _obs.TriggerStudioModeTransition());
                break;

            // --- Stats (Phase 6) ---
            case "get_stats":
                Task.Run(() =>
                {
                    var stats = _obs.GetStats();
                    if (stats != null) _ipc.SendStats(stats);
                });
                break;

            case "get_hotkeys":
                Task.Run(() => _ipc.SendHotkeys(_obs.GetHotkeyList()));
                break;

            case "trigger_hotkey":
                if (TryParsePayload(msg, out var hkRoot)
                    && TryGetString(hkRoot, "name", out var hkName)
                    && hkName.Length > 0)
                {
                    Task.Run(() => _obs.TriggerHotkey(hkName));
                }
                break;

            case "save_settings":
                HandleSaveSettings(msg.Payload);
                break;

            case "open_settings":
                OnSettingsClicked();
                break;

            case "close_overlay":
                _overlayVisible = false;
                _previewTimer?.Stop();
                _ipc.SendHideOverlay();
                break;

            case "overlay_moved":
                if (TryParsePayload(msg, out var posRoot)
                    && TryGetInt(posRoot, "x", out var posX)
                    && TryGetInt(posRoot, "y", out var posY))
                {
                    _config.OverlayX = posX;
                    _config.OverlayY = posY;
                    _configService.Save(_config);
                }
                break;

            default:
                Debug.WriteLine($"Unknown overlay message: {msg.Type}");
                break;
        }
    }

    // --- Payload Parsing Helpers ---

    /// <summary>
    /// Safely parses the JSON payload of an IPC message, returning the root element.
    /// Logs and returns false on malformed JSON.
    /// </summary>
    private static bool TryParsePayload(IpcMessage msg, out JsonElement root)
    {
        root = default;
        try
        {
            var doc = JsonDocument.Parse(msg.Payload ?? "{}");
            root = doc.RootElement;
            return true;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"IPC: Malformed payload for '{msg.Type}': {ex.Message}");
            return false;
        }
    }

    private static bool TryGetString(JsonElement root, string property, out string value)
    {
        value = "";
        if (root.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString() ?? "";
            return true;
        }
        Debug.WriteLine($"IPC: Missing or invalid string property '{property}'.");
        return false;
    }

    private static bool TryGetInt(JsonElement root, string property, out int value)
    {
        value = 0;
        if (root.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            && prop.TryGetInt32(out value))
        {
            return true;
        }
        Debug.WriteLine($"IPC: Missing or invalid int property '{property}'.");
        return false;
    }

    private static bool TryGetDouble(JsonElement root, string property, out double value)
    {
        value = 0.0;
        if (root.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            && prop.TryGetDouble(out value) && !double.IsNaN(value) && !double.IsInfinity(value))
        {
            return true;
        }
        Debug.WriteLine($"IPC: Missing or invalid double property '{property}'.");
        return false;
    }

    private static bool TryGetBool(JsonElement root, string property, out bool value)
    {
        value = false;
        if (root.TryGetProperty(property, out var prop)
            && (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False))
        {
            value = prop.GetBoolean();
            return true;
        }
        Debug.WriteLine($"IPC: Missing or invalid bool property '{property}'.");
        return false;
    }

    // --- Save Replay ---

    private void HandleSaveReplay()
    {
        // Atomically claim the save slot; bail if another call got it first
        if (Interlocked.CompareExchange(ref _saveInProgress, 1, 0) != 0) return;

        try
        {
            var hasCapture = _obs.HasActiveCapture();
            if (hasCapture == false)
            {
                _ipc.SendNotification("NO CAPTURE DETECTED", Constants.Colors.Warning, 3.0);
                return;
            }

            if (_obs.SaveBuffer())
            {
                if (_config.ShowNotifications)
                {
                    _ipc.SendNotification(
                        _config.NotificationMessage,
                        Constants.Colors.Accent,
                        _config.NotificationDuration);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Save replay error: {ex.Message}");
        }
        finally
        {
            // Reset after delay to prevent rapid re-triggers
            Task.Delay(Constants.ButtonDebounceMs).ContinueWith(_ => Interlocked.Exchange(ref _saveInProgress, 0));
        }
    }

    // --- Overlay Toggle ---

    public void ToggleOverlay()
    {
        _overlayVisible = !_overlayVisible;

        if (_overlayVisible)
        {
            // Capture foreground game before showing overlay
            _gameDetection.CaptureCurrentGame();

            _ipc.SendShowOverlay();
            _previewTimer?.Start();
        }
        else
        {
            _ipc.SendHideOverlay();
            _previewTimer?.Stop();
        }
    }

    // --- Settings ---

    private void HandleSaveSettings(string payloadJson)
    {
        try
        {
            var payload = JsonDocument.Parse(payloadJson).RootElement;

            if (payload.TryGetProperty("showNotifications", out var sn)
                && (sn.ValueKind == JsonValueKind.True || sn.ValueKind == JsonValueKind.False))
                _config.ShowNotifications = sn.GetBoolean();
            if (payload.TryGetProperty("notificationMessage", out var nm)
                && nm.ValueKind == JsonValueKind.String)
                _config.NotificationMessage = nm.GetString() ?? "REPLAY SAVED";
            if (payload.TryGetProperty("notificationDuration", out var nd)
                && nd.ValueKind == JsonValueKind.Number && nd.TryGetDouble(out var ndVal)
                && !double.IsNaN(ndVal) && !double.IsInfinity(ndVal))
                _config.NotificationDuration = Math.Clamp(ndVal, 0.5, 30.0);
            if (payload.TryGetProperty("showRecIndicator", out var sri)
                && (sri.ValueKind == JsonValueKind.True || sri.ValueKind == JsonValueKind.False))
                _config.ShowRecIndicator = sri.GetBoolean();
            if (payload.TryGetProperty("recIndicatorPosition", out var rip)
                && rip.ValueKind == JsonValueKind.String)
                _config.RecIndicatorPosition = rip.GetString() ?? "top-left";

            _configService.Save(_config);
            _ipc.SendConfigUpdate(_config);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HandleSaveSettings error: {ex.Message}");
        }
    }

    private void OnSettingsClicked()
    {
        // Tell overlay to drop topmost so settings dialog is usable
        _ipc.SendMessage(IpcMessage.Create("settings_opened"));

        var settingsWindow = new SettingsWindow(_config);
        if (settingsWindow.ShowDialog() == true && settingsWindow.Result != null)
        {
            ApplySettings(settingsWindow.Result);
        }

        // Restore overlay topmost
        _ipc.SendMessage(IpcMessage.Create("settings_closed"));
    }

    // --- Actions ---

    private void OpenLibrary()
    {
        try
        {
            if (Directory.Exists(_config.WatchFolder))
                Process.Start("explorer.exe", _config.WatchFolder);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Open library failed: {ex.Message}");
        }
    }

    private void OnRestart()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true
                });
            }
            Shutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Restart failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply new settings at runtime (called after SettingsDialog saves).
    /// </summary>
    public void ApplySettings(AppConfig newConfig)
    {
        var oldConfig = _config;
        _config = newConfig;
        _configService.Save(_config);

        // Re-register hotkeys if changed
        if (oldConfig.ToggleHotkey != newConfig.ToggleHotkey ||
            oldConfig.SaveHotkey != newConfig.SaveHotkey ||
            oldConfig.HotkeyEnabled != newConfig.HotkeyEnabled)
        {
            if (newConfig.HotkeyEnabled)
                RegisterHotkeysAndNotify(newConfig.ToggleHotkey, newConfig.SaveHotkey);
            else
                _hotkeys.Unregister();
        }

        // Restart file watcher if folder changed
        if (oldConfig.WatchFolder != newConfig.WatchFolder)
            _replayFiles.Restart(newConfig.WatchFolder);

        // Update overlay config
        _ipc.SendConfigUpdate(newConfig);

        // Update Windows startup if changed
        if (oldConfig.StartWithWindows != newConfig.StartWithWindows)
            AdminService.SetWindowsStartup(newConfig.StartWithWindows);
    }

    // --- Diagnostics ---

    private static readonly string DiagLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReplayOverlay", "hotkey.log");

    private static void LogToFile(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(DiagLogPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(DiagLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* best-effort */ }
    }

    // --- Shutdown ---

    protected override void OnExit(ExitEventArgs e)
    {
        Debug.WriteLine("App shutting down...");

        _statusTimer?.Stop();
        _previewTimer?.Stop();

        _hotkeys.Dispose();
        _tray.Dispose();
        _replayFiles.Dispose();

        _ipc.SendShutdown();
        Thread.Sleep(200);

        _overlayProcess.Stop();
        _ipc.Dispose();
        _obs.Dispose();

        base.OnExit(e);
    }
}
