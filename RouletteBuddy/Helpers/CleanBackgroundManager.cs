using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using SharpGen.Runtime;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RouletteBuddy.Helpers;

public sealed unsafe class CleanBackgroundManager(IPluginLog log) : IDisposable
{
    private ID3D11Device? device;
    private ID3D11DeviceContext? context;

    private ID3D11Texture2D? capturedTexture;
    private ID3D11Texture2D? outputTexture;
    private ID3D11Texture2D? blurTempTexture;
    private ID3D11ShaderResourceView? capturedSrv;
    private ID3D11ShaderResourceView? outputSrv;
    private ID3D11ShaderResourceView? blurTempSrv;
    private ID3D11UnorderedAccessView? outputUav;
    private ID3D11UnorderedAccessView? blurTempUav;

    private ID3D11ComputeShader? alphaFixShader;
    private ID3D11ComputeShader? blurHorizontalShader;
    private ID3D11ComputeShader? blurVerticalShader;

    private bool resourcesReady;
    private int lastFrameCount = -1;
    private bool hasAttemptedInit;

    public int BlurIterations { get; set; } = 3;

    public void Initialize()
    {
        if (hasAttemptedInit)
        {
            return;
        }

        hasAttemptedInit = true;
        UpdateDevice();
    }

    public void DrawBackground(float opacity = 0.8f)
    {
        if (!UpdateDevice())
        {
            log.Debug("DirectX11 设备未就绪");
            return;
        }

        if (alphaFixShader == null)
        {
            log.Debug("便签磨砂背景着色器未加载");
            return;
        }

        try
        {
            var currentFrame = ImGui.GetFrameCount();
            if (lastFrameCount != currentFrame)
            {
                CaptureAndBlur();
                lastFrameCount = currentFrame;
            }

            if (!resourcesReady || outputSrv == null)
            {
                log.Debug("便签磨砂背景资源未就绪或输出 SRV 为空");
                return;
            }

            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();
            var displaySize = ImGui.GetIO().DisplaySize;
            if (windowSize.X <= 0f || windowSize.Y <= 0f || displaySize.X <= 0f || displaySize.Y <= 0f)
            {
                return;
            }

            var uv0 = new Vector2(windowPos.X / displaySize.X, windowPos.Y / displaySize.Y);
            var uv1 = new Vector2(
                (windowPos.X + windowSize.X) / displaySize.X,
                (windowPos.Y + windowSize.Y) / displaySize.Y
            );

            var drawList = ImGui.GetBackgroundDrawList();
            drawList.AddImage(
                new ImTextureID(outputSrv.NativePointer),
                windowPos,
                windowPos + windowSize,
                uv0,
                uv1,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f))
            );

            var overlayColor = ImGui.ColorConvertFloat4ToU32(
                new Vector4(0.10f, 0.12f, 0.16f, Math.Clamp(opacity, 0f, 1f))
            );
            drawList.AddRectFilled(windowPos, windowPos + windowSize, overlayColor);
        }
        catch (Exception ex)
        {
            log.Error(ex, "便签磨砂背景绘制失败");
        }
    }

    private bool UpdateDevice()
    {
        if (device != null && context != null)
        {
            return true;
        }

        try
        {
            var deviceInstance = Device.Instance();
            if (deviceInstance == null)
            {
                log.Debug("Device.Instance() 返回 null");
                return false;
            }

            var nativeContext = deviceInstance->D3D11DeviceContext;
            if (nativeContext == null)
            {
                log.Debug("D3D11DeviceContext 为 null");
                return false;
            }

            context = new ID3D11DeviceContext((IntPtr)nativeContext);
            context.AddRef();
            device = context.Device;

            LoadShaders();

            log.Information("DirectX11 设备成功获取，便签磨砂背景已初始化");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "DirectX11 设备获取失败");
            DisposeDeviceObjects();
            return false;
        }
    }

    private void LoadShaders()
    {
        log.Information("开始加载便签磨砂背景着色器...");
        alphaFixShader = LoadShaderFromResource("RouletteBuddy.Shaders.AlphaFix.cso");
        blurHorizontalShader = LoadShaderFromResource("RouletteBuddy.Shaders.HBlur.cso");
        blurVerticalShader = LoadShaderFromResource("RouletteBuddy.Shaders.VBlur.cso");

        if (alphaFixShader != null)
        {
            log.Information("AlphaFix 着色器加载成功");
        }

        if (blurHorizontalShader != null)
        {
            log.Information("HBlur 着色器加载成功");
        }

        if (blurVerticalShader != null)
        {
            log.Information("VBlur 着色器加载成功");
        }
    }

    private ID3D11ComputeShader? LoadShaderFromResource(string resourceName)
    {
        try
        {
            if (device == null)
            {
                return null;
            }

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                log.Error($"找不到对应路径的嵌入式资源文件: {resourceName}");
                return null;
            }

            var bytecode = new byte[stream.Length];
            stream.ReadExactly(bytecode);
            return device.CreateComputeShader(bytecode, null);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"着色器加载失败: {resourceName}");
            return null;
        }
    }

    private void CaptureAndBlur()
    {
        if (context == null || device == null)
        {
            return;
        }

        var deviceInstance = Device.Instance();
        if (deviceInstance == null || deviceInstance->SwapChain == null)
        {
            return;
        }

        var nativeSwapChain = deviceInstance->SwapChain->DXGISwapChain;
        if (nativeSwapChain == null)
        {
            return;
        }

        using var swapChain = new IDXGISwapChain((IntPtr)nativeSwapChain);
        swapChain.AddRef();

        using var backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        var desc = backBuffer.Description;
        var currentDesc = capturedTexture?.Description;
        if (
            capturedTexture == null
            || currentDesc == null
            || currentDesc.Value.Width != desc.Width
            || currentDesc.Value.Height != desc.Height
        )
        {
            if (!ResizeResources(desc))
            {
                return;
            }
        }

        if (capturedTexture == null)
        {
            return;
        }

        context.CopyResource(capturedTexture, backBuffer);
        RunComputeShader(alphaFixShader, capturedSrv, outputUav, desc.Width, desc.Height);

        if (blurHorizontalShader == null || blurVerticalShader == null)
        {
            return;
        }

        for (var i = 0; i < Math.Clamp(BlurIterations, 1, 8); i++)
        {
            RunComputeShader(blurHorizontalShader, outputSrv, blurTempUav, desc.Width, desc.Height);
            RunComputeShader(blurVerticalShader, blurTempSrv, outputUav, desc.Width, desc.Height);
        }
    }

    private void RunComputeShader(
        ID3D11ComputeShader? shader,
        ID3D11ShaderResourceView? input,
        ID3D11UnorderedAccessView? output,
        uint width,
        uint height
    )
    {
        if (shader == null || input == null || output == null || context == null)
        {
            return;
        }

        context.CSSetShader(shader);
        context.CSSetShaderResource(0, input);
        context.CSSetUnorderedAccessView(0, output, 0u);

        var dispatchX = (width + 7) / 8;
        var dispatchY = (height + 7) / 8;
        context.Dispatch(dispatchX, dispatchY, 1);

        context.CSSetShaderResource(0, null);
        context.CSSetUnorderedAccessView(0, null, 0u);
        context.CSSetShader(null);
    }

    private bool ResizeResources(Texture2DDescription backBufferDesc)
    {
        DisposeTextures();

        try
        {
            if (device == null)
            {
                return false;
            }

            var format = NormalizeFormat(backBufferDesc.Format);
            var textureDesc = new Texture2DDescription
            {
                Width = backBufferDesc.Width,
                Height = backBufferDesc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None,
            };

            var inputDesc = textureDesc;
            inputDesc.BindFlags = BindFlags.ShaderResource;

            capturedTexture = device.CreateTexture2D(in inputDesc);
            capturedSrv = device.CreateShaderResourceView(capturedTexture, null);

            outputTexture = device.CreateTexture2D(in textureDesc);
            outputUav = device.CreateUnorderedAccessView(outputTexture, null);
            outputSrv = device.CreateShaderResourceView(outputTexture, null);

            blurTempTexture = device.CreateTexture2D(in textureDesc);
            blurTempUav = device.CreateUnorderedAccessView(blurTempTexture, null);
            blurTempSrv = device.CreateShaderResourceView(blurTempTexture, null);

            resourcesReady = true;
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "便签磨砂背景 D3D11 资源创建失败");
            DisposeTextures();
            return false;
        }
    }

    private static Format NormalizeFormat(Format format)
    {
        return format switch
        {
            Format.R8G8B8A8_UNorm_SRgb => Format.R8G8B8A8_UNorm,
            Format.B8G8R8A8_UNorm_SRgb => Format.B8G8R8A8_UNorm,
            _ => format,
        };
    }

    private void DisposeTextures()
    {
        resourcesReady = false;
        DisposeAndNull(ref capturedSrv);
        DisposeAndNull(ref capturedTexture);
        DisposeAndNull(ref outputSrv);
        DisposeAndNull(ref outputUav);
        DisposeAndNull(ref outputTexture);
        DisposeAndNull(ref blurTempSrv);
        DisposeAndNull(ref blurTempUav);
        DisposeAndNull(ref blurTempTexture);
    }

    private void DisposeDeviceObjects()
    {
        DisposeTextures();
        DisposeAndNull(ref alphaFixShader);
        DisposeAndNull(ref blurHorizontalShader);
        DisposeAndNull(ref blurVerticalShader);
        DisposeAndNull(ref device);
        DisposeAndNull(ref context);
    }

    private static void DisposeAndNull<T>(ref T? resource)
        where T : ComObject
    {
        resource?.Dispose();
        resource = null;
    }

    public void Dispose()
    {
        DisposeDeviceObjects();
    }
}
