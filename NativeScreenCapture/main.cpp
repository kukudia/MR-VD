#include "pch.h"
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "windowsapp.lib")

struct __declspec(uuid("3702167f-3382-4148-8c37-a961ee0d44f2"))
    IDirect3DDxgiInterfaceAccess : ::IUnknown
{
    virtual HRESULT __stdcall GetInterface(GUID const& id, void** object) = 0;
};

struct CaptureContext
{
    ID3D11Device* d3dDevice = nullptr;
    ID3D11DeviceContext* d3dContext = nullptr;
    ID3D11Texture2D* stagingTexture = nullptr; // 用于CPU读取的中间纹理

    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice winrtDevice{ nullptr };
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool framePool{ nullptr };
    winrt::Windows::Graphics::Capture::GraphicsCaptureSession session{ nullptr };

    int width = 0;
    int height = 0;
    bool capturing = false;
};

static CaptureContext g_ctx;
static bool g_winrtInitialized = false;

static void EnsureWinRT()
{
    if (!g_winrtInitialized)
    {
        winrt::init_apartment(winrt::apartment_type::multi_threaded);
        g_winrtInitialized = true;
    }
}

static winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice
CreateWinRTDevice(ID3D11Device* d3dDevice)
{
    winrt::com_ptr<IDXGIDevice> dxgiDevice;
    winrt::check_hresult(
        d3dDevice->QueryInterface(IID_PPV_ARGS(dxgiDevice.put()))
    );

    winrt::com_ptr<IInspectable> inspectable;
    winrt::check_hresult(
        CreateDirect3D11DeviceFromDXGIDevice(
            dxgiDevice.get(),
            inspectable.put()
        )
    );

    return inspectable.as<
        winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice>();
}

static winrt::Windows::Graphics::Capture::GraphicsCaptureItem
CreateItemForWindow(HWND hwnd)
{
    auto factory = winrt::get_activation_factory<
        winrt::Windows::Graphics::Capture::GraphicsCaptureItem,
        IGraphicsCaptureItemInterop>();

    winrt::Windows::Graphics::Capture::GraphicsCaptureItem item{ nullptr };
    winrt::check_hresult(
        factory->CreateForWindow(
            hwnd,
            winrt::guid_of<winrt::Windows::Graphics::Capture::GraphicsCaptureItem>(),
            winrt::put_abi(item)
        )
    );

    return item;
}

extern "C" __declspec(dllexport)
bool StartCapture(HWND hwnd)
{
    try
    {
        EnsureWinRT();

        if (!hwnd || !IsWindow(hwnd)) return false;

        // 清理旧的资源
        if (g_ctx.stagingTexture) {
            g_ctx.stagingTexture->Release();
            g_ctx.stagingTexture = nullptr;
        }

        // 创建 D3D11 设备
        HRESULT hr = D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            nullptr, 0,
            D3D11_SDK_VERSION,
            &g_ctx.d3dDevice,
            nullptr,
            &g_ctx.d3dContext
        );

        if (FAILED(hr)) return false;

        // 创建 WinRT D3D11 Device
        g_ctx.winrtDevice = CreateWinRTDevice(g_ctx.d3dDevice);

        // Capture Item
        auto item = CreateItemForWindow(hwnd);
        auto size = item.Size();

        g_ctx.width = size.Width;
        g_ctx.height = size.Height;

        // FramePool + Session
        g_ctx.framePool =
            winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool::Create(
                g_ctx.winrtDevice,
                winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,
                2,
                size
            );

        g_ctx.session = g_ctx.framePool.CreateCaptureSession(item);
        g_ctx.session.StartCapture();

        g_ctx.capturing = true;
        return true;
    }
    catch (...)
    {
        return false;
    }
}

extern "C" __declspec(dllexport)
bool CopyFrameToBuffer(void* destBuffer, int destWidth, int destHeight)
{
    if (!g_ctx.capturing || !g_ctx.framePool || !destBuffer)
        return false;

    try
    {
        auto frame = g_ctx.framePool.TryGetNextFrame();
        if (!frame)
            return false;

        auto surface = frame.Surface();
        auto access = surface.as<::IDirect3DDxgiInterfaceAccess>();

        ID3D11Texture2D* capturedTex = nullptr;
        HRESULT hr = access->GetInterface(
            __uuidof(ID3D11Texture2D),
            reinterpret_cast<void**>(&capturedTex)
        );

        if (FAILED(hr))
            return false;

        // 创建 staging texture (如果还没有)
        if (!g_ctx.stagingTexture)
        {
            D3D11_TEXTURE2D_DESC desc = {};
            capturedTex->GetDesc(&desc);

            desc.Usage = D3D11_USAGE_STAGING;
            desc.BindFlags = 0;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            desc.MiscFlags = 0;

            hr = g_ctx.d3dDevice->CreateTexture2D(&desc, nullptr, &g_ctx.stagingTexture);
            if (FAILED(hr))
            {
                capturedTex->Release();
                return false;
            }
        }

        // 复制到 staging texture
        g_ctx.d3dContext->CopyResource(g_ctx.stagingTexture, capturedTex);
        capturedTex->Release();

        // 映射并读取数据
        D3D11_MAPPED_SUBRESOURCE mapped;
        hr = g_ctx.d3dContext->Map(g_ctx.stagingTexture, 0, D3D11_MAP_READ, 0, &mapped);
        if (FAILED(hr))
            return false;

        // 复制到目标缓冲区
        int copyWidth = min(destWidth, g_ctx.width);
        int copyHeight = min(destHeight, g_ctx.height);

        for (int y = 0; y < copyHeight; y++)
        {
            memcpy(
                (BYTE*)destBuffer + y * destWidth * 4,
                (BYTE*)mapped.pData + y * mapped.RowPitch,
                copyWidth * 4
            );
        }

        g_ctx.d3dContext->Unmap(g_ctx.stagingTexture, 0);
        return true;
    }
    catch (...)
    {
        return false;
    }
}

extern "C" __declspec(dllexport)
void GetCaptureSize(int* width, int* height)
{
    if (width) *width = g_ctx.width;
    if (height) *height = g_ctx.height;
}

extern "C" __declspec(dllexport)
void StopCapture()
{
    g_ctx.capturing = false;

    // 先停止会话
    if (g_ctx.session)
    {
        try {
            g_ctx.session.Close();
        }
        catch (...) {}
        g_ctx.session = nullptr;
    }

    // 然后关闭帧池
    if (g_ctx.framePool)
    {
        try {
            g_ctx.framePool.Close();
        }
        catch (...) {}
        g_ctx.framePool = nullptr;
    }

    // 清理 WinRT 设备
    g_ctx.winrtDevice = nullptr;

    // 清理 D3D 资源
    if (g_ctx.stagingTexture)
    {
        g_ctx.stagingTexture->Release();
        g_ctx.stagingTexture = nullptr;
    }

    if (g_ctx.d3dContext)
    {
        g_ctx.d3dContext->Release();
        g_ctx.d3dContext = nullptr;
    }

    if (g_ctx.d3dDevice)
    {
        g_ctx.d3dDevice->Release();
        g_ctx.d3dDevice = nullptr;
    }
}