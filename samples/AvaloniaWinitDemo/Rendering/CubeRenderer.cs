using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaWinitDemo.Rendering;

internal sealed class CubeRenderer : IDisposable
{
    private const uint TextureSize = 256;

    private static readonly float[] VertexData =
    {
        // top
        -1f, -1f, 1f, 1f, 0f, 0f,
        1f, -1f, 1f, 1f, 1f, 0f,
        1f, 1f, 1f, 1f, 1f, 1f,
        -1f, 1f, 1f, 1f, 0f, 1f,
        // bottom
        -1f, 1f, -1f, 1f, 1f, 0f,
        1f, 1f, -1f, 1f, 0f, 0f,
        1f, -1f, -1f, 1f, 0f, 1f,
        -1f, -1f, -1f, 1f, 1f, 1f,
        // right
        1f, -1f, -1f, 1f, 0f, 0f,
        1f, 1f, -1f, 1f, 1f, 0f,
        1f, 1f, 1f, 1f, 1f, 1f,
        1f, -1f, 1f, 1f, 0f, 1f,
        // left
        -1f, -1f, 1f, 1f, 1f, 0f,
        -1f, 1f, 1f, 1f, 0f, 0f,
        -1f, 1f, -1f, 1f, 0f, 1f,
        -1f, -1f, -1f, 1f, 1f, 1f,
        // front
        1f, 1f, -1f, 1f, 1f, 0f,
        -1f, 1f, -1f, 1f, 0f, 0f,
        -1f, 1f, 1f, 1f, 0f, 1f,
        1f, 1f, 1f, 1f, 1f, 1f,
        // back
        1f, -1f, 1f, 1f, 0f, 0f,
        -1f, -1f, 1f, 1f, 1f, 0f,
        -1f, -1f, -1f, 1f, 1f, 1f,
        1f, -1f, -1f, 1f, 0f, 1f,
    };

    private static readonly ushort[] IndexData =
    {
        0, 1, 2, 2, 3, 0,
        4, 5, 6, 6, 7, 4,
        8, 9, 10, 10, 11, 8,
        12, 13, 14, 14, 15, 12,
        16, 17, 18, 18, 19, 16,
        20, 21, 22, 22, 23, 20,
    };

    private static readonly byte[] TextureData = CreateTexels((int)TextureSize);

    private static byte[] CreateTexels(int size)
    {
        var texels = new byte[size * size];
        for (int i = 0; i < texels.Length; i++)
        {
            var cx = 3f * (i % size) / (size - 1f) - 2f;
            var cy = 2f * (i / size) / (size - 1f) - 1f;
            var x = cx;
            var y = cy;
            byte count = 0;
            while (count < 0xFF && x * x + y * y < 4f)
            {
                var oldX = x;
                x = x * x - y * y + cx;
                y = 2f * oldX * y + cy;
                count++;
            }

            texels[i] = count;
        }

        return texels;
    }

    private const string CubeShaderSource = """
struct VertexOutput {
    @location(0) tex_coord: vec2<f32>,
    @builtin(position) position: vec4<f32>,
};

@group(0)
@binding(0)
var<uniform> transform: mat4x4<f32>;

@vertex
fn vs_main(
    @location(0) position: vec4<f32>,
    @location(1) tex_coord: vec2<f32>,
) -> VertexOutput {
    var result: VertexOutput;
    result.tex_coord = tex_coord;
    result.position = transform * position;
    return result;
}

@group(0)
@binding(1)
var r_color: texture_2d<u32>;

@fragment
fn fs_main(vertex: VertexOutput) -> @location(0) vec4<f32> {
    let tex = textureLoad(r_color, vec2<i32>(vertex.tex_coord * 256.0), 0);
    let v = f32(tex.x) / 255.0;
    return vec4<f32>(1.0 - (v * 5.0), 1.0 - (v * 15.0), 1.0 - (v * 50.0), 1.0);
}

@fragment
fn fs_wire(vertex: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(0.0, 0.5, 0.0, 0.5);
}
""";

    private WgpuDevice? _device;
    private WgpuTextureFormat _surfaceFormat;
    private WgpuShaderModule? _shaderModule;
    private WgpuBuffer? _vertexBuffer;
    private WgpuBuffer? _indexBuffer;
    private WgpuBuffer? _uniformBuffer;
    private WgpuTexture? _texture;
    private WgpuTextureView? _textureView;
    private WgpuBindGroupLayout? _bindGroupLayout;
    private WgpuPipelineLayout? _pipelineLayout;
    private WgpuBindGroup? _bindGroup;
    private WgpuRenderPipeline? _pipeline;
    private int _indexCount;
    private bool _disposed;

    public bool Render(WgpuSurfaceRenderContext context, float timeSeconds, Rect viewportRect)
    {
        if (context.RenderParams.Width == 0 || context.RenderParams.Height == 0)
        {
            return false;
        }

        if (viewportRect.Width <= 0 || viewportRect.Height <= 0)
        {
            return false;
        }

        if (!EnsureResources(context.Device, context.Queue, context.SurfaceFormat))
        {
            return false;
        }

        UpdateUniformBuffer(context.Queue, viewportRect, timeSeconds);
        EncodeCommands(context.Device, context.Queue, context.TargetView, context.RenderParams, viewportRect);
        return true;
    }

    public void Reset()
    {
        DisposeResource(ref _pipeline);
        DisposeResource(ref _bindGroup);
        DisposeResource(ref _pipelineLayout);
        DisposeResource(ref _bindGroupLayout);
        DisposeResource(ref _textureView);
        DisposeResource(ref _texture);
        DisposeResource(ref _uniformBuffer);
        DisposeResource(ref _indexBuffer);
        DisposeResource(ref _vertexBuffer);
        DisposeResource(ref _shaderModule);
        _device = null;
        _surfaceFormat = default;
        _indexCount = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Reset();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private bool EnsureResources(WgpuDevice device, WgpuQueue queue, WgpuTextureFormat surfaceFormat)
    {
        if (_device is null || !ReferenceEquals(_device, device))
        {
            Reset();
            _device = device;
            _surfaceFormat = surfaceFormat;
            CreateStaticResources(device, queue);
            CreatePipeline(device, surfaceFormat);
            return _pipeline is not null;
        }

        if (_surfaceFormat != surfaceFormat)
        {
            _surfaceFormat = surfaceFormat;
            DisposeResource(ref _pipeline);
            CreatePipeline(device, surfaceFormat);
        }

        if (_pipeline is null)
        {
            CreatePipeline(device, surfaceFormat);
        }

        return _pipeline is not null;
    }

    private void CreateStaticResources(WgpuDevice device, WgpuQueue queue)
    {
        _shaderModule = device.CreateShaderModule(new WgpuShaderModuleDescriptor(CubeShaderSource, "CubeShader"));

        _vertexBuffer = device.CreateBuffer(new WgpuBufferDescriptor
        {
            Label = "CubeVertexBuffer",
            Usage = WgpuBufferUsage.Vertex | WgpuBufferUsage.CopyDst,
            Size = (ulong)(VertexData.Length * sizeof(float)),
            MappedAtCreation = false,
        });
        queue.WriteBuffer(_vertexBuffer, 0, MemoryMarshal.AsBytes<float>(VertexData.AsSpan()));

        _indexBuffer = device.CreateBuffer(new WgpuBufferDescriptor
        {
            Label = "CubeIndexBuffer",
            Usage = WgpuBufferUsage.Index | WgpuBufferUsage.CopyDst,
            Size = (ulong)(IndexData.Length * sizeof(ushort)),
            MappedAtCreation = false,
        });
        queue.WriteBuffer(_indexBuffer, 0, MemoryMarshal.AsBytes(IndexData.AsSpan()));

        _uniformBuffer = device.CreateBuffer(new WgpuBufferDescriptor
        {
            Label = "CubeUniformBuffer",
            Usage = WgpuBufferUsage.Uniform | WgpuBufferUsage.CopyDst,
            Size = 64,
            MappedAtCreation = false,
        });

        _texture = device.CreateTexture(new WgpuTextureDescriptor
        {
            Label = "CubeTexture",
            Size = new WgpuExtent3D
            {
                Width = TextureSize,
                Height = TextureSize,
                DepthOrArrayLayers = 1,
            },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WgpuTextureDimension.D2,
            Format = WgpuTextureFormat.R8Uint,
            Usage = WgpuTextureUsage.TextureBinding | WgpuTextureUsage.CopyDst,
        });
        _textureView = _texture.CreateView();

        queue.WriteTexture(
            new WgpuImageCopyTexture
            {
                Texture = _texture,
                MipLevel = 0,
                Origin = new WgpuOrigin3D { X = 0, Y = 0, Z = 0 },
                Aspect = WgpuTextureAspect.All,
            },
            TextureData,
            new WgpuTextureDataLayout
            {
                Offset = 0,
                BytesPerRow = TextureSize,
                RowsPerImage = TextureSize,
            },
            new WgpuExtent3D
            {
                Width = TextureSize,
                Height = TextureSize,
                DepthOrArrayLayers = 1,
            });

        _bindGroupLayout = device.CreateBindGroupLayout(new WgpuBindGroupLayoutDescriptor
        {
            Label = "CubeBindGroupLayout",
            Entries = new[]
            {
                new WgpuBindGroupLayoutEntry(
                    0,
                    WgpuShaderStage.Vertex,
                    new WgpuBufferBindingLayout
                    {
                        Type = WgpuBufferBindingType.Uniform,
                        HasDynamicOffset = false,
                        MinBindingSize = 64,
                    }),
                new WgpuBindGroupLayoutEntry(
                    1,
                    WgpuShaderStage.Fragment,
                    new WgpuTextureBindingLayout
                    {
                        SampleType = WgpuTextureSampleType.Uint,
                        Dimension = WgpuTextureViewDimension.D2,
                        Multisampled = false,
                    }),
            },
        });

        _pipelineLayout = device.CreatePipelineLayout(new WgpuPipelineLayoutDescriptor
        {
            Label = "CubePipelineLayout",
            BindGroupLayouts = new[] { _bindGroupLayout! },
        });

        _bindGroup = device.CreateBindGroup(new WgpuBindGroupDescriptor
        {
            Label = "CubeBindGroup",
            Layout = _bindGroupLayout!,
            Entries = new[]
            {
                WgpuBindGroupEntry.CreateBuffer(0, new WgpuBufferBinding
                {
                    Buffer = _uniformBuffer!,
                    Offset = 0,
                    Size = 64,
                }),
                WgpuBindGroupEntry.CreateTextureView(1, _textureView!),
            },
        });

        _indexCount = IndexData.Length;
    }

    private void CreatePipeline(WgpuDevice device, WgpuTextureFormat surfaceFormat)
    {
        if (_shaderModule is null || _pipelineLayout is null)
        {
            return;
        }

        var vertexLayout = new WgpuVertexBufferLayout
        {
            ArrayStride = (ulong)(6 * sizeof(float)),
            StepMode = WgpuVertexStepMode.Vertex,
            Attributes = new[]
            {
                new WgpuVertexAttribute
                {
                    Format = WgpuVertexFormat.Float32x4,
                    Offset = 0,
                    ShaderLocation = 0,
                },
                new WgpuVertexAttribute
                {
                    Format = WgpuVertexFormat.Float32x2,
                    Offset = (ulong)(4 * sizeof(float)),
                    ShaderLocation = 1,
                },
            },
        };

        var vertexState = new WgpuVertexState
        {
            Module = _shaderModule,
            EntryPoint = "vs_main",
            Buffers = new[] { vertexLayout },
        };

        var fragmentState = new WgpuFragmentState
        {
            Module = _shaderModule,
            EntryPoint = "fs_main",
            Targets = new[]
            {
                new WgpuColorTargetState
                {
                    Format = surfaceFormat,
                    Blend = null,
                    WriteMask = WgpuColorWriteMask.All,
                },
            },
        };

        var primitiveState = new WgpuPrimitiveState
        {
            Topology = WgpuPrimitiveTopology.TriangleList,
            StripIndexFormat = null,
            FrontFace = WgpuFrontFace.Ccw,
            CullMode = WgpuCullMode.Back,
            UnclippedDepth = false,
            PolygonMode = WgpuPolygonMode.Fill,
            Conservative = false,
        };

        var pipelineDescriptor = new WgpuRenderPipelineDescriptor
        {
            Label = "CubePipeline",
            Layout = _pipelineLayout,
            Vertex = vertexState,
            Primitive = primitiveState,
            DepthStencil = null,
            Multisample = new WgpuMultisampleState
            {
                Count = 1,
                Mask = uint.MaxValue,
                AlphaToCoverageEnabled = false,
            },
            Fragment = fragmentState,
        };

        DisposeResource(ref _pipeline);
        _pipeline = device.CreateRenderPipeline(pipelineDescriptor);
    }

    private void UpdateUniformBuffer(WgpuQueue queue, Rect viewportRect, float timeSeconds)
    {
        if (_uniformBuffer is null)
        {
            return;
        }

        var aspect = (float)(viewportRect.Height <= double.Epsilon
            ? 1.0
            : viewportRect.Width / viewportRect.Height);

        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspect, 0.1f, 100f);
        var view = Matrix4x4.CreateLookAt(new Vector3(1.5f, -5f, 3f), Vector3.Zero, Vector3.UnitZ);
        var rotation = Matrix4x4.CreateRotationY(timeSeconds * 0.8f) * Matrix4x4.CreateRotationX(timeSeconds * 0.4f);
        var mvp = rotation * view * projection;
        var matrix = Matrix4x4.Transpose(mvp);

        Span<float> buffer = stackalloc float[16]
        {
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44,
        };

        queue.WriteBuffer(_uniformBuffer, 0, MemoryMarshal.AsBytes(buffer));
    }

    private void EncodeCommands(WgpuDevice device, WgpuQueue queue, WgpuTextureView targetView, RenderParams renderParams, Rect viewportRect)
    {
        if (_pipeline is null || _bindGroup is null || _vertexBuffer is null || _indexBuffer is null)
        {
            return;
        }

        using var encoder = device.CreateCommandEncoder(new WgpuCommandEncoderDescriptor { Label = "CubeEncoder" });

        var colorAttachment = new WgpuRenderPassColorAttachment
        {
            View = targetView,
            ResolveTarget = null,
            Load = WgpuLoadOp.Load,
            Store = WgpuStoreOp.Store,
            ClearColor = new WgpuColor
            {
                R = renderParams.BaseColor.R,
                G = renderParams.BaseColor.G,
                B = renderParams.BaseColor.B,
                A = renderParams.BaseColor.A,
            },
        };

        var renderPassDesc = new WgpuRenderPassDescriptor
        {
            Label = "CubeRenderPass",
            ColorAttachments = new[] { colorAttachment },
            DepthStencilAttachment = null,
        };

        using (var pass = encoder.BeginRenderPass(renderPassDesc))
        {
            var viewportX = (float)viewportRect.X;
            var viewportY = (float)viewportRect.Y;
            var viewportWidth = (float)viewportRect.Width;
            var viewportHeight = (float)viewportRect.Height;

            pass.SetViewport(viewportX, viewportY, viewportWidth, viewportHeight);
            var surfaceWidth = (int)renderParams.Width;
            var surfaceHeight = (int)renderParams.Height;

            var scissorX = (int)Math.Floor(viewportRect.X);
            var scissorY = (int)Math.Floor(viewportRect.Y);
            scissorX = Math.Clamp(scissorX, 0, Math.Max(surfaceWidth - 1, 0));
            scissorY = Math.Clamp(scissorY, 0, Math.Max(surfaceHeight - 1, 0));

            var scissorWidth = (int)Math.Ceiling(viewportRect.Width);
            var scissorHeight = (int)Math.Ceiling(viewportRect.Height);
            scissorWidth = Math.Clamp(scissorWidth, 1, Math.Max(surfaceWidth - scissorX, 1));
            scissorHeight = Math.Clamp(scissorHeight, 1, Math.Max(surfaceHeight - scissorY, 1));

            pass.SetScissorRect((uint)scissorX, (uint)scissorY, (uint)scissorWidth, (uint)scissorHeight);

            pass.SetPipeline(_pipeline);
            pass.SetBindGroup(0, _bindGroup);
            pass.SetVertexBuffer(0, _vertexBuffer);
            pass.SetIndexBuffer(_indexBuffer, WgpuIndexFormat.Uint16);
            pass.DrawIndexed((uint)_indexCount, 1, 0, 0, 0);
        }

        using var commandBuffer = encoder.Finish(new WgpuCommandBufferDescriptor { Label = "CubeCommandBuffer" });
        queue.Submit(new[] { commandBuffer });
    }

    private static void DisposeResource<T>(ref T? resource) where T : class, IDisposable
    {
        if (resource is not null)
        {
            resource.Dispose();
            resource = null;
        }
    }

    ~CubeRenderer()
    {
        Dispose();
    }
}
