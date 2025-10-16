using System;
using System.Threading;

namespace SkiaSharp;

public enum GRBackend
{
    Unknown = 0,
    OpenGL = 1,
    Vulkan = 2,
    Metal = 3,
    Direct3D = 4,
}

[Flags]
public enum GRBackendState
{
    None = 0,
    All = -1,
}

[Flags]
public enum GRGlBackendState
{
    None = 0,
    All = -1,
}

public enum GRSurfaceOrigin
{
    BottomLeft = 0,
    TopLeft = 1,
}

public sealed class GRContextOptions
{
    public bool AvoidStencilBuffers { get; set; }
}

public sealed class GRGlInterface : IDisposable
{
    private readonly bool _isGles;
    private bool _disposed;

    private GRGlInterface(bool isGles)
    {
        _isGles = isGles;
    }

    public bool IsGles => _isGles;

    public static GRGlInterface Create() => new(false);

    public static GRGlInterface CreateOpenGl(Func<string, IntPtr>? getProcAddress) => new(false);

    public static GRGlInterface CreateGles(Func<string, IntPtr>? getProcAddress) => new(true);

    public void Dispose()
    {
        _disposed = true;
    }
}

internal enum GrContextBackend
{
    Unspecified,
    OpenGl,
    Vulkan,
    Metal,
    Direct3D,
}

public sealed class GRContext : IDisposable
{
    private static long s_nextHandle = 1;

    private readonly object _sync = new();
    private readonly GrContextBackend _backend;
    private bool _disposed;
    private bool _abandoned;
    private long _resourceLimit = long.MaxValue;
    private long _resourceUsage;

    private GRContext(GrContextBackend backend, GRContextOptions? options)
    {
        _backend = backend;
        Options = options ?? new GRContextOptions();
        Handle = new IntPtr(Interlocked.Increment(ref s_nextHandle));
    }

    internal GRContextOptions Options { get; }

    public IntPtr Handle { get; private set; }

    public GRBackend Backend => _backend switch
    {
        GrContextBackend.OpenGl => GRBackend.OpenGL,
        GrContextBackend.Vulkan => GRBackend.Vulkan,
        GrContextBackend.Metal => GRBackend.Metal,
        GrContextBackend.Direct3D => GRBackend.Direct3D,
        _ => GRBackend.Unknown,
    };

    public bool IsAbandoned => _abandoned || _disposed;

    public static GRContext CreateGl() => CreateGl(null, null);

    public static GRContext CreateGl(GRGlInterface? backendContext) => CreateGl(backendContext, null);

    public static GRContext CreateGl(GRContextOptions? options) => CreateGl(null, options);

    public static GRContext CreateGl(GRGlInterface? backendContext, GRContextOptions? options)
    {
        _ = backendContext;
        return new GRContext(GrContextBackend.OpenGl, options);
    }

    public static GRContext CreateVulkan(GRVkBackendContext backendContext) => CreateVulkan(backendContext, null);

    public static GRContext CreateVulkan(GRVkBackendContext backendContext, GRContextOptions? options)
    {
        if (backendContext.VkDevice == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(backendContext));
        }

        return new GRContext(GrContextBackend.Vulkan, options);
    }

    public static GRContext CreateMetal(GRMtlBackendContext backendContext) => CreateMetal(backendContext, null);

    public static GRContext CreateMetal(GRMtlBackendContext backendContext, GRContextOptions? options)
    {
        if (backendContext.DeviceHandle == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(backendContext));
        }

        return new GRContext(GrContextBackend.Metal, options);
    }

    public static GRContext CreateDirect3D(GRD3DBackendContext backendContext) => CreateDirect3D(backendContext, null);

    public static GRContext CreateDirect3D(GRD3DBackendContext backendContext, GRContextOptions? options)
    {
        if (backendContext.Device == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(backendContext));
        }

        return new GRContext(GrContextBackend.Direct3D, options);
    }

    public void AbandonContext(bool releaseResources = false)
    {
        _abandoned = true;
        if (releaseResources)
        {
            _resourceUsage = 0;
        }
    }

    public void ResetContext()
    {
    }

    public void ResetContext(GRBackendState state)
    {
        _ = state;
    }

    public void ResetContext(GRGlBackendState state)
    {
        _ = state;
    }

    public void ResetContext(uint state)
    {
        _ = state;
    }

    public void Flush() => Flush(true);

    public void Flush(bool submit, bool synchronous = false)
    {
        _ = submit;
        _ = synchronous;
    }

    public void Submit(bool synchronous = false)
    {
        _ = synchronous;
    }

    public int GetMaxSurfaceSampleCount(SKColorType colorType)
    {
        _ = colorType;
        return 1;
    }

    public long GetResourceCacheLimit() => _resourceLimit;

    public void SetResourceCacheLimit(long maxResourceBytes) => _resourceLimit = maxResourceBytes;

    public void GetResourceCacheUsage(out int maxResources, out long maxResourceBytes)
    {
        maxResources = 0;
        maxResourceBytes = _resourceUsage;
    }

    public void PurgeResources()
    {
        _resourceUsage = 0;
    }

    public void PurgeUnusedResources(long milliseconds)
    {
        _ = milliseconds;
    }

    public void PurgeUnlockedResources(bool scratchResourcesOnly)
    {
        _ = scratchResourcesOnly;
    }

    public void PurgeUnlockedResources(long bytesToPurge, bool preferScratchResources)
    {
        _ = bytesToPurge;
        _ = preferScratchResources;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Handle = IntPtr.Zero;
        _resourceUsage = 0;
        GC.SuppressFinalize(this);
    }

    ~GRContext()
    {
        Dispose();
    }
}

public readonly struct GRGlFramebufferInfo
{
    public GRGlFramebufferInfo(uint framebufferObjectId, uint format)
    {
        FramebufferObjectId = framebufferObjectId;
        Format = format;
    }

    public uint FramebufferObjectId { get; }
    public uint Format { get; }
}

public readonly struct GRGlTextureInfo
{
    public GRGlTextureInfo(uint target, uint id, uint format = 0)
    {
        Target = target;
        Id = id;
        Format = format;
    }

    public uint Target { get; }
    public uint Id { get; }
    public uint Format { get; }
}

public readonly struct GRVkAlloc
{
    public ulong Memory { get; init; }
    public ulong Offset { get; init; }
    public ulong Size { get; init; }
    public uint Flags { get; init; }
}

public readonly struct GRVkImageInfo
{
    public ulong Image { get; init; }
    public ulong ImageTiling { get; init; }
    public ulong ImageUsageFlags { get; init; }
    public ulong Format { get; init; }
    public ulong ImageLayout { get; init; }
    public int SampleCount { get; init; }
    public int LevelCount { get; init; }
    public bool Protected { get; init; }
    public uint CurrentQueueFamily { get; init; }
    public GRVkAlloc Alloc { get; init; }
}

public readonly struct GRVkBackendContext
{
    public IntPtr VkInstance { get; init; }
    public IntPtr VkPhysicalDevice { get; init; }
    public IntPtr VkDevice { get; init; }
    public IntPtr VkQueue { get; init; }
    public uint GraphicsQueueIndex { get; init; }
    public Func<string, IntPtr, IntPtr, IntPtr>? GetProcedureAddress { get; init; }
}

public readonly struct GRD3DBackendContext
{
    public IntPtr Device { get; init; }
    public IntPtr Adapter { get; init; }
    public IntPtr Queue { get; init; }
}

public readonly struct GRMtlTextureInfo
{
    public IntPtr TextureHandle { get; init; }
    public IntPtr Format { get; init; }
    public int SampleCount { get; init; }
    public int LevelCount { get; init; }
    public bool IsProtected { get; init; }
}

public readonly struct GRMtlBackendContext
{
    public IntPtr DeviceHandle { get; init; }
    public IntPtr QueueHandle { get; init; }
}

public sealed class GRBackendRenderTarget : IDisposable
{
    private readonly GRGlFramebufferInfo? _glInfo;
    private readonly GRVkImageInfo? _vkInfo;
    private readonly GRMtlTextureInfo? _mtlInfo;
    private bool _disposed;

    public GRBackendRenderTarget(int width, int height, int sampleCount, int stencilBits, GRGlFramebufferInfo framebufferInfo)
    {
        Width = width;
        Height = height;
        Samples = sampleCount;
        Stencil = stencilBits;
        _glInfo = framebufferInfo;
    }

    public GRBackendRenderTarget(int width, int height, GRVkImageInfo imageInfo)
    {
        Width = width;
        Height = height;
        _vkInfo = imageInfo;
    }

    public GRBackendRenderTarget(int width, int height, GRMtlTextureInfo textureInfo)
    {
        Width = width;
        Height = height;
        _mtlInfo = textureInfo;
    }

    public int Width { get; }
    public int Height { get; }
    public int Samples { get; }
    public int Stencil { get; }

    public GRGlFramebufferInfo? GlInfo => _glInfo;
    public GRVkImageInfo? VkInfo => _vkInfo;
    public GRMtlTextureInfo? MetalInfo => _mtlInfo;

    public void Dispose()
    {
        _disposed = true;
    }
}

public sealed class GRBackendTexture : IDisposable
{
    private readonly GRGlTextureInfo? _glInfo;
    private readonly GRVkImageInfo? _vkInfo;
    private readonly GRMtlTextureInfo? _mtlInfo;
    private bool _disposed;

    public GRBackendTexture(int width, int height, bool mipmapped, GRGlTextureInfo textureInfo)
    {
        Width = width;
        Height = height;
        Mipmapped = mipmapped;
        _glInfo = textureInfo;
    }

    public GRBackendTexture(int width, int height, bool mipmapped, GRVkImageInfo imageInfo)
    {
        Width = width;
        Height = height;
        Mipmapped = mipmapped;
        _vkInfo = imageInfo;
    }

    public GRBackendTexture(int width, int height, bool mipmapped, GRMtlTextureInfo textureInfo)
    {
        Width = width;
        Height = height;
        Mipmapped = mipmapped;
        _mtlInfo = textureInfo;
    }

    public int Width { get; }
    public int Height { get; }
    public bool Mipmapped { get; }

    public bool IsValid => !_disposed;

    public GRGlTextureInfo? GlInfo => _glInfo;
    public GRVkImageInfo? VkInfo => _vkInfo;
    public GRMtlTextureInfo? MetalInfo => _mtlInfo;

    public void Dispose()
    {
        _disposed = true;
    }
}
