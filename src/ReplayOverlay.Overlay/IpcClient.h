#pragma once
#include <string>
#include <optional>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <nlohmann/json.hpp>

struct IpcMessage
{
    std::string type;
    nlohmann::json payload;
};

class IpcClient
{
public:
    bool Connect(const std::string& pipeName);
    void Disconnect();
    bool IsConnected() const { return m_pipe != INVALID_HANDLE_VALUE; }

    bool SendMessage(const IpcMessage& msg);
    std::optional<IpcMessage> ReadMessage(); // Non-blocking

private:
    bool WriteExact(const void* data, DWORD size);
    bool ReadExact(void* data, DWORD size);

    HANDLE m_pipe = INVALID_HANDLE_VALUE;
};
