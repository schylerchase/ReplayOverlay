#include <gtest/gtest.h>
#include "OverlayState.h"

TEST(OverlayState, UpdateFromStateJson_ParsesAllFields)
{
    nlohmann::json j = {
        {"connected", true},
        {"scenes", {"Scene1", "Scene2", "Scene3"}},
        {"currentScene", "Scene2"},
        {"isStreaming", false},
        {"isRecording", true},
        {"isBufferActive", true},
        {"hasActiveCapture", true},
        {"sources", {
            {{"id", 1}, {"name", "Camera"}, {"isVisible", true}},
            {{"id", 2}, {"name", "Game"}, {"isVisible", false}}
        }},
        {"audio", {
            {{"name", "Desktop"}, {"volumeMul", 1.0}, {"isMuted", false}},
            {{"name", "Mic"}, {"volumeMul", 0.5}, {"isMuted", true}}
        }}
    };

    OverlayState state;
    state.UpdateFromStateJson(j);

    EXPECT_TRUE(state.connected);
    EXPECT_EQ(state.scenes.size(), 3u);
    EXPECT_EQ(state.currentScene, "Scene2");
    EXPECT_FALSE(state.isStreaming);
    EXPECT_TRUE(state.isRecording);
    EXPECT_TRUE(state.isBufferActive);
    EXPECT_TRUE(state.hasActiveCapture.has_value());
    EXPECT_TRUE(state.hasActiveCapture.value());

    EXPECT_EQ(state.sources.size(), 2u);
    EXPECT_EQ(state.sources[0].id, 1);
    EXPECT_EQ(state.sources[0].name, "Camera");
    EXPECT_TRUE(state.sources[0].isVisible);
    EXPECT_EQ(state.sources[1].name, "Game");
    EXPECT_FALSE(state.sources[1].isVisible);

    EXPECT_EQ(state.audio.size(), 2u);
    EXPECT_EQ(state.audio[0].name, "Desktop");
    EXPECT_DOUBLE_EQ(state.audio[0].volumeMul, 1.0);
    EXPECT_FALSE(state.audio[0].isMuted);
    EXPECT_TRUE(state.audio[1].isMuted);
}

TEST(OverlayState, UpdateFromStateJson_HandlesEmptyArrays)
{
    nlohmann::json j = {
        {"connected", false},
        {"scenes", nlohmann::json::array()},
        {"sources", nlohmann::json::array()},
        {"audio", nlohmann::json::array()}
    };

    OverlayState state;
    state.UpdateFromStateJson(j);

    EXPECT_FALSE(state.connected);
    EXPECT_TRUE(state.scenes.empty());
    EXPECT_TRUE(state.sources.empty());
    EXPECT_TRUE(state.audio.empty());
}

TEST(OverlayState, UpdateFromStateJson_NullHasActiveCapture)
{
    nlohmann::json j = {
        {"hasActiveCapture", nullptr}
    };

    OverlayState state;
    state.UpdateFromStateJson(j);

    EXPECT_FALSE(state.hasActiveCapture.has_value());
}

TEST(OverlayState, UpdateFromConfigJson_ParsesFields)
{
    nlohmann::json j = {
        {"toggleHotkey", "F12"},
        {"saveHotkey", "num add"},
        {"recIndicatorPosition", "bottom-right"},
        {"showRecIndicator", false},
        {"showNotifications", true},
        {"notificationDuration", 5.0},
        {"notificationMessage", "SAVED!"}
    };

    OverlayState state;
    state.UpdateFromConfigJson(j);

    EXPECT_EQ(state.toggleHotkey, "F12");
    EXPECT_EQ(state.saveHotkey, "num add");
    EXPECT_EQ(state.recIndicatorPosition, "bottom-right");
    EXPECT_FALSE(state.showRecIndicator);
    EXPECT_TRUE(state.showNotifications);
    EXPECT_DOUBLE_EQ(state.notificationDuration, 5.0);
    EXPECT_EQ(state.notificationMessage, "SAVED!");
}

TEST(OverlayState, UpdateFromStateJson_PartialUpdate)
{
    OverlayState state;
    state.connected = false;
    state.currentScene = "OldScene";

    // Only update connected, leave currentScene as default
    nlohmann::json j = {{"connected", true}};
    state.UpdateFromStateJson(j);

    EXPECT_TRUE(state.connected);
    // currentScene was not in the JSON, so remains as previous value
    EXPECT_EQ(state.currentScene, "OldScene");
}
