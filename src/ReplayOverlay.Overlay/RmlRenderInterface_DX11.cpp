#include "RmlRenderInterface_DX11.h"
#include <cstring>
#include <vector>
#include <unordered_map>

#pragma comment(lib, "d3dcompiler.lib")

// --- Embedded HLSL shaders ---

static const char* s_vertexShaderSrc = R"hlsl(
cbuffer Constants : register(b0)
{
    float4x4 transform;
    float2 translation;
    float2 padding;
};

struct VS_IN
{
    float2 pos   : POSITION;
    float4 color : COLOR;
    float2 uv    : TEXCOORD;
};

struct VS_OUT
{
    float4 pos   : SV_Position;
    float4 color : COLOR;
    float2 uv    : TEXCOORD;
};

VS_OUT main(VS_IN input)
{
    VS_OUT output;
    float2 p = input.pos + translation;
    output.pos = mul(transform, float4(p, 0.0f, 1.0f));
    // RmlUi 6.0 already premultiplies vertex colors on the CPU side
    output.color = input.color;
    output.uv = input.uv;
    return output;
}
)hlsl";

static const char* s_pixelShaderSrc = R"hlsl(
Texture2D tex : register(t0);
SamplerState samp : register(s0);

struct PS_IN
{
    float4 pos   : SV_Position;
    float4 color : COLOR;
    float2 uv    : TEXCOORD;
};

float4 main(PS_IN input) : SV_Target
{
    // RmlUi 6.0 premultiplies both vertex colors and textures on the CPU.
    // Simple component-wise multiply produces correct premultiplied output
    // for DComp compositing with blend state ONE / INV_SRC_ALPHA.
    return input.color * tex.Sample(samp, input.uv);
}
)hlsl";

// --- Init / Shutdown ---

bool RmlRenderInterface_DX11::Init(ID3D11Device* device, ID3D11DeviceContext* context)
{
    m_device = device;
    m_context = context;

    if (!CreateShaders()) return false;
    if (!CreatePipelineState()) return false;

    m_whiteTexture = CreateWhiteTexture();
    if (!m_whiteTexture) return false;

    return true;
}

void RmlRenderInterface_DX11::Shutdown()
{
    // Release all compiled geometries
    for (auto& [id, geo] : m_geometries)
    {
        if (geo.vertexBuffer) geo.vertexBuffer->Release();
        if (geo.indexBuffer) geo.indexBuffer->Release();
    }
    m_geometries.clear();

    // Release all textures (except external)
    for (auto& [id, tex] : m_textures)
    {
        if (tex.srv && !tex.external) tex.srv->Release();
    }
    m_textures.clear();

    if (m_whiteTexture) { m_whiteTexture->Release(); m_whiteTexture = nullptr; }
    if (m_vertexShader) { m_vertexShader->Release(); m_vertexShader = nullptr; }
    if (m_pixelShader) { m_pixelShader->Release(); m_pixelShader = nullptr; }
    if (m_inputLayout) { m_inputLayout->Release(); m_inputLayout = nullptr; }
    if (m_constantBuffer) { m_constantBuffer->Release(); m_constantBuffer = nullptr; }
    if (m_blendState) { m_blendState->Release(); m_blendState = nullptr; }
    if (m_rasterizerState) { m_rasterizerState->Release(); m_rasterizerState = nullptr; }
    if (m_rasterizerStateScissor) { m_rasterizerStateScissor->Release(); m_rasterizerStateScissor = nullptr; }
    if (m_sampler) { m_sampler->Release(); m_sampler = nullptr; }
    if (m_depthStencilState) { m_depthStencilState->Release(); m_depthStencilState = nullptr; }
}

// --- Setup helpers ---

bool RmlRenderInterface_DX11::CreateShaders()
{
    HRESULT hr;
    ID3DBlob* vsBlob = nullptr;
    ID3DBlob* psBlob = nullptr;
    ID3DBlob* errorBlob = nullptr;

    // Compile vertex shader
    hr = D3DCompile(s_vertexShaderSrc, strlen(s_vertexShaderSrc), "RmlVS",
                    nullptr, nullptr, "main", "vs_4_0", 0, 0, &vsBlob, &errorBlob);
    if (FAILED(hr))
    {
        if (errorBlob) errorBlob->Release();
        return false;
    }

    // Compile pixel shader
    hr = D3DCompile(s_pixelShaderSrc, strlen(s_pixelShaderSrc), "RmlPS",
                    nullptr, nullptr, "main", "ps_4_0", 0, 0, &psBlob, &errorBlob);
    if (FAILED(hr))
    {
        vsBlob->Release();
        if (errorBlob) errorBlob->Release();
        return false;
    }

    // Create shader objects
    hr = m_device->CreateVertexShader(vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(),
                                       nullptr, &m_vertexShader);
    if (FAILED(hr)) { vsBlob->Release(); psBlob->Release(); return false; }

    hr = m_device->CreatePixelShader(psBlob->GetBufferPointer(), psBlob->GetBufferSize(),
                                      nullptr, &m_pixelShader);
    if (FAILED(hr)) { vsBlob->Release(); psBlob->Release(); return false; }

    // Input layout matching Rml::Vertex
    // Rml::Vertex = { Vector2f position, Colourb colour, Vector2f tex_coord }
    D3D11_INPUT_ELEMENT_DESC layout[] =
    {
        { "POSITION", 0, DXGI_FORMAT_R32G32_FLOAT,       0, offsetof(Rml::Vertex, position),  D3D11_INPUT_PER_VERTEX_DATA, 0 },
        { "COLOR",    0, DXGI_FORMAT_R8G8B8A8_UNORM,     0, offsetof(Rml::Vertex, colour),    D3D11_INPUT_PER_VERTEX_DATA, 0 },
        { "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT,       0, offsetof(Rml::Vertex, tex_coord), D3D11_INPUT_PER_VERTEX_DATA, 0 },
    };

    hr = m_device->CreateInputLayout(layout, 3, vsBlob->GetBufferPointer(),
                                      vsBlob->GetBufferSize(), &m_inputLayout);

    vsBlob->Release();
    psBlob->Release();

    return SUCCEEDED(hr);
}

bool RmlRenderInterface_DX11::CreatePipelineState()
{
    HRESULT hr;

    // Constant buffer
    {
        D3D11_BUFFER_DESC bd = {};
        bd.ByteWidth = sizeof(ConstantBuffer);
        bd.Usage = D3D11_USAGE_DYNAMIC;
        bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        bd.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
        hr = m_device->CreateBuffer(&bd, nullptr, &m_constantBuffer);
        if (FAILED(hr)) return false;
    }

    // Blend state: premultiplied alpha
    {
        D3D11_BLEND_DESC bd = {};
        bd.RenderTarget[0].BlendEnable = TRUE;
        bd.RenderTarget[0].SrcBlend = D3D11_BLEND_ONE;
        bd.RenderTarget[0].DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
        bd.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
        bd.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
        bd.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
        bd.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
        bd.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
        hr = m_device->CreateBlendState(&bd, &m_blendState);
        if (FAILED(hr)) return false;
    }

    // Rasterizer state: no culling (RmlUi may use either winding), no scissor
    {
        D3D11_RASTERIZER_DESC rd = {};
        rd.FillMode = D3D11_FILL_SOLID;
        rd.CullMode = D3D11_CULL_NONE;
        rd.ScissorEnable = FALSE;
        rd.DepthClipEnable = TRUE;
        hr = m_device->CreateRasterizerState(&rd, &m_rasterizerState);
        if (FAILED(hr)) return false;

        // Scissor-enabled variant
        rd.ScissorEnable = TRUE;
        hr = m_device->CreateRasterizerState(&rd, &m_rasterizerStateScissor);
        if (FAILED(hr)) return false;
    }

    // Sampler state: bilinear
    {
        D3D11_SAMPLER_DESC sd = {};
        sd.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        sd.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
        sd.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
        sd.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
        hr = m_device->CreateSamplerState(&sd, &m_sampler);
        if (FAILED(hr)) return false;
    }

    // Depth stencil: disabled
    {
        D3D11_DEPTH_STENCIL_DESC dd = {};
        dd.DepthEnable = FALSE;
        dd.StencilEnable = FALSE;
        hr = m_device->CreateDepthStencilState(&dd, &m_depthStencilState);
        if (FAILED(hr)) return false;
    }

    return true;
}

ID3D11ShaderResourceView* RmlRenderInterface_DX11::CreateWhiteTexture()
{
    unsigned char white[4] = { 255, 255, 255, 255 };

    D3D11_TEXTURE2D_DESC td = {};
    td.Width = 1;
    td.Height = 1;
    td.MipLevels = 1;
    td.ArraySize = 1;
    td.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    td.SampleDesc.Count = 1;
    td.Usage = D3D11_USAGE_IMMUTABLE;
    td.BindFlags = D3D11_BIND_SHADER_RESOURCE;

    D3D11_SUBRESOURCE_DATA data = {};
    data.pSysMem = white;
    data.SysMemPitch = 4;

    ID3D11Texture2D* tex = nullptr;
    HRESULT hr = m_device->CreateTexture2D(&td, &data, &tex);
    if (FAILED(hr)) return nullptr;

    ID3D11ShaderResourceView* srv = nullptr;
    hr = m_device->CreateShaderResourceView(tex, nullptr, &srv);
    tex->Release();
    return SUCCEEDED(hr) ? srv : nullptr;
}

void RmlRenderInterface_DX11::PreMultiplyAlpha(unsigned char* pixels, int w, int h)
{
    for (int i = 0; i < w * h; i++)
    {
        unsigned char* px = pixels + i * 4;
        float a = px[3] / 255.0f;
        px[0] = static_cast<unsigned char>(px[0] * a);
        px[1] = static_cast<unsigned char>(px[1] * a);
        px[2] = static_cast<unsigned char>(px[2] * a);
    }
}

void RmlRenderInterface_DX11::SetViewport(int width, int height)
{
    m_viewportWidth = width;
    m_viewportHeight = height;
}

Rml::TextureHandle RmlRenderInterface_DX11::RegisterExternalTexture(ID3D11ShaderResourceView* srv)
{
    uintptr_t handle = m_nextTextureHandle++;
    m_textures[handle] = { srv, true };
    return static_cast<Rml::TextureHandle>(handle);
}

// --- Geometry ---

Rml::CompiledGeometryHandle RmlRenderInterface_DX11::CompileGeometry(
    Rml::Span<const Rml::Vertex> vertices, Rml::Span<const int> indices)
{
    CompiledGeometry geo = {};
    geo.indexCount = static_cast<int>(indices.size());

    // Vertex buffer
    {
        D3D11_BUFFER_DESC bd = {};
        bd.ByteWidth = static_cast<UINT>(vertices.size() * sizeof(Rml::Vertex));
        bd.Usage = D3D11_USAGE_IMMUTABLE;
        bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;

        D3D11_SUBRESOURCE_DATA data = {};
        data.pSysMem = vertices.data();

        if (FAILED(m_device->CreateBuffer(&bd, &data, &geo.vertexBuffer)))
            return {};
    }

    // Index buffer
    {
        D3D11_BUFFER_DESC bd = {};
        bd.ByteWidth = static_cast<UINT>(indices.size() * sizeof(int));
        bd.Usage = D3D11_USAGE_IMMUTABLE;
        bd.BindFlags = D3D11_BIND_INDEX_BUFFER;

        D3D11_SUBRESOURCE_DATA data = {};
        data.pSysMem = indices.data();

        if (FAILED(m_device->CreateBuffer(&bd, &data, &geo.indexBuffer)))
        {
            geo.vertexBuffer->Release();
            return {};
        }
    }

    uintptr_t handle = m_nextGeometryHandle++;
    m_geometries[handle] = geo;
    return static_cast<Rml::CompiledGeometryHandle>(handle);
}

void RmlRenderInterface_DX11::RenderGeometry(Rml::CompiledGeometryHandle handle,
                                              Rml::Vector2f translation,
                                              Rml::TextureHandle texture)
{
    auto it = m_geometries.find(static_cast<uintptr_t>(handle));
    if (it == m_geometries.end()) return;

    auto& geo = it->second;

    // Update constant buffer with orthographic projection + translation
    {
        D3D11_MAPPED_SUBRESOURCE mapped;
        m_context->Map(m_constantBuffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped);
        auto* cb = static_cast<ConstantBuffer*>(mapped.pData);

        // Orthographic projection: [0, viewportW] x [0, viewportH] -> [-1, 1] x [-1, 1]
        // Column-major storage for HLSL mul(matrix, vector)
        if (m_hasTransform)
        {
            // Apply custom transform, then orthographic projection
            // Build ortho matrix
            float ortho[16] = {};
            ortho[0]  = 2.0f / m_viewportWidth;
            ortho[5]  = -2.0f / m_viewportHeight;
            ortho[10] = 1.0f;
            ortho[12] = -1.0f;
            ortho[13] = 1.0f;
            ortho[15] = 1.0f;

            // Multiply: ortho * m_transformMatrix (both column-major)
            for (int c = 0; c < 4; c++)
            {
                for (int r = 0; r < 4; r++)
                {
                    float sum = 0.0f;
                    for (int k = 0; k < 4; k++)
                        sum += ortho[k * 4 + r] * m_transformMatrix[c * 4 + k];
                    cb->transform[c * 4 + r] = sum;
                }
            }
        }
        else
        {
            // Simple orthographic projection (column-major)
            memset(cb->transform, 0, sizeof(cb->transform));
            cb->transform[0]  = 2.0f / m_viewportWidth;   // [0][0]
            cb->transform[5]  = -2.0f / m_viewportHeight;  // [1][1]
            cb->transform[10] = 1.0f;                       // [2][2]
            cb->transform[12] = -1.0f;                      // [3][0]
            cb->transform[13] = 1.0f;                       // [3][1]
            cb->transform[15] = 1.0f;                       // [3][3]
        }

        cb->translation[0] = translation.x;
        cb->translation[1] = translation.y;
        cb->padding[0] = 0.0f;
        cb->padding[1] = 0.0f;

        m_context->Unmap(m_constantBuffer, 0);
    }

    // Bind pipeline state
    UINT stride = sizeof(Rml::Vertex);
    UINT offset = 0;
    m_context->IASetVertexBuffers(0, 1, &geo.vertexBuffer, &stride, &offset);
    m_context->IASetIndexBuffer(geo.indexBuffer, DXGI_FORMAT_R32_UINT, 0);
    m_context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    m_context->IASetInputLayout(m_inputLayout);

    m_context->VSSetShader(m_vertexShader, nullptr, 0);
    m_context->VSSetConstantBuffers(0, 1, &m_constantBuffer);
    m_context->PSSetShader(m_pixelShader, nullptr, 0);
    m_context->PSSetSamplers(0, 1, &m_sampler);

    // Bind texture
    ID3D11ShaderResourceView* srv = m_whiteTexture;
    if (texture)
    {
        auto texIt = m_textures.find(static_cast<uintptr_t>(texture));
        if (texIt != m_textures.end() && texIt->second.srv)
            srv = texIt->second.srv;
    }
    m_context->PSSetShaderResources(0, 1, &srv);

    // State
    float blendFactor[4] = { 0, 0, 0, 0 };
    m_context->OMSetBlendState(m_blendState, blendFactor, 0xFFFFFFFF);
    m_context->OMSetDepthStencilState(m_depthStencilState, 0);
    m_context->RSSetState(m_scissorEnabled ? m_rasterizerStateScissor : m_rasterizerState);

    // Viewport
    D3D11_VIEWPORT vp = {};
    vp.Width = static_cast<float>(m_viewportWidth);
    vp.Height = static_cast<float>(m_viewportHeight);
    vp.MaxDepth = 1.0f;
    m_context->RSSetViewports(1, &vp);

    // Draw
    m_context->DrawIndexed(geo.indexCount, 0, 0);
}

void RmlRenderInterface_DX11::ReleaseGeometry(Rml::CompiledGeometryHandle handle)
{
    auto it = m_geometries.find(static_cast<uintptr_t>(handle));
    if (it == m_geometries.end()) return;

    if (it->second.vertexBuffer) it->second.vertexBuffer->Release();
    if (it->second.indexBuffer) it->second.indexBuffer->Release();
    m_geometries.erase(it);
}

// --- Textures ---

Rml::TextureHandle RmlRenderInterface_DX11::LoadTexture(Rml::Vector2i& dimensions,
                                                         const Rml::String& source)
{
    // Virtual texture for live OBS preview.
    // Use find() because RmlUi's JoinPath may prepend a document path.
    // Always return a valid handle so RmlUi caches it; SetPreviewTexture()
    // updates the texture map entry when a real frame arrives.
    if (source.find("__preview__") != Rml::String::npos)
    {
        if (m_previewHandle == 0)
            m_previewHandle = m_nextTextureHandle++;

        if (m_previewSrv)
        {
            dimensions.x = m_previewWidth;
            dimensions.y = m_previewHeight;
            m_textures[m_previewHandle] = { m_previewSrv, true };
        }
        else
        {
            dimensions.x = 1;
            dimensions.y = 1;
            m_textures[m_previewHandle] = { m_whiteTexture, true };
        }
        return static_cast<Rml::TextureHandle>(m_previewHandle);
    }
    return {};
}

void RmlRenderInterface_DX11::SetPreviewTexture(ID3D11ShaderResourceView* srv, int w, int h)
{
    m_previewSrv = srv;
    m_previewWidth = w;
    m_previewHeight = h;
    // Keep the texture map entry pointing to the latest SRV
    if (m_previewHandle != 0)
        m_textures[m_previewHandle] = { srv, true };
}

void RmlRenderInterface_DX11::ClearPreviewTexture()
{
    m_previewSrv = nullptr;
    m_previewWidth = 0;
    m_previewHeight = 0;
    // Preserve the handle -- RmlUi's FileTextureDatabase still references it.
    // Replace the SRV with the white placeholder so the old SRV can be safely freed.
    if (m_previewHandle != 0)
        m_textures[m_previewHandle] = { m_whiteTexture, true };
}

Rml::TextureHandle RmlRenderInterface_DX11::GenerateTexture(
    Rml::Span<const Rml::byte> source, Rml::Vector2i dimensions)
{
    int w = dimensions.x;
    int h = dimensions.y;

    // RmlUi 6.0 already provides premultiplied pixel data -- upload as-is
    D3D11_TEXTURE2D_DESC td = {};
    td.Width = w;
    td.Height = h;
    td.MipLevels = 1;
    td.ArraySize = 1;
    td.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    td.SampleDesc.Count = 1;
    td.Usage = D3D11_USAGE_IMMUTABLE;
    td.BindFlags = D3D11_BIND_SHADER_RESOURCE;

    D3D11_SUBRESOURCE_DATA data = {};
    data.pSysMem = source.data();
    data.SysMemPitch = w * 4;

    ID3D11Texture2D* tex = nullptr;
    if (FAILED(m_device->CreateTexture2D(&td, &data, &tex)))
        return {};

    ID3D11ShaderResourceView* srv = nullptr;
    HRESULT hr = m_device->CreateShaderResourceView(tex, nullptr, &srv);
    tex->Release();
    if (FAILED(hr)) return {};

    uintptr_t handle = m_nextTextureHandle++;
    m_textures[handle] = { srv, false };
    return static_cast<Rml::TextureHandle>(handle);
}

void RmlRenderInterface_DX11::ReleaseTexture(Rml::TextureHandle handle)
{
    auto it = m_textures.find(static_cast<uintptr_t>(handle));
    if (it == m_textures.end()) return;

    if (it->second.srv && !it->second.external)
        it->second.srv->Release();
    m_textures.erase(it);
}

// --- Scissor ---

void RmlRenderInterface_DX11::EnableScissorRegion(bool enable)
{
    m_scissorEnabled = enable;
}

void RmlRenderInterface_DX11::SetScissorRegion(Rml::Rectanglei region)
{
    D3D11_RECT rect;
    rect.left   = region.Left();
    rect.top    = region.Top();
    rect.right  = region.Right();
    rect.bottom = region.Bottom();
    m_context->RSSetScissorRects(1, &rect);
}

// --- Transform ---

void RmlRenderInterface_DX11::SetTransform(const Rml::Matrix4f* transform)
{
    if (transform)
    {
        m_hasTransform = true;
        // RmlUi Matrix4f is column-major, same as our storage
        memcpy(m_transformMatrix, transform->data(), sizeof(m_transformMatrix));
    }
    else
    {
        m_hasTransform = false;
    }
}
