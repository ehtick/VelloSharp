using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaVelloCommon.Rendering;

internal sealed class CubeRenderer : IDisposable
{
    private const uint TextureSize = 256;

    private static readonly Vector3 CameraPosition = new(1.5f, -5f, 3f);
    private static readonly Vector3 CameraTarget = Vector3.Zero;
    private static readonly Vector3 CameraUp = Vector3.UnitZ;

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
    private WgpuRenderPipeline? _wireframePipeline;
    private bool _wireframeSupported;
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

        var transform = ComputeTransformMatrix(viewportRect, timeSeconds);

        UpdateUniformBuffer(context.Queue, transform);
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
        DisposeResource(ref _wireframePipeline);
        _device = null;
        _surfaceFormat = default;
        _indexCount = 0;
        _wireframeSupported = false;
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
            _wireframeSupported = (device.GetFeatures() & WgpuFeature.PolygonModeLine) != 0;
            CreateStaticResources(device, queue);
            CreatePipelines(device, surfaceFormat);
            return _pipeline is not null;
        }

        if (_surfaceFormat != surfaceFormat)
        {
            _surfaceFormat = surfaceFormat;
            DisposeResource(ref _pipeline);
            DisposeResource(ref _wireframePipeline);
            CreatePipelines(device, surfaceFormat);
        }

        if (_pipeline is null)
        {
            CreatePipelines(device, surfaceFormat);
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

    private void CreatePipelines(WgpuDevice device, WgpuTextureFormat surfaceFormat)
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

        DisposeResource(ref _wireframePipeline);

        if (_wireframeSupported)
        {
            var wireframeFragmentState = new WgpuFragmentState
            {
                Module = _shaderModule,
                EntryPoint = "fs_wire",
                Targets = new[]
                {
                    new WgpuColorTargetState
                    {
                        Format = surfaceFormat,
                        Blend = new WgpuBlendState
                        {
                            Color = new WgpuBlendComponent
                            {
                                SrcFactor = WgpuBlendFactor.SrcAlpha,
                                DstFactor = WgpuBlendFactor.OneMinusSrcAlpha,
                                Operation = WgpuBlendOperation.Add,
                            },
                            Alpha = new WgpuBlendComponent
                            {
                                SrcFactor = WgpuBlendFactor.One,
                                DstFactor = WgpuBlendFactor.OneMinusSrcAlpha,
                                Operation = WgpuBlendOperation.Add,
                            },
                        },
                        WriteMask = WgpuColorWriteMask.All,
                    },
                },
            };

            var wireframePrimitiveState = new WgpuPrimitiveState
            {
                Topology = WgpuPrimitiveTopology.TriangleList,
                StripIndexFormat = null,
                FrontFace = WgpuFrontFace.Ccw,
                CullMode = WgpuCullMode.Back,
                UnclippedDepth = false,
                PolygonMode = WgpuPolygonMode.Line,
                Conservative = false,
            };

            var wireframeDescriptor = new WgpuRenderPipelineDescriptor
            {
                Label = "CubeWireframePipeline",
                Layout = _pipelineLayout,
                Vertex = vertexState,
                Primitive = wireframePrimitiveState,
                DepthStencil = null,
                Multisample = new WgpuMultisampleState
                {
                    Count = 1,
                    Mask = uint.MaxValue,
                    AlphaToCoverageEnabled = false,
                },
                Fragment = wireframeFragmentState,
            };

            _wireframePipeline = device.CreateRenderPipeline(wireframeDescriptor);
        }
    }

    private void UpdateUniformBuffer(WgpuQueue queue, in Matrix4x4 uniformMatrix)
    {
        if (_uniformBuffer is null)
        {
            return;
        }

        Span<float> buffer = stackalloc float[16]
        {
            uniformMatrix.M11, uniformMatrix.M12, uniformMatrix.M13, uniformMatrix.M14,
            uniformMatrix.M21, uniformMatrix.M22, uniformMatrix.M23, uniformMatrix.M24,
            uniformMatrix.M31, uniformMatrix.M32, uniformMatrix.M33, uniformMatrix.M34,
            uniformMatrix.M41, uniformMatrix.M42, uniformMatrix.M43, uniformMatrix.M44,
        };

        queue.WriteBuffer(_uniformBuffer, 0, MemoryMarshal.AsBytes(buffer));
    }

    private Matrix4x4 ComputeTransformMatrix(Rect viewportRect, float timeSeconds)
    {
        var aspect = (float)(viewportRect.Height <= double.Epsilon
            ? 1.0
            : viewportRect.Width / viewportRect.Height);

        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspect, 1f, 10f);
        var view = Matrix4x4.CreateLookAt(CameraPosition, CameraTarget, CameraUp);

        var rotationY = Matrix4x4.CreateRotationY(timeSeconds * 0.8f);
        var rotationX = Matrix4x4.CreateRotationX(timeSeconds * 0.4f);
        var model = Matrix4x4.Multiply(rotationY, rotationX);

        var modelViewProjection = Matrix4x4.Multiply(Matrix4x4.Multiply(model, view), projection);
        return Matrix4x4.Transpose(modelViewProjection);
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

            if (_wireframePipeline is not null)
            {
                pass.SetPipeline(_wireframePipeline);
                pass.SetBindGroup(0, _bindGroup);
                pass.DrawIndexed((uint)_indexCount, 1, 0, 0, 0);
            }
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
