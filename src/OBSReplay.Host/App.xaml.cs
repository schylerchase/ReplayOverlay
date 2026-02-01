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

    // Save guard
    private volatile bool _saveInProgress;

    // Reconnection tracking
    private volatile bool _connecting;
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
        if (_config.HotkeyEnabled)
            _hotkeys.Register(_config.ToggleHotkey, _config.SaveHotkey);

        // --- Tray icon ---
        _tray.Initialize();
        _tray.ShowOverlayClicked += () => ToggleOverlay();
        _tray.SaveReplayClicked += () => Task.Run(HandleSaveReplay);
        _tray.StartBufferClicked += () => Task.Run(() => _obs.StartBuffer());
        _tray.StopBufferClicked += () => Task.Run(() => _obs.StopBuffer());
        _tray.SettingsClicked += OnSettingsClicked;
        _tray.OpenLibraryClicked += _ => OpenLibrary();
        _tray.RestartClicked += OnRestart;
        _tray.ExitClicked += () => Shutdown();

        // --- File watcher ---
        _replayFiles.Start(_config.WatchFolder);

        // --- OBS connection (background) ---
        Task.Run(ConnectToObs);

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
                catch { return false; }
            });
        }
        catch { return false; }
    }

    // --- Polling ---

    private volatile bool _polling;

    private void OnStatusTick(object? sender, EventArgs e)
    {
        // Don't stack polling calls
        if (_polling || _connecting) return;

        if (!_obs.IsConnected)
        {
            // Attempt reconnection periodically (not too aggressively)
            if ((DateTime.UtcNow - _lastReconnectAttempt).TotalSeconds >= ReconnectIntervalS)
            {
                _lastReconnectAttempt = DateTime.UtcNow;
                _connecting = true;
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
                        _connecting = false;
                    }
                });
            }
            return;
        }

        _polling = true;

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
                _polling = false;
            }
        });
    }

    private void OnPreviewTick(object? sender, EventArgs e)
    {
        if (!_obs.IsConnected || !_overlayVisible)
            return;

        Task.Run(() =>
        {
            var base64 = _obs.GetScreenshotBase64(Constants.PreviewWidth, Constants.PreviewHeight);
            if (base64 != null)
                _ipc.SendPreviewFrame(base64);
        });
    }

    // --- Hotkey Handlers ---

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
                var sceneData = JsonDocument.Parse(msg.Payload);
                var sceneName = sceneData.RootElement.GetProperty("name").GetString();
                if (sceneName != null)
                    Task.Run(() => _obs.SetScene(sceneName));
                break;

            case "toggle_source":
                var srcData = JsonDocument.Parse(msg.Payload);
                var srcScene = srcData.RootElement.GetProperty("scene").GetString() ?? "";
                var srcId = srcData.RootElement.GetProperty("itemId").GetInt32();
                var srcVisible = srcData.RootElement.GetProperty("visible").GetBoolean();
                Task.Run(() => _obs.SetSourceVisible(srcScene, srcId, srcVisible));
                break;

            case "set_volume":
                var volData = JsonDocument.Parse(msg.Payload);
                var volName = volData.RootElement.GetProperty("name").GetString() ?? "";
                var volMul = volData.RootElement.GetProperty("volumeMul").GetDouble();
                Task.Run(() => _obs.SetInputVolume(volName, volMul));
                break;

            case "toggle_mute":
                var muteData = JsonDocument.Parse(msg.Payload);
                var muteName = muteData.RootElement.GetProperty("name").GetString() ?? "";
                Task.Run(() => _obs.ToggleMute(muteName));
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

            case "save_replay":
                _gameDetection.CaptureCurrentGame();
                var game = _gameDetection.PrepareGameFolder(_config);
                if (game != null) _replayFiles.PendingGame = game;
                Task.Run(HandleSaveReplay);
                break;

            case "set_profile":
                var profileData = JsonDocument.Parse(msg.Payload);
                var profileName = profileData.RootElement.GetProperty("name").GetString();
                if (profileName != null)
                    Task.Run(() => _obs.SetCurrentProfile(profileName));
                break;

            case "set_scene_collection":
                var collData = JsonDocument.Parse(msg.Payload);
                var collName = collData.RootElement.GetProperty("name").GetString();
                if (collName != null)
                    Task.Run(() => _obs.SetCurrentSceneCollection(collName));
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
                var syncData = JsonDocument.Parse(msg.Payload);
                var syncName = syncData.RootElement.GetProperty("name").GetString() ?? "";
                var syncMs = syncData.RootElement.GetProperty("offsetMs").GetInt32();
                Task.Run(() => _obs.SetInputAudioSyncOffset(syncName, syncMs));
                break;

            case "set_audio_balance":
                var balData = JsonDocument.Parse(msg.Payload);
                var balName = balData.RootElement.GetProperty("name").GetString() ?? "";
                var balVal = balData.RootElement.GetProperty("balance").GetDouble();
                Task.Run(() => _obs.SetInputAudioBalance(balName, balVal));
                break;

            case "set_audio_monitor_type":
                var monData = JsonDocument.Parse(msg.Payload);
                var monName = monData.RootElement.GetProperty("name").GetString() ?? "";
                var monType = monData.RootElement.GetProperty("monitorType").GetInt32();
                Task.Run(() => _obs.SetInputAudioMonitorType(monName, monType));
                break;

            case "set_audio_tracks":
                var trkData = JsonDocument.Parse(msg.Payload);
                var trkName = trkData.RootElement.GetProperty("name").GetString() ?? "";
                var trkArr = trkData.RootElement.GetProperty("tracks");
                var tracks = new bool[6];
                for (int i = 0; i < 6 && i < trkArr.GetArrayLength(); i++)
                    tracks[i] = trkArr[i].GetBoolean();
                Task.Run(() => _obs.SetInputAudioTracks(trkName, tracks));
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
                var csData = JsonDocument.Parse(msg.Payload);
                var csScene = csData.RootElement.GetProperty("scene").GetString() ?? "";
                var csName = csData.RootElement.GetProperty("name").GetString() ?? "";
                var csKind = csData.RootElement.GetProperty("kind").GetString() ?? "";
                Task.Run(() => { _obs.CreateInput(csScene, csName, csKind); _scenesCache = null; });
                break;

            case "remove_source":
                var rsData = JsonDocument.Parse(msg.Payload);
                var rsScene = rsData.RootElement.GetProperty("scene").GetString() ?? "";
                var rsId = rsData.RootElement.GetProperty("itemId").GetInt32();
                Task.Run(() => _obs.RemoveSceneItem(rsScene, rsId));
                break;

            case "duplicate_source":
                var dsData = JsonDocument.Parse(msg.Payload);
                var dsScene = dsData.RootElement.GetProperty("scene").GetString() ?? "";
                var dsId = dsData.RootElement.GetProperty("itemId").GetInt32();
                Task.Run(() => _obs.DuplicateSceneItem(dsScene, dsId));
                break;

            case "reorder_source":
                var roData = JsonDocument.Parse(msg.Payload);
                var roScene = roData.RootElement.GetProperty("scene").GetString() ?? "";
                var roId = roData.RootElement.GetProperty("itemId").GetInt32();
                var roIdx = roData.RootElement.GetProperty("index").GetInt32();
                Task.Run(() => _obs.SetSceneItemIndex(roScene, roId, roIdx));
                break;

            case "set_source_locked":
                var slData = JsonDocument.Parse(msg.Payload);
                var slScene = slData.RootElement.GetProperty("scene").GetString() ?? "";
                var slId = slData.RootElement.GetProperty("itemId").GetInt32();
                var slLocked = slData.RootElement.GetProperty("locked").GetBoolean();
                Task.Run(() => _obs.SetSceneItemLocked(slScene, slId, slLocked));
                break;

            case "rename_source":
                var rnData = JsonDocument.Parse(msg.Payload);
                var rnName = rnData.RootElement.GetProperty("name").GetString() ?? "";
                var rnNew = rnData.RootElement.GetProperty("newName").GetString() ?? "";
                Task.Run(() => _obs.SetInputName(rnName, rnNew));
                break;

            // --- Scene Management (Phase 7) ---
            case "create_scene":
                var cscData = JsonDocument.Parse(msg.Payload);
                var cscName = cscData.RootElement.GetProperty("name").GetString() ?? "";
                Task.Run(() => { _obs.CreateScene(cscName); _scenesCache = null; });
                break;

            case "remove_scene":
                var rmscData = JsonDocument.Parse(msg.Payload);
                var rmscName = rmscData.RootElement.GetProperty("name").GetString() ?? "";
                Task.Run(() => { _obs.RemoveScene(rmscName); _scenesCache = null; });
                break;

            case "rename_scene":
                var rnscData = JsonDocument.Parse(msg.Payload);
                var rnscOld = rnscData.RootElement.GetProperty("name").GetString() ?? "";
                var rnscNew = rnscData.RootElement.GetProperty("newName").GetString() ?? "";
                Task.Run(() => { _obs.RenameScene(rnscOld, rnscNew); _scenesCache = null; });
                break;

            // --- Filters (Phase 4) ---
            case "get_filters":
                var gfData = JsonDocument.Parse(msg.Payload);
                var gfSource = gfData.RootElement.GetProperty("source").GetString() ?? "";
                Task.Run(() => _ipc.SendFilters(_obs.GetSourceFilters(gfSource)));
                break;

            case "get_filter_kinds":
                Task.Run(() => _ipc.SendFilterKinds(_obs.GetFilterKindList()));
                break;

            case "set_filter_enabled":
                var feData = JsonDocument.Parse(msg.Payload);
                var feSrc = feData.RootElement.GetProperty("source").GetString() ?? "";
                var feFilter = feData.RootElement.GetProperty("filter").GetString() ?? "";
                var feEnabled = feData.RootElement.GetProperty("enabled").GetBoolean();
                Task.Run(() => _obs.SetFilterEnabled(feSrc, feFilter, feEnabled));
                break;

            case "set_filter_index":
                var fiData = JsonDocument.Parse(msg.Payload);
                var fiSrc = fiData.RootElement.GetProperty("source").GetString() ?? "";
                var fiFilter = fiData.RootElement.GetProperty("filter").GetString() ?? "";
                var fiIdx = fiData.RootElement.GetProperty("index").GetInt32();
                Task.Run(() => _obs.SetFilterIndex(fiSrc, fiFilter, fiIdx));
                break;

            case "create_filter":
                var cfData = JsonDocument.Parse(msg.Payload);
                var cfSrc = cfData.RootElement.GetProperty("source").GetString() ?? "";
                var cfName2 = cfData.RootElement.GetProperty("name").GetString() ?? "";
                var cfKind = cfData.RootElement.GetProperty("kind").GetString() ?? "";
                Task.Run(() => _obs.CreateFilter(cfSrc, cfName2, cfKind));
                break;

            case "remove_filter":
                var rfData = JsonDocument.Parse(msg.Payload);
                var rfSrc = rfData.RootElement.GetProperty("source").GetString() ?? "";
                var rfFilter = rfData.RootElement.GetProperty("filter").GetString() ?? "";
                Task.Run(() => _obs.RemoveFilter(rfSrc, rfFilter));
                break;

            case "rename_filter":
                var rnfData = JsonDocument.Parse(msg.Payload);
                var rnfSrc = rnfData.RootElement.GetProperty("source").GetString() ?? "";
                var rnfFilter = rnfData.RootElement.GetProperty("filter").GetString() ?? "";
                var rnfNew = rnfData.RootElement.GetProperty("newName").GetString() ?? "";
                Task.Run(() => _obs.RenameFilter(rnfSrc, rnfFilter, rnfNew));
                break;

            // --- Transitions (Phase 5) ---
            case "set_transition":
                var stData = JsonDocument.Parse(msg.Payload);
                var stName = stData.RootElement.GetProperty("name").GetString() ?? "";
                Task.Run(() => _obs.SetCurrentTransition(stName));
                break;

            case "set_transition_duration":
                var sdData = JsonDocument.Parse(msg.Payload);
                var sdDur = sdData.RootElement.GetProperty("duration").GetInt32();
                Task.Run(() => _obs.SetTransitionDuration(sdDur));
                break;

            case "toggle_studio_mode":
                var smData = JsonDocument.Parse(msg.Payload);
                var smEnabled = smData.RootElement.GetProperty("enabled").GetBoolean();
                Task.Run(() => _obs.SetStudioModeEnabled(smEnabled));
                break;

            case "set_preview_scene":
                var psData = JsonDocument.Parse(msg.Payload);
                var psName = psData.RootElement.GetProperty("name").GetString() ?? "";
                Task.Run(() => _obs.SetCurrentPreviewScene(psName));
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
                var hkData = JsonDocument.Parse(msg.Payload);
                var hkName = hkData.RootElement.GetProperty("name").GetString() ?? "";
                Task.Run(() => _obs.TriggerHotkey(hkName));
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
                var posData = JsonDocument.Parse(msg.Payload);
                _config.OverlayX = posData.RootElement.GetProperty("x").GetInt32();
                _config.OverlayY = posData.RootElement.GetProperty("y").GetInt32();
                _configService.Save(_config);
                break;

            default:
                Debug.WriteLine($"Unknown overlay message: {msg.Type}");
                break;
        }
    }

    // --- Save Replay ---

    private void HandleSaveReplay()
    {
        if (_saveInProgress) return;
        _saveInProgress = true;

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
            Task.Delay(Constants.ButtonDebounceMs).ContinueWith(_ => _saveInProgress = false);
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

            if (payload.TryGetProperty("showNotifications", out var sn))
                _config.ShowNotifications = sn.GetBoolean();
            if (payload.TryGetProperty("notificationMessage", out var nm))
                _config.NotificationMessage = nm.GetString() ?? "REPLAY SAVED";
            if (payload.TryGetProperty("notificationDuration", out var nd))
                _config.NotificationDuration = nd.GetDouble();
            if (payload.TryGetProperty("showRecIndicator", out var sri))
                _config.ShowRecIndicator = sri.GetBoolean();
            if (payload.TryGetProperty("recIndicatorPosition", out var rip))
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
                _hotkeys.Register(newConfig.ToggleHotkey, newConfig.SaveHotkey);
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
