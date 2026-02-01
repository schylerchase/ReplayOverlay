#pragma once
#include <string>
#include <d3d11.h>

class DxRenderer;

class PreviewRenderer
{
public:
    void UpdateFromBase64(DxRenderer& dx, const std::string& base64Data);
    void Release();

    ID3D11ShaderResourceView* GetTexture() const { return m_srv; }
    int GetWidth()  const { return m_width; }
    int GetHeight() const { return m_height; }

private:
    ID3D11ShaderResourceView* m_srv = nullptr;
    int m_width  = 0;
    int m_height = 0;
};
