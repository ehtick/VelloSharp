using System;

namespace VelloSharp.Avalonia.Browser;

internal sealed class VelloBrowserSurfaceTexture : IDisposable
{
    private readonly VelloBrowserSwapChainSurface _surface;
    private uint _textureHandle;
    private bool _presented;
    private bool _disposed;

    internal VelloBrowserSurfaceTexture(VelloBrowserSwapChainSurface surface, uint textureHandle)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        if (textureHandle == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textureHandle), "Texture handle must be a non-zero value.");
        }

        _textureHandle = textureHandle;
    }

    public uint TextureHandle
    {
        get
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VelloBrowserSurfaceTexture));
            }

            if (_textureHandle == 0)
            {
                throw new InvalidOperationException("The surface texture handle is no longer valid.");
            }

            return _textureHandle;
        }
    }

    public bool IsPresented => _presented;

    public void Present()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloBrowserSurfaceTexture));
        }

        if (_presented)
        {
            throw new InvalidOperationException("The surface texture has already been presented.");
        }

        var handle = _textureHandle;
        if (handle == 0)
        {
            throw new InvalidOperationException("The surface texture handle is no longer valid.");
        }

        _surface.OnTexturePresented(handle);
        _presented = true;
        _textureHandle = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_presented && _textureHandle != 0)
        {
            _surface.OnTextureReleased(_textureHandle);
            _textureHandle = 0;
        }

        _disposed = true;
    }
}
