#include "IpcClient.h"
#include <vector>

static constexpr uint32_t MaxIpcMessageBytes = 10 * 1024 * 1024; // 10MB safety limit

bool IpcClient::Connect(const std::string& pipeName)
{
    Disconnect();

    std::string fullName = "\\\\.\\pipe\\" + pipeName;

    m_pipe = CreateFileA(
        fullName.c_str(),
        GENERIC_READ | GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        0,
        nullptr);

    if (m_pipe == INVALID_HANDLE_VALUE)
    {
        // Pipe may not be ready yet
        if (GetLastError() == ERROR_PIPE_BUSY)
        {
            if (WaitNamedPipeA(fullName.c_str(), 5000))
            {
                m_pipe = CreateFileA(
                    fullName.c_str(),
                    GENERIC_READ | GENERIC_WRITE,
                    0, nullptr, OPEN_EXISTING, 0, nullptr);
            }
        }
    }

    if (m_pipe == INVALID_HANDLE_VALUE)
        return false;

    // Set pipe to message-read mode
    DWORD mode = PIPE_READMODE_BYTE;
    SetNamedPipeHandleState(m_pipe, &mode, nullptr, nullptr);

    return true;
}

void IpcClient::Disconnect()
{
    if (m_pipe != INVALID_HANDLE_VALUE)
    {
        CloseHandle(m_pipe);
        m_pipe = INVALID_HANDLE_VALUE;
    }
}

bool IpcClient::SendMessage(const IpcMessage& msg)
{
    if (!IsConnected()) return false;

    try
    {
        nlohmann::json j;
        j["type"] = msg.type;
        j["payload"] = msg.payload.dump();

        std::string json = j.dump();
        uint32_t length = static_cast<uint32_t>(json.size());

        if (!WriteExact(&length, sizeof(length))) return false;
        if (!WriteExact(json.data(), length)) return false;

        return true;
    }
    catch (const std::exception& ex)
    {
        OutputDebugStringA("[IPC] SendMessage exception: ");
        OutputDebugStringA(ex.what());
        OutputDebugStringA("\n");
        return false;
    }
    catch (...)
    {
        OutputDebugStringA("[IPC] SendMessage unknown exception\n");
        return false;
    }
}

std::optional<IpcMessage> IpcClient::ReadMessage()
{
    if (!IsConnected()) return std::nullopt;

    // Check if data is available (non-blocking peek)
    DWORD available = 0;
    if (!PeekNamedPipe(m_pipe, nullptr, 0, nullptr, &available, nullptr))
    {
        Disconnect();
        return std::nullopt;
    }

    if (available < 4) return std::nullopt; // Not enough data for length prefix

    // Read 4-byte length prefix
    uint32_t length = 0;
    if (!ReadExact(&length, sizeof(length)))
    {
        Disconnect();
        return std::nullopt;
    }

    if (length == 0 || length > MaxIpcMessageBytes)
    {
        Disconnect();
        return std::nullopt;
    }

    // Read JSON body
    std::vector<char> buffer(length);
    if (!ReadExact(buffer.data(), length))
    {
        Disconnect();
        return std::nullopt;
    }

    try
    {
        std::string json(buffer.begin(), buffer.end());
        auto j = nlohmann::json::parse(json);

        IpcMessage msg;
        msg.type = j.value("type", "");

        // Payload comes as a JSON string that needs to be parsed
        std::string payloadStr = j.value("payload", "{}");
        msg.payload = nlohmann::json::parse(payloadStr);

        return msg;
    }
    catch (const std::exception& ex)
    {
        OutputDebugStringA("[IPC] ReadMessage exception: ");
        OutputDebugStringA(ex.what());
        OutputDebugStringA("\n");
        return std::nullopt;
    }
    catch (...)
    {
        OutputDebugStringA("[IPC] ReadMessage unknown exception\n");
        return std::nullopt;
    }
}

bool IpcClient::WriteExact(const void* data, DWORD size)
{
    const char* ptr = static_cast<const char*>(data);
    DWORD remaining = size;

    while (remaining > 0)
    {
        DWORD written = 0;
        if (!WriteFile(m_pipe, ptr, remaining, &written, nullptr))
            return false;
        ptr += written;
        remaining -= written;
    }

    FlushFileBuffers(m_pipe);
    return true;
}

bool IpcClient::ReadExact(void* data, DWORD size)
{
    char* ptr = static_cast<char*>(data);
    DWORD remaining = size;

    while (remaining > 0)
    {
        DWORD read = 0;
        if (!ReadFile(m_pipe, ptr, remaining, &read, nullptr))
            return false;
        if (read == 0) return false; // pipe closed
        ptr += read;
        remaining -= read;
    }

    return true;
}
