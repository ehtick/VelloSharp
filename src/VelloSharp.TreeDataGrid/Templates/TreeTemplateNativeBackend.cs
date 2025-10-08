using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VelloSharp.TreeDataGrid.Rendering;

namespace VelloSharp.TreeDataGrid.Templates;

public sealed class TreeTemplateNativeBackend : ITreeTemplateBackend, IDisposable
{
    private bool _disposed;

    public TreeTemplateRuntimeHandle Realize(
        TreeTemplateCacheKey key,
        int generation,
        ReadOnlySpan<TreeTemplateInstruction> instructions,
        in TreeTemplateRuntimeContext context)
    {
        ThrowIfDisposed();
        using var buffer = new NativeInstructionBuffer(instructions);
        unsafe
        {
            fixed (NativeMethods.VelloTdgTemplateInstruction* ptr = buffer.Span)
            {
                var handle = NativeMethods.vello_tdg_template_program_create(ptr, (nuint)buffer.Span.Length);
                if (handle == IntPtr.Zero)
                {
                    ThrowNativeError("Failed to create template program.");
                }

                return new TreeTemplateRuntimeHandle(Guid.NewGuid(), handle);
            }
        }
    }

    public void Execute(
        TreeTemplateRuntimeHandle handle,
        TreeCompiledTemplate template,
        TreeSceneGraph sceneGraph,
        in TreeTemplateRuntimeContext context)
    {
        ThrowIfDisposed();
        if (handle.NativeHandle == IntPtr.Zero)
        {
            return;
        }

        unsafe
        {
            using var bindings = new NativeBindingBuffer(context.Bindings);
            foreach (var batch in context.PaneBatches.EnumerateActive())
            {
                if (batch.IsEmpty)
                {
                    continue;
                }

                var spans = batch.GetSpans();
                if (spans.Length == 0)
                {
                    continue;
                }

                double minX = spans[0].Offset;
                double maxX = spans[^1].Offset + spans[^1].Width;

                using var columns = new NativeColumnPlanBuffer(spans);
                bool added = false;
                try
                {
                    sceneGraph.Cache.DangerousAddRef(ref added);
                    var cacheHandle = sceneGraph.Cache.DangerousGetHandle();
                    fixed (NativeMethods.VelloTdgColumnPlan* columnsPtr = columns.Span)
                    fixed (NativeMethods.VelloTdgTemplateBinding* bindingsPtr = bindings.Span)
                    {
                        if (!NativeMethods.vello_tdg_template_program_encode_pane(
                                handle.NativeHandle,
                                cacheHandle,
                                batch.NodeId,
                                ConvertPaneKind(batch.Pane),
                                columnsPtr,
                                (nuint)columns.Span.Length,
                                bindingsPtr,
                                (nuint)bindings.Span.Length))
                        {
                            ThrowNativeError("Template pane encoding failed.");
                        }
                    }
                }
                finally
                {
                    if (added)
                    {
                        sceneGraph.Cache.DangerousRelease();
                    }
                }

                sceneGraph.MarkRowDirty(batch.NodeId, minX, maxX, 0.0, 24.0);
            }
        }
    }

    public void Release(TreeTemplateRuntimeHandle handle)
    {
        if (handle.NativeHandle != IntPtr.Zero)
        {
            NativeMethods.vello_tdg_template_program_destroy(handle.NativeHandle);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static NativeMethods.VelloTdgTemplatePaneKind ConvertPaneKind(TreeFrozenKind pane)
        => pane switch
        {
            TreeFrozenKind.Leading => NativeMethods.VelloTdgTemplatePaneKind.Leading,
            TreeFrozenKind.Trailing => NativeMethods.VelloTdgTemplatePaneKind.Trailing,
            _ => NativeMethods.VelloTdgTemplatePaneKind.Primary,
        };

    private static void ThrowNativeError(string message)
    {
        var detail = NativeMethods.GetLastError();
        if (!string.IsNullOrEmpty(detail))
        {
            throw new InvalidOperationException($"{message} {detail}");
        }

        throw new InvalidOperationException(message);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeTemplateNativeBackend));
        }
    }

    private sealed class NativeInstructionBuffer : IDisposable
    {
        private readonly List<IntPtr> _allocated = new();
        private readonly NativeMethods.VelloTdgTemplateInstruction[] _instructions;

        public NativeInstructionBuffer(ReadOnlySpan<TreeTemplateInstruction> instructions)
        {
            _instructions = new NativeMethods.VelloTdgTemplateInstruction[instructions.Length];
            for (int i = 0; i < instructions.Length; i++)
            {
                ref readonly var source = ref instructions[i];
                _instructions[i] = new NativeMethods.VelloTdgTemplateInstruction
                {
                    OpCode = ConvertOpCode(source.OpCode),
                    NodeKind = ConvertNodeKind(source.NodeKind),
                    ValueKind = ConvertValueKind(source.Value.Kind),
                    Property = AllocateString(source.PropertyName),
                    Value = RequiresString(source.Value.Kind) ? AllocateString(source.Value.Raw) : IntPtr.Zero,
                    NumberValue = source.Value.Number ?? 0.0,
                    BooleanValue = source.Value.Boolean == true ? 1 : 0,
                };
            }
        }

        public ReadOnlySpan<NativeMethods.VelloTdgTemplateInstruction> Span => _instructions;

        public void Dispose()
        {
            foreach (var ptr in _allocated)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }

            _allocated.Clear();
        }

        private IntPtr AllocateString(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return IntPtr.Zero;
            }

            var ptr = Marshal.StringToCoTaskMemUTF8(value);
            _allocated.Add(ptr);
            return ptr;
        }
    }

    private sealed class NativeBindingBuffer : IDisposable
    {
        private readonly List<IntPtr> _allocated = new();
        private readonly NativeMethods.VelloTdgTemplateBinding[] _bindings;

        public NativeBindingBuffer(TreeTemplateBindings bindings)
        {
            var values = bindings.Values;
            if (values is null || values.Count == 0)
            {
                _bindings = Array.Empty<NativeMethods.VelloTdgTemplateBinding>();
                return;
            }

            _bindings = new NativeMethods.VelloTdgTemplateBinding[values.Count];
            int index = 0;
            foreach (var pair in values)
            {
                var binding = new NativeMethods.VelloTdgTemplateBinding
                {
                    Path = AllocateString(pair.Key),
                };

                switch (pair.Value)
                {
                    case null:
                        binding.Kind = NativeMethods.VelloTdgTemplateValueKind.String;
                        binding.StringValue = IntPtr.Zero;
                        break;
                    case bool boolean:
                        binding.Kind = NativeMethods.VelloTdgTemplateValueKind.Boolean;
                        binding.BooleanValue = boolean ? 1 : 0;
                        break;
                    case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                        binding.Kind = NativeMethods.VelloTdgTemplateValueKind.Number;
                        binding.NumberValue = Convert.ToDouble(pair.Value);
                        break;
                    default:
                        binding.Kind = NativeMethods.VelloTdgTemplateValueKind.String;
                        binding.StringValue = AllocateString(pair.Value.ToString());
                        break;
                }

                _bindings[index++] = binding;
            }
        }

        public ReadOnlySpan<NativeMethods.VelloTdgTemplateBinding> Span => _bindings;

        public void Dispose()
        {
            foreach (var ptr in _allocated)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }

            _allocated.Clear();
        }

        private IntPtr AllocateString(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return IntPtr.Zero;
            }

            var ptr = Marshal.StringToCoTaskMemUTF8(value);
            _allocated.Add(ptr);
            return ptr;
        }
    }

    private sealed class NativeColumnPlanBuffer : IDisposable
    {
        private NativeMethods.VelloTdgColumnPlan[] _plans;

        public NativeColumnPlanBuffer(ReadOnlySpan<TreeColumnSpan> spans)
        {
            _plans = new NativeMethods.VelloTdgColumnPlan[spans.Length];
            for (int i = 0; i < spans.Length; i++)
            {
                _plans[i] = new NativeMethods.VelloTdgColumnPlan
                {
                    Offset = spans[i].Offset,
                    Width = spans[i].Width,
                    Frozen = ConvertFrozen(spans[i].Frozen),
                    Key = spans[i].Key,
                };
            }
        }

        public Span<NativeMethods.VelloTdgColumnPlan> Span => _plans;

        public void Dispose()
        {
            _plans = Array.Empty<NativeMethods.VelloTdgColumnPlan>();
        }
    }

    private static NativeMethods.VelloTdgTemplateOpCode ConvertOpCode(TreeTemplateOpCode op)
        => op switch
        {
            TreeTemplateOpCode.OpenNode => NativeMethods.VelloTdgTemplateOpCode.OpenNode,
            TreeTemplateOpCode.CloseNode => NativeMethods.VelloTdgTemplateOpCode.CloseNode,
            TreeTemplateOpCode.BindProperty => NativeMethods.VelloTdgTemplateOpCode.BindProperty,
            _ => NativeMethods.VelloTdgTemplateOpCode.SetProperty,
        };

    private static NativeMethods.VelloTdgTemplateNodeKind ConvertNodeKind(TreeTemplateNodeKind kind)
        => kind switch
        {
            TreeTemplateNodeKind.Templates => NativeMethods.VelloTdgTemplateNodeKind.Templates,
            TreeTemplateNodeKind.RowTemplate => NativeMethods.VelloTdgTemplateNodeKind.RowTemplate,
            TreeTemplateNodeKind.GroupHeaderTemplate => NativeMethods.VelloTdgTemplateNodeKind.GroupHeaderTemplate,
            TreeTemplateNodeKind.SummaryTemplate => NativeMethods.VelloTdgTemplateNodeKind.SummaryTemplate,
            TreeTemplateNodeKind.ChromeTemplate => NativeMethods.VelloTdgTemplateNodeKind.ChromeTemplate,
            TreeTemplateNodeKind.PaneTemplate => NativeMethods.VelloTdgTemplateNodeKind.PaneTemplate,
            TreeTemplateNodeKind.CellTemplate => NativeMethods.VelloTdgTemplateNodeKind.CellTemplate,
            TreeTemplateNodeKind.Stack => NativeMethods.VelloTdgTemplateNodeKind.Stack,
            TreeTemplateNodeKind.Text => NativeMethods.VelloTdgTemplateNodeKind.Text,
            TreeTemplateNodeKind.Rectangle => NativeMethods.VelloTdgTemplateNodeKind.Rectangle,
            TreeTemplateNodeKind.Image => NativeMethods.VelloTdgTemplateNodeKind.Image,
            TreeTemplateNodeKind.ContentPresenter => NativeMethods.VelloTdgTemplateNodeKind.ContentPresenter,
            _ => NativeMethods.VelloTdgTemplateNodeKind.Unknown,
        };

    private static NativeMethods.VelloTdgTemplateValueKind ConvertValueKind(TreeTemplateValueKind kind)
        => kind switch
        {
            TreeTemplateValueKind.String => NativeMethods.VelloTdgTemplateValueKind.String,
            TreeTemplateValueKind.Number => NativeMethods.VelloTdgTemplateValueKind.Number,
            TreeTemplateValueKind.Boolean => NativeMethods.VelloTdgTemplateValueKind.Boolean,
            TreeTemplateValueKind.Binding => NativeMethods.VelloTdgTemplateValueKind.Binding,
            TreeTemplateValueKind.Color => NativeMethods.VelloTdgTemplateValueKind.Color,
            _ => NativeMethods.VelloTdgTemplateValueKind.Unknown,
        };

    private static NativeMethods.VelloTdgFrozenKind ConvertFrozen(TreeFrozenKind frozen)
        => frozen switch
        {
            TreeFrozenKind.Leading => NativeMethods.VelloTdgFrozenKind.Leading,
            TreeFrozenKind.Trailing => NativeMethods.VelloTdgFrozenKind.Trailing,
            _ => NativeMethods.VelloTdgFrozenKind.None,
        };

    private static bool RequiresString(TreeTemplateValueKind kind)
        => kind is TreeTemplateValueKind.String or TreeTemplateValueKind.Color or TreeTemplateValueKind.Binding;
}
