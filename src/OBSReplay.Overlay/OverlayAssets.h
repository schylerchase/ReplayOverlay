#pragma once
#include <string>

// Build the theme CSS and overlay RML at runtime to avoid MSVC string literal limits.
// Adjacent raw string literals would still hit the 16380-char limit per literal,
// so we use a function that concatenates smaller strings.

inline std::string GetOverlayThemeRcss()
{
    std::string css;
    css += R"rcss(
body {
    font-family: Segoe UI;
    font-size: 14dp;
    color: #eaeaea;
}
.panel {
    background-color: #1a1a2edd;
    border-radius: 8dp;
    width: 700dp;
    min-height: 400dp;
    max-height: 700dp;
    margin: 40dp auto 0 auto;
    padding: 0;
    overflow: hidden;
}
.header {
    display: flex;
    flex-direction: row;
    align-items: center;
    padding: 8dp 12dp;
    border-bottom: 1dp solid #2a2a4a;
    gap: 8dp;
}
.header .status-connected { color: #4ecca3; font-weight: bold; }
.header .status-disconnected { color: #e94560; font-weight: bold; }
.header .badge { font-size: 12dp; font-weight: bold; margin-left: 6dp; }
.header .badge-rec { color: #e94560; }
.header .badge-idle { color: #f0c040; }
.header .badge-paused { color: #f0c040; }
.header .spacer { flex: 1; }
.header select {
    background-color: #16213e;
    color: #eaeaea;
    border: 1dp solid #2a2a4a;
    border-radius: 4dp;
    padding: 2dp 6dp;
    font-size: 12dp;
    width: 120dp;
}
.header .close-btn {
    background-color: transparent;
    color: #7f8c8d;
    border: none;
    font-size: 16dp;
    padding: 2dp 8dp;
    cursor: pointer;
}
.header .close-btn:hover { color: #e94560; }
)rcss";

    css += R"rcss(
.tab-bar {
    display: flex;
    flex-direction: row;
    background-color: #16213e;
    border-bottom: 1dp solid #2a2a4a;
    padding: 0 4dp;
}
.tab {
    padding: 6dp 12dp;
    color: #7f8c8d;
    cursor: pointer;
    font-size: 13dp;
    border-bottom: 2dp solid transparent;
}
.tab:hover { color: #eaeaea; }
.tab.active {
    color: #4ecca3;
    border-bottom-color: #4ecca3;
}
.tab-content {
    padding: 12dp;
    overflow-y: auto;
    min-height: 280dp;
    max-height: 520dp;
}
.footer {
    padding: 6dp 12dp;
    border-top: 1dp solid #2a2a4a;
    text-align: center;
    color: #7f8c8d;
    font-size: 12dp;
}
.section-header {
    color: #7f8c8d;
    font-size: 12dp;
    font-weight: bold;
    margin-bottom: 6dp;
    letter-spacing: 1dp;
}
)rcss";

    css += R"rcss(
button, .btn {
    background-color: #16213e;
    color: #7f8c8d;
    border: 1dp solid #2a2a4a;
    border-radius: 4dp;
    padding: 6dp 12dp;
    cursor: pointer;
    font-size: 13dp;
    text-align: center;
}
button:hover, .btn:hover {
    background-color: #1a2848;
    color: #eaeaea;
}
.btn-accent { background-color: #0d3d30; color: #4ecca3; border-color: #4ecca3; }
.btn-accent:hover { background-color: #145a48; }
.btn-active-stream { background-color: #0d3d30; color: #4ecca3; border-color: #4ecca3; }
.btn-active-record { background-color: #3d1525; color: #e94560; border-color: #e94560; }
.btn-danger { background-color: #3d1525; color: #e94560; border-color: #e94560; }
.btn-danger:hover { background-color: #5a1e35; }
.btn-warning { background-color: #f0c040; color: #1a1a2e; }
.btn-small { padding: 3dp 8dp; font-size: 12dp; }
)rcss";

    css += R"rcss(
.list-container {
    background-color: #0f0f23;
    border: 1dp solid #2a2a4a;
    border-radius: 4dp;
    padding: 4dp;
    overflow-y: auto;
    max-height: 180dp;
}
.list-item {
    display: flex;
    flex-direction: row;
    align-items: center;
    padding: 4dp 8dp;
    border-radius: 3dp;
    cursor: pointer;
    gap: 6dp;
}
.list-item:hover { background-color: #1a2848; }
.list-item.selected { background-color: #16213e; }
.list-item.current { color: #4ecca3; }
.list-item .name { flex: 1; }
.list-item .kind { color: #7f8c8d; font-size: 12dp; }
.columns { display: flex; flex-direction: row; gap: 12dp; }
.col-left { width: 40%; }
.col-right { width: 60%; flex: 1; }
.control-grid { display: flex; flex-direction: row; flex-wrap: wrap; gap: 4dp; }
.control-grid button { width: 48%; padding: 8dp; }
.preview-area {
    background-color: #0f0f23;
    border-radius: 4dp;
    width: 100%;
    height: 140dp;
    text-align: center;
    color: #7f8c8d;
    padding-top: 60dp;
    margin-bottom: 8dp;
}
)rcss";

    css += R"rcss(
.audio-row {
    display: flex;
    flex-direction: row;
    align-items: center;
    padding: 4dp 0;
    gap: 6dp;
    border-bottom: 1dp solid #1a1a30;
}
.audio-row .mute-btn {
    width: 28dp; height: 28dp; padding: 0;
    text-align: center; font-size: 12dp; border-radius: 4dp;
}
.audio-row .mute-btn.muted { background-color: #e94560; color: white; border-color: #e94560; }
.audio-row .mute-btn.unmuted { background-color: #4ecca3; color: white; border-color: #4ecca3; }
.audio-row .expand-btn {
    width: 24dp; height: 24dp; padding: 0;
    font-size: 10dp; text-align: center;
    background-color: transparent; border: none; color: #7f8c8d;
}
.audio-row .audio-name { width: 100dp; overflow: hidden; color: #7f8c8d; font-size: 13dp; }
.audio-row input.range { flex: 1; }
.audio-advanced { padding: 6dp 0 6dp 40dp; border-bottom: 1dp solid #1a1a30; }
.audio-advanced .adv-row {
    display: flex; flex-direction: row; align-items: center; gap: 8dp; margin-bottom: 4dp;
}
.audio-advanced label { color: #7f8c8d; font-size: 12dp; width: 80dp; }
.audio-advanced input.range { width: 200dp; }
.audio-advanced select { width: 200dp; }
)rcss";

    css += R"rcss(
.action-row { display: flex; flex-direction: row; gap: 4dp; margin-top: 6dp; }
.action-row button { min-width: 60dp; }
.source-row {
    display: flex; flex-direction: row; align-items: center;
    gap: 6dp; padding: 3dp 6dp; border-radius: 3dp; cursor: pointer;
}
.source-row:hover { background-color: #1a2848; }
.source-row.selected { background-color: #16213e; }
.filter-row {
    display: flex; flex-direction: row; align-items: center;
    gap: 6dp; padding: 3dp 6dp; border-radius: 3dp; cursor: pointer;
}
.filter-row:hover { background-color: #1a2848; }
.filter-row.selected { background-color: #16213e; }
input.text {
    background-color: #0f0f23; border: 1dp solid #2a2a4a;
    border-radius: 4dp; padding: 4dp 8dp; color: #eaeaea; font-size: 13dp;
}
select {
    background-color: #0f0f23; border: 1dp solid #2a2a4a;
    border-radius: 4dp; padding: 4dp 8dp; color: #eaeaea; font-size: 13dp;
}
.separator { height: 1dp; background-color: #2a2a4a; margin: 8dp 0; }
.stat-row { display: flex; flex-direction: row; gap: 20dp; margin-bottom: 4dp; }
.stat-label { color: #7f8c8d; font-size: 13dp; width: 100dp; }
.stat-value { font-size: 13dp; }
.hotkey-item { padding: 3dp 8dp; border-radius: 3dp; cursor: pointer; font-size: 13dp; }
.hotkey-item:hover { background-color: #1a2848; color: #4ecca3; }
)rcss";

    css += R"rcss(
.notification {
    position: fixed; bottom: 80dp; left: 50%; margin-left: -150dp;
    width: 300dp; padding: 12dp 20dp; border-radius: 8dp;
    text-align: center; font-size: 16dp; font-weight: bold; z-index: 100;
}
.rec-indicator {
    position: fixed; z-index: 100;
    display: flex; flex-direction: row; align-items: center; gap: 6dp; padding: 4dp 10dp;
}
.rec-indicator.top-left { top: 10dp; left: 10dp; }
.rec-indicator.top-center { top: 10dp; left: 50%; margin-left: -30dp; }
.rec-indicator.top-right { top: 10dp; right: 10dp; }
.rec-indicator.bottom-left { bottom: 10dp; left: 10dp; }
.rec-indicator.bottom-center { bottom: 10dp; left: 50%; margin-left: -30dp; }
.rec-indicator.bottom-right { bottom: 10dp; right: 10dp; }
.rec-dot { width: 12dp; height: 12dp; border-radius: 6dp; background-color: #e94560; }
.rec-label { color: #e94560; font-size: 14dp; font-weight: bold; }
scrollbarvertical { width: 8dp; margin-left: 2dp; }
scrollbarvertical slidertrack { background-color: #0f0f23; border-radius: 4dp; }
scrollbarvertical sliderbar { background-color: #2a2a4a; border-radius: 4dp; min-height: 20dp; }
scrollbarvertical sliderbar:hover { background-color: #3a3a5a; }
.hidden { display: none; }
)rcss";

    return css;
}

inline std::string GetOverlayDocumentRml()
{
    std::string rml;
    rml += R"rml(
<rml><head><style>__THEME__</style></head>
<body data-model="overlay">
)rml";

    // Notification + REC indicator
    rml += R"rml(
<div class="notification" data-if="notif_active"
     data-style-background-color="notif_color"
     data-style-opacity="notif_alpha">
    {{notif_text}}
</div>
<div data-if="rec_active"
     data-class-top-left="rec_position == 'top-left'"
     data-class-top-center="rec_position == 'top-center'"
     data-class-top-right="rec_position == 'top-right'"
     data-class-bottom-left="rec_position == 'bottom-left'"
     data-class-bottom-center="rec_position == 'bottom-center'"
     data-class-bottom-right="rec_position == 'bottom-right'"
     class="rec-indicator">
    <div class="rec-dot" data-if="rec_dot_visible"></div>
    <div class="rec-label">REC</div>
</div>
)rml";

    // Panel start + header
    rml += R"rml(
<div class="panel" id="panel">
<div class="header">
    <div data-if="connected == 'Connected'" class="status-connected">Connected</div>
    <div data-if="connected != 'Connected'" class="status-disconnected">Disconnected</div>
    <div data-if="is_buffer_active" class="badge">
        <span data-if="has_active_capture" class="badge-rec">[REC]</span>
        <span data-if="has_active_capture == false" class="badge-idle">[IDLE]</span>
    </div>
    <span data-if="is_recording_paused" class="badge badge-paused">[PAUSED]</span>
    <div class="spacer"></div>
    <button class="close-btn" data-event-click="close_overlay">X</button>
</div>
)rml";

    // Tab bar
    rml += R"rml(
<div class="tab-bar">
    <div class="tab" data-class-active="active_tab == 'main'" data-event-click="switch_tab('main')">Main</div>
    <div class="tab" data-class-active="active_tab == 'sources'" data-event-click="switch_tab('sources')">Sources</div>
    <div class="tab" data-class-active="active_tab == 'audio'" data-event-click="switch_tab('audio')">Audio</div>
    <div class="tab" data-class-active="active_tab == 'filters'" data-event-click="switch_tab('filters')">Filters</div>
    <div class="tab" data-class-active="active_tab == 'transitions'" data-event-click="switch_tab('transitions')">Transitions</div>
    <div class="tab" data-class-active="active_tab == 'stats'" data-event-click="switch_tab('stats')">Stats</div>
    <div class="tab" data-class-active="active_tab == 'settings'" data-event-click="switch_tab('settings')">Settings</div>
</div>
<div class="tab-content">
)rml";

    // Main tab
    rml += R"rml(
<div data-if="active_tab == 'main'">
    <div class="columns">
        <div class="col-left">
            <div class="preview-area">No Preview</div>
            <div class="control-grid">
                <button data-event-click="toggle_stream"
                        data-class-btn-active-stream="is_streaming">Stream</button>
                <button data-event-click="toggle_record"
                        data-class-btn-active-record="is_recording">Record</button>
                <button data-event-click="toggle_buffer"
                        data-class-btn-active-stream="is_buffer_active">Buffer</button>
                <button data-event-click="save_replay" class="btn-accent">Save Replay</button>
            </div>
            <div data-if="is_recording" style="margin-top: 4dp;">
                <button data-event-click="toggle_pause" style="width: 100%;"
                        data-class-btn-warning="is_recording_paused">
                    <span data-if="is_recording_paused">Resume</span>
                    <span data-if="is_recording_paused == false">Pause</span>
                </button>
            </div>
        </div>
        <div class="col-right">
            <div class="section-header">SCENES</div>
            <div class="list-container" style="max-height: 140dp;">
                <div data-for="scene : scenes" class="list-item"
                     data-class-current="scene.name == current_scene"
                     data-event-click="switch_scene(scene.name)">
                    <div class="name">{{scene.name}}</div>
                </div>
            </div>
            <div style="height: 8dp;"></div>
            <div class="section-header">SOURCES</div>
            <div class="list-container" style="max-height: 140dp;">
                <div data-for="src : sources" class="list-item"
                     data-event-click="toggle_source(src.id, src.visible)">
                    <input type="checkbox" data-attrif-checked="src.visible"/>
                    <div class="name">{{src.name}}</div>
                </div>
            </div>
        </div>
    </div>
</div>
)rml";

    // Sources tab
    rml += R"rml(
<div data-if="active_tab == 'sources'">
    <div class="section-header">SOURCE MANAGEMENT</div>
    <div class="list-container" style="max-height: 250dp;">
        <div data-for="src : sources" class="source-row"
             data-class-selected="src.id == selected_source_id"
             data-event-click="select_source(src.id)">
            <input type="checkbox" data-attrif-checked="src.visible"
                   data-event-click="toggle_source(src.id, src.visible)"/>
            <button class="btn-small" data-event-click="toggle_lock(src.id, src.locked)"
                    data-style-color="src.locked ? '#f0c040' : '#7f8c8d'"
                    style="background: transparent; border: none; padding: 2dp 4dp;">
                <span data-if="src.locked">L</span>
                <span data-if="src.locked == false">U</span>
            </button>
            <div class="name" data-style-color="src.id == selected_source_id ? '#4ecca3' : '#eaeaea'">
                {{src.name}}
            </div>
            <div class="kind">({{src.kind}})</div>
        </div>
    </div>
    <div data-if="selected_source_id >= 0" class="action-row">
        <button class="btn-small" data-event-click="source_up">Up</button>
        <button class="btn-small" data-event-click="source_down">Down</button>
        <button class="btn-small" data-event-click="source_dup">Dup</button>
        <button class="btn-small btn-danger" data-event-click="source_delete">Delete</button>
    </div>
</div>
)rml";

    // Audio tab
    rml += R"rml(
<div data-if="active_tab == 'audio'">
    <div class="section-header">AUDIO MIXER</div>
    <div data-for="audio : audio_items">
        <div class="audio-row">
            <button class="mute-btn"
                    data-class-muted="audio.muted"
                    data-class-unmuted="audio.muted == false"
                    data-event-click="toggle_mute(audio.name)">
                <span data-if="audio.muted">M</span>
                <span data-if="audio.muted == false">U</span>
            </button>
            <button class="expand-btn" data-event-click="expand_audio(audio.name)">
                <span data-if="expanded_audio == audio.name">v</span>
                <span data-if="expanded_audio != audio.name">&gt;</span>
            </button>
            <div class="audio-name">{{audio.name}}</div>
            <input type="range" min="0" max="100"
                   data-value="audio.faderVal"
                   data-event-change="set_volume(audio.name, event.value)"
                   style="flex: 1;"/>
        </div>
        <div data-if="expanded_audio == audio.name" class="audio-advanced">
            <div data-if="has_advanced">
                <div class="adv-row">
                    <label>Sync (ms)</label>
                    <input type="range" min="-2000" max="2000"
                           data-value="adv_sync_ms"
                           data-event-change="set_sync_offset(audio.name, event.value)"/>
                    <span>{{adv_sync_ms}} ms</span>
                </div>
                <div class="adv-row">
                    <label>Balance</label>
                    <input type="range" min="0" max="100"
                           data-value="adv_balance"
                           data-event-change="set_balance(audio.name, event.value)"/>
                </div>
                <div class="adv-row">
                    <label>Monitor</label>
                    <select data-value="adv_monitor_type"
                            data-event-change="set_monitor_type(audio.name, event.value)">
                        <option value="0">Off</option>
                        <option value="1">Monitor Only</option>
                    </select>
                </div>
            </div>
            <div data-if="has_advanced == false" style="color: #7f8c8d;">Loading...</div>
        </div>
    </div>
</div>
)rml";

    // Filters tab
    rml += R"rml(
<div data-if="active_tab == 'filters'">
    <div class="section-header">FILTER MANAGEMENT</div>
    <div style="display: flex; flex-direction: row; gap: 8dp; margin-bottom: 8dp; align-items: center;">
        <select data-value="filter_selected_source"
                data-event-change="select_filter_source(event.value)"
                style="width: 250dp;">
            <option data-for="fs : filter_sources" data-value="fs">{{fs}}</option>
        </select>
        <button class="btn-small" data-event-click="refresh_filters">Refresh</button>
    </div>
    <div class="list-container" style="max-height: 200dp;">
        <div data-for="f : filters" class="filter-row"
             data-class-selected="it_index == filter_selected_idx"
             data-event-click="select_filter(it_index)">
            <input type="checkbox" data-attrif-checked="f.enabled"
                   data-event-click="toggle_filter(f.name, f.enabled)"/>
            <div class="name" data-style-color="it_index == filter_selected_idx ? '#4ecca3' : '#eaeaea'">
                {{f.name}}</div>
            <div class="kind">({{f.kind}})</div>
        </div>
    </div>
    <div data-if="filter_selected_idx >= 0" class="action-row">
        <button class="btn-small" data-event-click="filter_up">Up</button>
        <button class="btn-small" data-event-click="filter_down">Down</button>
        <button class="btn-small btn-danger" data-event-click="filter_delete">Delete</button>
    </div>
</div>
)rml";

    // Transitions tab
    rml += R"rml(
<div data-if="active_tab == 'transitions'">
    <div class="section-header">TRANSITIONS</div>
    <div style="display: flex; flex-direction: row; gap: 8dp; margin-bottom: 8dp; align-items: center;">
        <label style="color: #7f8c8d; width: 80dp;">Transition</label>
        <select data-value="current_transition"
                data-event-change="set_transition(event.value)" style="width: 250dp;">
            <option data-for="t : transitions_list" data-value="t">{{t}}</option>
        </select>
    </div>
    <div style="display: flex; flex-direction: row; gap: 8dp; margin-bottom: 12dp; align-items: center;">
        <label style="color: #7f8c8d; width: 80dp;">Duration</label>
        <input type="range" min="0" max="5000"
               data-value="transition_dur_ms"
               data-event-change="set_transition_dur(event.value)" style="width: 250dp;"/>
        <span>{{transition_dur_ms}} ms</span>
    </div>
    <div class="separator"></div>
    <div class="section-header">STUDIO MODE</div>
    <button data-event-click="toggle_studio_mode"
            data-class-btn-active-stream="studio_mode" style="width: 200dp; margin-bottom: 8dp;">
        <span data-if="studio_mode">Studio Mode: ON</span>
        <span data-if="studio_mode == false">Studio Mode: OFF</span>
    </button>
    <div data-if="studio_mode">
        <div style="display: flex; flex-direction: row; gap: 8dp; margin-bottom: 8dp; align-items: center;">
            <label style="color: #7f8c8d; width: 80dp;">Preview</label>
            <select data-value="preview_scene"
                    data-event-change="set_preview_scene(event.value)" style="width: 250dp;">
                <option data-for="scene : scenes" data-value="scene.name">{{scene.name}}</option>
            </select>
        </div>
        <div style="color: #7f8c8d; margin-bottom: 8dp;">Program: {{current_scene}}</div>
        <button class="btn-accent" data-event-click="trigger_transition" style="width: 200dp; padding: 10dp;">
            Transition &gt;&gt;</button>
    </div>
</div>
)rml";

    // Stats tab
    rml += R"rml(
<div data-if="active_tab == 'stats'">
    <div class="section-header">PERFORMANCE</div>
    <div class="stat-row">
        <div class="stat-label">FPS:</div>
        <div class="stat-value" data-style-color="fps_color">{{stat_fps}}</div>
        <div class="stat-label" style="margin-left: 20dp;">CPU:</div>
        <div class="stat-value" data-style-color="cpu_color">{{stat_cpu}}</div>
    </div>
    <div class="stat-row">
        <div class="stat-label">Memory:</div>
        <div class="stat-value">{{stat_memory}}</div>
        <div class="stat-label" style="margin-left: 20dp;">Frame Time:</div>
        <div class="stat-value">{{stat_frame_time}}</div>
    </div>
    <div class="stat-row">
        <div class="stat-label">Disk:</div>
        <div class="stat-value" data-style-color="disk_color">{{stat_disk}}</div>
    </div>
    <div class="stat-row">
        <div class="stat-label">Render Skip:</div>
        <div class="stat-value" data-style-color="render_skip_color">{{stat_render_skip}}</div>
        <div class="stat-label" style="margin-left: 20dp;">Output Skip:</div>
        <div class="stat-value" data-style-color="output_skip_color">{{stat_output_skip}}</div>
    </div>
    <div class="separator"></div>
    <div class="section-header">HOTKEYS</div>
    <div class="list-container" style="max-height: 150dp;">
        <div data-for="hk : hotkeys" class="hotkey-item"
             data-event-click="trigger_hotkey(hk.rawName)">
            {{hk.displayName}}</div>
    </div>
</div>
)rml";

    // Settings tab
    rml += R"rml(
<div data-if="active_tab == 'settings'">
    <div class="section-header">OVERLAY</div>
    <div style="margin-bottom: 8dp; display: flex; flex-direction: row; align-items: center; gap: 8dp;">
        <input type="checkbox" data-checked="settings_show_notif"/>
        <span>Show notifications</span>
    </div>
    <div style="margin-bottom: 8dp; display: flex; flex-direction: row; align-items: center; gap: 8dp;">
        <label style="color: #7f8c8d; width: 130dp;">Notification msg</label>
        <input type="text" data-value="settings_notif_msg" style="width: 200dp;"/>
    </div>
    <div style="margin-bottom: 8dp; display: flex; flex-direction: row; align-items: center; gap: 8dp;">
        <label style="color: #7f8c8d; width: 130dp;">Duration (s)</label>
        <input type="range" min="1" max="10" data-value="settings_notif_dur" style="width: 200dp;"/>
        <span>{{settings_notif_dur}}</span>
    </div>
    <div style="margin-bottom: 8dp; display: flex; flex-direction: row; align-items: center; gap: 8dp;">
        <input type="checkbox" data-checked="settings_show_rec"/>
        <span>Show REC indicator</span>
    </div>
    <div style="margin-bottom: 12dp; display: flex; flex-direction: row; align-items: center; gap: 8dp;">
        <label style="color: #7f8c8d; width: 130dp;">REC position</label>
        <select data-value="settings_rec_pos_idx" style="width: 200dp;">
            <option value="0">top-left</option>
            <option value="1">top-center</option>
            <option value="2">top-right</option>
            <option value="3">bottom-left</option>
            <option value="4">bottom-center</option>
            <option value="5">bottom-right</option>
        </select>
    </div>
    <div class="separator"></div>
    <div style="display: flex; flex-direction: row; gap: 8dp; align-items: center;">
        <button class="btn-accent" data-event-click="apply_settings" style="width: 150dp;">Apply Settings</button>
        <span style="color: #7f8c8d;">Opens full settings</span>
        <button class="btn-small" data-event-click="open_settings">More...</button>
    </div>
</div>
)rml";

    // Close tab-content, footer, panel, body
    rml += R"rml(
</div>
<div class="footer">{{toggle_hotkey}} toggle | {{save_hotkey}} save</div>
</div>
</body></rml>
)rml";

    return rml;
}
