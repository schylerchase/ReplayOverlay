#include "DxRenderer.h"
#include "OverlayAssets.h"
#include <string>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "dcomp.lib")

bool DxRenderer::Init(HWND hwnd, int width, int height)
{
    D3D_FEATURE_LEVEL featureLevel;
    UINT createFlags = 0;
#ifdef _DEBUG
    createFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

    // Create device first, then use DXGI factory for swap chain with alpha support
    HRESULT hr = D3D11CreateDevice(
        nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, createFlags,
        nullptr, 0, D3D11_SDK_VERSION,
        &m_device, &featureLevel, &m_context);

    if (FAILED(hr))
        return false;

    // Get DXGI factory from the device
    IDXGIDevice* dxgiDevice = nullptr;
    IDXGIAdapter* dxgiAdapter = nullptr;
    IDXGIFactory2* dxgiFactory = nullptr;

    hr = m_device->QueryInterface(IID_PPV_ARGS(&dxgiDevice));
    if (FAILED(hr) || !dxgiDevice) return false;

    hr = dxgiDevice->GetAdapter(&dxgiAdapter);
    if (FAILED(hr) || !dxgiAdapter) { dxgiDevice->Release(); return false; }

    hr = dxgiAdapter->GetParent(IID_PPV_ARGS(&dxgiFactory));
    if (FAILED(hr) || !dxgiFactory) { dxgiAdapter->Release(); dxgiDevice->Release(); return false; }

    // Create swap chain with premultiplied alpha for DWM transparency
    DXGI_SWAP_CHAIN_DESC1 sd = {};
    sd.Width       = width;
    sd.Height      = height;
    sd.Format      = DXGI_FORMAT_B8G8R8A8_UNORM;
    sd.SampleDesc.Count = 1;
    sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    sd.BufferCount = 2;
    sd.SwapEffect  = DXGI_SWAP_EFFECT_FLIP_DISCARD;
    sd.AlphaMode   = DXGI_ALPHA_MODE_PREMULTIPLIED;

    IDXGISwapChain1* swapChain1 = nullptr;
    hr = dxgiFactory->CreateSwapChainForComposition(m_device, &sd, nullptr, &swapChain1);

    dxgiFactory->Release();
    dxgiAdapter->Release();
    dxgiDevice->Release();

    if (FAILED(hr) || !swapChain1)
        return false;

    m_swapChain = swapChain1;

    // Associate swap chain with the HWND using DirectComposition
    // (required for DXGI_ALPHA_MODE_PREMULTIPLIED)
    hr = CreateDCompTarget(hwnd, swapChain1);
    if (FAILED(hr))
        return false;

    CreateRenderTarget();

    // Initialize RmlUi
    if (!InitRmlUi(hwnd, width, height))
        return false;

    return true;
}

bool DxRenderer::InitRmlUi(HWND hwnd, int width, int height)
{
    // Init render and system interfaces
    if (!m_rmlRender.Init(m_device, m_context))
        return false;

    m_rmlRender.SetViewport(width, height);

    // Install interfaces before Rml::Initialise
    Rml::SetRenderInterface(&m_rmlRender);
    Rml::SetSystemInterface(&m_rmlSystem);

    if (!Rml::Initialise())
        return false;

    // Load Segoe UI system font (regular + bold)
    {
        char winDir[MAX_PATH] = {};
        GetWindowsDirectoryA(winDir, MAX_PATH);
        std::string basePath = std::string(winDir) + "\\Fonts\\";
        if (!Rml::LoadFontFace(basePath + "segoeui.ttf", true))
            Rml::LoadFontFace(basePath + "arial.ttf", true);
        Rml::LoadFontFace(basePath + "segoeuib.ttf", false); // Bold variant
        Rml::LoadFontFace(basePath + "segmdl2.ttf", false);  // Icon font (Segoe MDL2 Assets)
    }

    // Create context at viewport size
    m_rmlContext = Rml::CreateContext("main", Rml::Vector2i(width, height));
    if (!m_rmlContext)
        return false;

    // Register theme CSS as a virtual document
    // RmlUi can load documents that reference other documents via link tags,
    // but for embedded assets we load the stylesheet directly into a document string.
    // We inject the theme CSS inline into the overlay RML.

    return true;
}

Rml::ElementDocument* DxRenderer::LoadOverlayDocument()
{
    if (!m_rmlContext) return nullptr;

    // Build the complete RML with inline CSS
    std::string rml = GetOverlayDocumentRml();
    std::string css = GetOverlayThemeRcss();

    // Replace __THEME__ placeholder with actual CSS
    std::string placeholder = "__THEME__";
    auto pos = rml.find(placeholder);
    if (pos != std::string::npos)
        rml.replace(pos, placeholder.length(), css);

    auto* doc = m_rmlContext->LoadDocumentFromMemory(rml);
    if (doc)
        doc->Show();

    return doc;
}

HRESULT DxRenderer::CreateDCompTarget(HWND hwnd, IDXGISwapChain1* swapChain)
{
    IDXGIDevice* dxgiDevice = nullptr;
    m_device->QueryInterface(IID_PPV_ARGS(&dxgiDevice));

    HRESULT hr = DCompositionCreateDevice(dxgiDevice, IID_PPV_ARGS(&m_dcompDevice));
    dxgiDevice->Release();
    if (FAILED(hr)) return hr;

    hr = m_dcompDevice->CreateTargetForHwnd(hwnd, TRUE, &m_dcompTarget);
    if (FAILED(hr)) return hr;

    hr = m_dcompDevice->CreateVisual(&m_dcompVisual);
    if (FAILED(hr)) return hr;

    m_dcompVisual->SetContent(swapChain);
    m_dcompTarget->SetRoot(m_dcompVisual);
    m_dcompDevice->Commit();

    return S_OK;
}

void DxRenderer::Shutdown()
{
    if (m_rmlContext)
    {
        Rml::RemoveContext("main");
        m_rmlContext = nullptr;
    }
    Rml::Shutdown();
    m_rmlRender.Shutdown();

    CleanupRenderTarget();
    if (m_dcompVisual) { m_dcompVisual->Release(); m_dcompVisual = nullptr; }
    if (m_dcompTarget) { m_dcompTarget->Release(); m_dcompTarget = nullptr; }
    if (m_dcompDevice) { m_dcompDevice->Release(); m_dcompDevice = nullptr; }
    if (m_swapChain) { m_swapChain->Release(); m_swapChain = nullptr; }
    if (m_context)   { m_context->Release();   m_context = nullptr; }
    if (m_device)    { m_device->Release();     m_device = nullptr; }
}

void DxRenderer::BeginFrame(float clearR, float clearG, float clearB, float clearA)
{
    float clearColor[4] = { clearR, clearG, clearB, clearA };
    m_context->OMSetRenderTargets(1, &m_rtv, nullptr);
    m_context->ClearRenderTargetView(m_rtv, clearColor);
}

void DxRenderer::EndFrame()
{
    if (m_rmlContext)
    {
        // Update() is called earlier in OverlayApp::Tick() so that direct
        // element manipulation (SetAttribute) happens after data-if processing.
        m_rmlContext->Render();
    }
    m_swapChain->Present(1, 0); // VSync on
}

void DxRenderer::Resize(int width, int height)
{
    if (width <= 0 || height <= 0) return;

    CleanupRenderTarget();
    m_swapChain->ResizeBuffers(0, width, height, DXGI_FORMAT_UNKNOWN, 0);
    CreateRenderTarget();

    m_rmlRender.SetViewport(width, height);
    if (m_rmlContext)
        m_rmlContext->SetDimensions(Rml::Vector2i(width, height));
}

void DxRenderer::CreateRenderTarget()
{
    ID3D11Texture2D* backBuffer = nullptr;
    m_swapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer));
    if (backBuffer)
    {
        m_device->CreateRenderTargetView(backBuffer, nullptr, &m_rtv);
        backBuffer->Release();
    }
}

void DxRenderer::CleanupRenderTarget()
{
    if (m_rtv) { m_rtv->Release(); m_rtv = nullptr; }
}

Rml::TextureHandle DxRenderer::RegisterExternalTexture(ID3D11ShaderResourceView* srv)
{
    return m_rmlRender.RegisterExternalTexture(srv);
}

ID3D11ShaderResourceView* DxRenderer::CreateTextureFromRGBA(
    const unsigned char* pixels, int width, int height)
{
    if (!pixels || !m_device) return nullptr;

    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width     = width;
    desc.Height    = height;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format    = DXGI_FORMAT_R8G8B8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage     = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

    D3D11_SUBRESOURCE_DATA subData = {};
    subData.pSysMem     = pixels;
    subData.SysMemPitch = width * 4;

    ID3D11Texture2D* texture = nullptr;
    HRESULT hr = m_device->CreateTexture2D(&desc, &subData, &texture);
    if (FAILED(hr) || !texture) return nullptr;

    D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
    srvDesc.Format = desc.Format;
    srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
    srvDesc.Texture2D.MipLevels = 1;

    ID3D11ShaderResourceView* srv = nullptr;
    hr = m_device->CreateShaderResourceView(texture, &srvDesc, &srv);
    texture->Release();

    return SUCCEEDED(hr) ? srv : nullptr;
}
