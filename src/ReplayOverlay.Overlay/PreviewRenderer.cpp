#include "PreviewRenderer.h"
#include "DxRenderer.h"
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <vector>
#include <string>

// stb_image for PNG decoding
#define STB_IMAGE_IMPLEMENTATION
#define STBI_ONLY_PNG
#include "stb_image.h"

// Simple base64 decoder
static const std::string base64_chars =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

static std::vector<unsigned char> Base64Decode(const std::string& encoded)
{
    std::vector<unsigned char> decoded;
    decoded.reserve(encoded.size() * 3 / 4);

    int val = 0, valb = -8;
    for (unsigned char c : encoded)
    {
        if (c == '=' || c == '\n' || c == '\r') continue;
        auto pos = base64_chars.find(c);
        if (pos == std::string::npos) continue;
        val = (val << 6) + static_cast<int>(pos);
        valb += 6;
        if (valb >= 0)
        {
            decoded.push_back(static_cast<unsigned char>((val >> valb) & 0xFF));
            valb -= 8;
        }
    }
    return decoded;
}

void PreviewRenderer::UpdateFromBase64(DxRenderer& dx, const std::string& base64Data)
{
    if (base64Data.empty()) return;

    // Decode base64 to PNG bytes
    auto pngData = Base64Decode(base64Data);
    if (pngData.empty())
    {
        OutputDebugStringA("[PreviewRenderer] Base64 decode returned empty result\n");
        return;
    }

    // Decode PNG to RGBA pixels
    int w = 0, h = 0, channels = 0;
    unsigned char* pixels = stbi_load_from_memory(
        pngData.data(), static_cast<int>(pngData.size()),
        &w, &h, &channels, 4); // Force RGBA

    if (!pixels)
    {
        OutputDebugStringA("[PreviewRenderer] stbi_load_from_memory failed\n");
        return;
    }

    // Release old texture
    Release();

    // Create D3D11 texture
    m_srv = dx.CreateTextureFromRGBA(pixels, w, h);
    if (!m_srv)
        OutputDebugStringA("[PreviewRenderer] CreateTextureFromRGBA returned nullptr\n");
    m_width  = w;
    m_height = h;

    stbi_image_free(pixels);
}

void PreviewRenderer::Release()
{
    if (m_srv)
    {
        m_srv->Release();
        m_srv = nullptr;
    }
    m_width  = 0;
    m_height = 0;
}
