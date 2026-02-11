#include <gtest/gtest.h>
#include "IpcClient.h"

TEST(IpcClient, InitiallyDisconnected)
{
    IpcClient client;
    EXPECT_FALSE(client.IsConnected());
}

TEST(IpcClient, ConnectToNonexistentPipeFails)
{
    IpcClient client;
    EXPECT_FALSE(client.Connect("NonexistentTestPipe_12345"));
    EXPECT_FALSE(client.IsConnected());
}

TEST(IpcClient, ReadMessageWhenDisconnectedReturnsNullopt)
{
    IpcClient client;
    auto msg = client.ReadMessage();
    EXPECT_FALSE(msg.has_value());
}

TEST(IpcClient, SendMessageWhenDisconnectedReturnsFalse)
{
    IpcClient client;
    IpcMessage msg{"test", {}};
    EXPECT_FALSE(client.SendMessage(msg));
}

TEST(IpcClient, DisconnectWhenAlreadyDisconnected)
{
    IpcClient client;
    // Should not crash
    client.Disconnect();
    EXPECT_FALSE(client.IsConnected());
}

TEST(IpcMessage, ConstructWithTypeAndPayload)
{
    IpcMessage msg;
    msg.type = "switch_scene";
    msg.payload = {{"name", "Gaming"}};

    EXPECT_EQ(msg.type, "switch_scene");
    EXPECT_EQ(msg.payload["name"], "Gaming");
}
