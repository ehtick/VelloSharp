using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VelloSharp;

namespace Avalonia.Winit;

internal sealed class WinitAccessKitManager : IDisposable
{
    private const ulong RootNodeId = 1;

    private readonly WinitWindowImpl _owner;
    private readonly WinitDispatcher _dispatcher;
    private readonly Dictionary<AutomationPeer, ulong> _peerToId;
    private readonly Dictionary<ulong, WeakReference<AutomationPeer>> _idToPeer = new();
    private readonly object _updateLock = new();

    private bool _initialized;
    private bool _updateScheduled;
    private WinitWindow? _window;
    private WindowAutomationPeer? _rootPeer;
    private ulong _nextPeerId = RootNodeId + 1;

    public WinitAccessKitManager(WinitWindowImpl owner, WinitDispatcher dispatcher)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _peerToId = new Dictionary<AutomationPeer, ulong>(ReferenceEqualityComparer<AutomationPeer>.Instance);
    }

    public void InitializeNative(WinitEventLoopContext context, WinitWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_initialized)
        {
            return;
        }

        context.InitializeAccessKit(window);
        _window = window;
        _initialized = true;
    }

    public void AttachAutomationPeer(WindowAutomationPeer? peer)
    {
        if (ReferenceEquals(_rootPeer, peer))
        {
            return;
        }

        DetachAutomationPeer();

        if (peer is null)
        {
            return;
        }

        _rootPeer = peer;
        _peerToId.Clear();
        _idToPeer.Clear();
        _nextPeerId = RootNodeId + 1;
        RegisterPeer(peer, RootNodeId);

        peer.ChildrenChanged += OnAutomationChanged;
        peer.PropertyChanged += OnAutomationPropertyChanged;
        if (peer is IRootProvider rootProvider)
        {
            rootProvider.FocusChanged += OnFocusChanged;
        }

        RequestUpdate();
    }

    private void DetachAutomationPeer()
    {
        if (_rootPeer is null)
        {
            return;
        }

        _rootPeer.ChildrenChanged -= OnAutomationChanged;
        _rootPeer.PropertyChanged -= OnAutomationPropertyChanged;
        if (_rootPeer is IRootProvider rootProvider)
        {
            rootProvider.FocusChanged -= OnFocusChanged;
        }

        _rootPeer = null;
        _peerToId.Clear();
        _idToPeer.Clear();
        _nextPeerId = RootNodeId + 1;
    }

    public void HandleAccessKitEvent(in WinitEventArgs args)
    {
        if (!_initialized)
        {
            return;
        }

        switch (args.AccessKitEventKind)
        {
            case WinitAccessKitEventKind.InitialTreeRequested:
                RequestUpdate();
                break;
            case WinitAccessKitEventKind.ActionRequested:
                if (!string.IsNullOrEmpty(args.AccessKitActionJson))
                {
                    var actionJson = args.AccessKitActionJson!;
                    _dispatcher.Post(() => ExecuteAction(actionJson));
                }

                break;
            case WinitAccessKitEventKind.AccessibilityDeactivated:
                break;
        }
    }

    public void Dispose()
    {
        DetachAutomationPeer();
    }

    private void ExecuteAction(string json)
    {
        AccessKitActionRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AccessKitActionRequestDto>(json);
        }
        catch (JsonException)
        {
            return;
        }

        if (request is null)
        {
            return;
        }

        if (!_idToPeer.TryGetValue(request.Target, out var weak) || !weak.TryGetTarget(out var peer))
        {
            return;
        }

        PerformAction(peer, request);
    }

    private void PerformAction(AutomationPeer peer, AccessKitActionRequestDto request)
    {
        if (!Enum.TryParse(request.Action, out AccessKitAction action))
        {
            return;
        }

        switch (action)
        {
            case AccessKitAction.Focus:
                peer.SetFocus();
                break;
            case AccessKitAction.Click:
                peer.GetProvider<IInvokeProvider>()?.Invoke();
                break;
            case AccessKitAction.Expand:
                peer.GetProvider<IExpandCollapseProvider>()?.Expand();
                break;
            case AccessKitAction.Collapse:
                peer.GetProvider<IExpandCollapseProvider>()?.Collapse();
                break;
            case AccessKitAction.ScrollIntoView:
                peer.BringIntoView();
                break;
            case AccessKitAction.SetValue:
                if (request.Data.ValueKind == JsonValueKind.Object && request.Data.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.String)
                {
                    peer.GetProvider<IValueProvider>()?.SetValue(valueElement.GetString()!);
                }
                else if (request.Data.ValueKind == JsonValueKind.Object && request.Data.TryGetProperty("numericValue", out var numericElement) && numericElement.ValueKind is JsonValueKind.Number)
                {
                    if (peer.GetProvider<IRangeValueProvider>() is { } rangeValue && numericElement.TryGetDouble(out var number))
                    {
                        var clamped = Math.Clamp(number, rangeValue.Minimum, rangeValue.Maximum);
                        rangeValue.SetValue(clamped);
                    }
                }

                break;
            case AccessKitAction.Increment:
                if (peer.GetProvider<IRangeValueProvider>() is { } incRange)
                {
                    var next = Math.Min(incRange.Value + incRange.SmallChange, incRange.Maximum);
                    incRange.SetValue(next);
                }

                break;
            case AccessKitAction.Decrement:
                if (peer.GetProvider<IRangeValueProvider>() is { } decRange)
                {
                    var next = Math.Max(decRange.Value - decRange.SmallChange, decRange.Minimum);
                    decRange.SetValue(next);
                }

                break;
            case AccessKitAction.ShowContextMenu:
                peer.ShowContextMenu();
                break;
            default:
                break;
        }

        RequestUpdate();
    }

    private void OnAutomationChanged(object? sender, EventArgs e) => RequestUpdate();

    private void OnAutomationPropertyChanged(object? sender, AutomationPropertyChangedEventArgs e) => RequestUpdate();

    private void OnFocusChanged(object? sender, EventArgs e) => RequestUpdate();

    public void InvalidateTree() => RequestUpdate();

    private void RequestUpdate()
    {
        if (!_initialized || _window is null)
        {
            return;
        }

        bool shouldPost;
        lock (_updateLock)
        {
            if (_updateScheduled)
            {
                return;
            }

            _updateScheduled = true;
            shouldPost = true;
        }

        if (shouldPost)
        {
            Dispatcher.UIThread.Post(() =>
            {
                AccessKitTreeUpdatePayload? payload = null;

                lock (_updateLock)
                {
                    if (!_initialized || _window is null)
                    {
                        _updateScheduled = false;
                        return;
                    }

                    payload = BuildTreeUpdatePayload();
                    _updateScheduled = false;
                }

                if (payload is null || _window is null)
                {
                    return;
                }

                using var update = AccessKitTreeUpdate.FromObject(payload);
                var json = update.ToJson();
                _window.SubmitAccessKitUpdate(json);
            });
        }
    }

    private AccessKitTreeUpdatePayload? BuildTreeUpdatePayload()
    {
        if (_rootPeer is null)
        {
            return null;
        }

        var payload = new AccessKitTreeUpdatePayload
        {
            Tree = new AccessKitTreePayload
            {
                Root = RootNodeId,
                ToolkitName = "Avalonia",
                ToolkitVersion = typeof(Application).Assembly.GetName().Version?.ToString()
            }
        };

        payload.Nodes.Add(BuildNodeEntry(_rootPeer, RootNodeId));

        var children = _rootPeer.GetChildren();
        foreach (var child in children)
        {
            VisitPeer(child, payload);
        }

        payload.Focus = GetFocusNodeId();

        return payload;
    }

    private void VisitPeer(AutomationPeer peer, AccessKitTreeUpdatePayload payload)
    {
        var id = EnsurePeerId(peer);
        payload.Nodes.Add(BuildNodeEntry(peer, id));

        var children = peer.GetChildren();
        if (children.Count == 0)
        {
            return;
        }

        foreach (var child in children)
        {
            VisitPeer(child, payload);
        }
    }

    private object BuildNodeEntry(AutomationPeer peer, ulong id)
    {
        var node = new AccessKitNodePayload
        {
            Role = MapRole(peer)
        };

        var name = peer.GetName();
        if (!string.IsNullOrEmpty(name))
        {
            node.Name = name;
        }

        var rect = peer.GetBoundingRectangle();
        if (rect.Width > 0 && rect.Height > 0)
        {
            node.Bounds = new AccessKitRectPayload(rect);
        }

        if (!peer.IsEnabled())
        {
            node.IsDisabled = true;
        }

        if (peer.IsKeyboardFocusable())
        {
            node.Actions ??= new List<string>();
            node.Actions.Add("Focus");
        }

        var children = peer.GetChildren();
        if (children.Count > 0)
        {
            var childIds = new List<ulong>(children.Count);
            foreach (var child in children)
            {
                childIds.Add(EnsurePeerId(child));
            }

            node.Children = childIds;
        }

        return new object[] { id, node };
    }

    private ulong GetFocusNodeId()
    {
        if (_rootPeer is IRootProvider rootProvider)
        {
            var focus = rootProvider.GetFocus();
            if (focus is not null)
            {
                return EnsurePeerId(focus);
            }
        }

        return RootNodeId;
    }

    private ulong EnsurePeerId(AutomationPeer peer)
    {
        if (_peerToId.TryGetValue(peer, out var id))
        {
            return id;
        }

        var newId = _nextPeerId++;
        RegisterPeer(peer, newId);
        return newId;
    }

    private void RegisterPeer(AutomationPeer peer, ulong id)
    {
        _peerToId[peer] = id;
        _idToPeer[id] = new WeakReference<AutomationPeer>(peer);
    }

    private static string MapRole(AutomationPeer peer)
    {
        return peer.GetAutomationControlType() switch
        {
            AutomationControlType.Button => "Button",
            AutomationControlType.CheckBox => "CheckBox",
            AutomationControlType.ComboBox => "ComboBox",
            AutomationControlType.List => "List",
            AutomationControlType.ListItem => "ListItem",
            AutomationControlType.Menu => "Menu",
            AutomationControlType.MenuItem => "MenuItem",
            AutomationControlType.MenuBar => "MenuBar",
            AutomationControlType.ProgressBar => "ProgressIndicator",
            AutomationControlType.RadioButton => "RadioButton",
            AutomationControlType.Slider => "Slider",
            AutomationControlType.ScrollBar => "ScrollBar",
            AutomationControlType.Spinner => "SpinButton",
            AutomationControlType.Tab => "TabList",
            AutomationControlType.TabItem => "Tab",
            AutomationControlType.Table => "Table",
            AutomationControlType.ToolBar => "Toolbar",
            AutomationControlType.ToolTip => "Tooltip",
            AutomationControlType.Tree => "Tree",
            AutomationControlType.TreeItem => "TreeItem",
            AutomationControlType.Hyperlink => "Link",
            AutomationControlType.Image => "Image",
            AutomationControlType.Text => "Label",
            AutomationControlType.Edit => "TextInput",
            AutomationControlType.Window => "Window",
            AutomationControlType.Pane => "Pane",
            AutomationControlType.Header => "Header",
            AutomationControlType.HeaderItem => "Header",
            AutomationControlType.Separator => "GenericContainer",
            AutomationControlType.Document => "Document",
            AutomationControlType.Custom => "GenericContainer",
            _ => "GenericContainer"
        };
    }

    private enum AccessKitAction
    {
        Click,
        Focus,
        Blur,
        Collapse,
        Expand,
        CustomAction,
        Decrement,
        Increment,
        HideTooltip,
        ShowTooltip,
        ReplaceSelectedText,
        ScrollDown,
        ScrollLeft,
        ScrollRight,
        ScrollUp,
        ScrollIntoView,
        ScrollToPoint,
        SetScrollOffset,
        SetTextSelection,
        SetSequentialFocusNavigationStartingPoint,
        SetValue,
        ShowContextMenu,
    }

    private sealed class AccessKitActionRequestDto
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public ulong Target { get; set; }

        [JsonPropertyName("data")]
        public JsonElement Data { get; set; }
    }

    private sealed class AccessKitTreeUpdatePayload
    {
        [JsonPropertyName("nodes")]
        public List<object> Nodes { get; } = new();

        [JsonPropertyName("tree")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AccessKitTreePayload? Tree { get; set; }

        [JsonPropertyName("focus")]
        public ulong Focus { get; set; }
    }

    private sealed class AccessKitTreePayload
    {
        [JsonPropertyName("root")]
        public ulong Root { get; set; }

        [JsonPropertyName("toolkitName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolkitName { get; set; }

        [JsonPropertyName("toolkitVersion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolkitVersion { get; set; }
    }

    private sealed class AccessKitNodePayload
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "Unknown";

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonPropertyName("bounds")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AccessKitRectPayload? Bounds { get; set; }

        [JsonPropertyName("children")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ulong>? Children { get; set; }

        [JsonPropertyName("isDisabled")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsDisabled { get; set; }

        [JsonPropertyName("actions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Actions { get; set; }
    }

    private sealed class AccessKitRectPayload
    {
        public AccessKitRectPayload(Rect rect)
        {
            X0 = rect.X;
            Y0 = rect.Y;
            X1 = rect.X + rect.Width;
            Y1 = rect.Y + rect.Height;
        }

        [JsonPropertyName("x0")]
        public double X0 { get; }

        [JsonPropertyName("y0")]
        public double Y0 { get; }

        [JsonPropertyName("x1")]
        public double X1 { get; }

        [JsonPropertyName("y1")]
        public double Y1 { get; }
    }
}

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static ReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
