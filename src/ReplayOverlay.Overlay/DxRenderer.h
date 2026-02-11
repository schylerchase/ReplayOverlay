#pragma once
#include <d3d11.h>
#include <dxgi1_2.h>
#include <dcomp.h>
#include <RmlUi/Core.h>
#include "RmlRenderInterface_DX11.h"
#include "RmlSystemInterface_Win32.h"

namespace Rml { class ElementDocument; }

class DxRenderer
{
public:
    bool Init(HWND hwnd, int width, int height);
    void Shutdown();

    void BeginFrame(float clearR = 0.0f, float clearG = 0.0f, float clearB = 0.0f, float clearA = 0.0f);
    void EndFrame();
    void Resize(int width, int height);

    ID3D11Device*        GetDevice()  const { return m_device; }
    ID3D11DeviceContext* GetContext() const { return m_context; }
    Rml::Context*        GetRmlContext() const { return m_rmlContext; }

    // Load the overlay document (call after data model is set up)
    Rml::ElementDocument* LoadOverlayDocument();

    // Create a texture from raw RGBA pixel data
    ID3D11ShaderResourceView* CreateTextureFromRGBA(
        const unsigned char* pixels, int width, int height);

    // Register an external SRV as a RmlUi texture handle (for preview)
    Rml::TextureHandle RegisterExternalTexture(ID3D11ShaderResourceView* srv);

    // Live preview texture management
    void SetPreviewTexture(ID3D11ShaderResourceView* srv, int w, int h)
    { m_rmlRender.SetPreviewTexture(srv, w, h); }
    void ClearPreviewTexture() { m_rmlRender.ClearPreviewTexture(); }

private:
    void CreateRenderTarget();
    void CleanupRenderTarget();
    HRESULT CreateDCompTarget(HWND hwnd, IDXGISwapChain1* swapChain);
    bool InitRmlUi(HWND hwnd, int width, int height);

    ID3D11Device*           m_device  = nullptr;
    ID3D11DeviceContext*    m_context = nullptr;
    IDXGISwapChain1*        m_swapChain = nullptr;
    ID3D11RenderTargetView* m_rtv = nullptr;

    // DirectComposition for transparent swap chain
    IDCompositionDevice*  m_dcompDevice = nullptr;
    IDCompositionTarget*  m_dcompTarget = nullptr;
    IDCompositionVisual*  m_dcompVisual = nullptr;

    // RmlUi
    RmlRenderInterface_DX11 m_rmlRender;
    RmlSystemInterface_Win32 m_rmlSystem;
    Rml::Context* m_rmlContext = nullptr;
};
