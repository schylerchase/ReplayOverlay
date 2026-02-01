#pragma once
#include <RmlUi/Core/RenderInterface.h>
#include <d3d11.h>
#include <d3dcompiler.h>

class RmlRenderInterface_DX11 : public Rml::RenderInterface
{
public:
    bool Init(ID3D11Device* device, ID3D11DeviceContext* context);
    void Shutdown();

    // Set viewport dimensions (call before rendering)
    void SetViewport(int width, int height);

    // Register an externally-owned SRV as a texture handle (for preview)
    Rml::TextureHandle RegisterExternalTexture(ID3D11ShaderResourceView* srv);

    // --- Rml::RenderInterface overrides ---

    Rml::CompiledGeometryHandle CompileGeometry(Rml::Span<const Rml::Vertex> vertices,
                                                 Rml::Span<const int> indices) override;
    void RenderGeometry(Rml::CompiledGeometryHandle handle, Rml::Vector2f translation,
                        Rml::TextureHandle texture) override;
    void ReleaseGeometry(Rml::CompiledGeometryHandle handle) override;

    Rml::TextureHandle LoadTexture(Rml::Vector2i& dimensions,
                                    const Rml::String& source) override;
    Rml::TextureHandle GenerateTexture(Rml::Span<const Rml::byte> source,
                                        Rml::Vector2i dimensions) override;
    void ReleaseTexture(Rml::TextureHandle handle) override;

    void EnableScissorRegion(bool enable) override;
    void SetScissorRegion(Rml::Rectanglei region) override;

    void SetTransform(const Rml::Matrix4f* transform) override;

private:
    struct CompiledGeometry
    {
        ID3D11Buffer* vertexBuffer = nullptr;
        ID3D11Buffer* indexBuffer = nullptr;
        int indexCount = 0;
    };

    struct TextureData
    {
        ID3D11ShaderResourceView* srv = nullptr;
        bool external = false; // Don't release externally-owned textures
    };

    struct alignas(16) ConstantBuffer
    {
        float transform[16]; // 4x4 column-major matrix
        float translation[2];
        float padding[2];
    };

    bool CreateShaders();
    bool CreatePipelineState();
    ID3D11ShaderResourceView* CreateWhiteTexture();
    void PreMultiplyAlpha(unsigned char* pixels, int w, int h);

    ID3D11Device*           m_device = nullptr;
    ID3D11DeviceContext*    m_context = nullptr;

    // Pipeline state
    ID3D11VertexShader*     m_vertexShader = nullptr;
    ID3D11PixelShader*      m_pixelShader = nullptr;
    ID3D11InputLayout*      m_inputLayout = nullptr;
    ID3D11Buffer*           m_constantBuffer = nullptr;
    ID3D11BlendState*       m_blendState = nullptr;
    ID3D11RasterizerState*  m_rasterizerState = nullptr;
    ID3D11RasterizerState*  m_rasterizerStateScissor = nullptr;
    ID3D11SamplerState*     m_sampler = nullptr;
    ID3D11DepthStencilState* m_depthStencilState = nullptr;

    // White 1x1 texture for untextured geometry
    ID3D11ShaderResourceView* m_whiteTexture = nullptr;

    // State
    bool m_scissorEnabled = false;
    int m_viewportWidth = 1920;
    int m_viewportHeight = 1080;

    // Current transform (identity by default)
    bool m_hasTransform = false;
    float m_transformMatrix[16] = {};

    // Handle counters
    uintptr_t m_nextGeometryHandle = 1;
    uintptr_t m_nextTextureHandle = 1;

    // Handle maps
    std::unordered_map<uintptr_t, CompiledGeometry> m_geometries;
    std::unordered_map<uintptr_t, TextureData> m_textures;
};
