using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using VelloSharp.Windows.Controls;

namespace VelloSharp.Windows.Accessibility;

internal sealed class VelloSwapChainAutomationPeer : FrameworkElementAutomationPeer
{
    private AccessKitTreeSnapshot? _snapshot;

    public VelloSwapChainAutomationPeer(VelloSwapChainControl owner)
        : base(owner)
    {
    }

    private VelloSwapChainControl Control => (VelloSwapChainControl)Owner;

    internal void UpdateSnapshot(AccessKitTreeSnapshot? snapshot)
    {
        _snapshot = snapshot;
        RaiseAutomationEvent(AutomationEvents.StructureChanged);
    }

    protected override string GetClassNameCore()
        => nameof(VelloSwapChainControl);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Custom;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override string? GetNameCore()
    {
        if (base.GetNameCore() is { Length: > 0 } name)
        {
            return name;
        }

        if (_snapshot is not null &&
            _snapshot.TryGetNode(_snapshot.RootId, out var node) &&
            !string.IsNullOrWhiteSpace(node.Name))
        {
            return node.Name;
        }

        return Control.Name;
    }

    protected override List<AutomationPeer> GetChildrenCore()
    {
        if (_snapshot is null)
        {
            return new List<AutomationPeer>();
        }

        var children = _snapshot.GetRootChildren();
        if (children.IsDefaultOrEmpty)
        {
            return new List<AutomationPeer>();
        }

        return children
            .Select(child => (AutomationPeer)new AccessKitNodeAutomationPeer(Control, _snapshot, child))
            .ToList();
    }

    protected override object? GetPatternCore(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.Invoke ||
            patternInterface == PatternInterface.SelectionItem)
        {
            return new AccessKitRootActionProvider(Control, _snapshot);
        }

        return base.GetPatternCore(patternInterface);
    }

    private sealed class AccessKitRootActionProvider : IInvokeProvider, ISelectionItemProvider
    {
        private readonly VelloSwapChainControl _control;
        private readonly AccessKitTreeSnapshot? _snapshot;

        public AccessKitRootActionProvider(VelloSwapChainControl control, AccessKitTreeSnapshot? snapshot)
        {
            _control = control;
            _snapshot = snapshot;
        }

        public void Invoke()
        {
            if (_snapshot is null)
            {
                return;
            }

            _control.RequestAccessKitAction("focus", _snapshot.RootId);
        }

        public bool IsSelected => false;

        public IRawElementProviderSimple? SelectionContainer => null;

        public void AddToSelection()
        {
            if (_snapshot is null)
            {
                return;
            }

            _control.RequestAccessKitAction("focus", _snapshot.RootId);
        }

        public void RemoveFromSelection()
        {
        }

        public void Select()
        {
            if (_snapshot is null)
            {
                return;
            }

            _control.RequestAccessKitAction("focus", _snapshot.RootId);
        }
    }
}
