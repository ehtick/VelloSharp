using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Windows.Foundation;
using VelloSharp.Uwp.Controls;

#if WINDOWS_UWP
using AutomationPeer = Windows.UI.Xaml.Automation.Peers.AutomationPeer;
using AutomationControlType = Windows.UI.Xaml.Automation.Peers.AutomationControlType;
using PatternInterface = Windows.UI.Xaml.Automation.Peers.PatternInterface;
using IInvokeProvider = Windows.UI.Xaml.Automation.Provider.IInvokeProvider;
#else
using AutomationPeer = Microsoft.UI.Xaml.Automation.Peers.AutomationPeer;
using AutomationControlType = Microsoft.UI.Xaml.Automation.Peers.AutomationControlType;
using PatternInterface = Microsoft.UI.Xaml.Automation.Peers.PatternInterface;
using IInvokeProvider = Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider;
#endif

namespace VelloSharp.Uwp.Accessibility;

internal sealed class AccessKitNodeAutomationPeer : AutomationPeer, IInvokeProvider
{
    private static readonly ImmutableHashSet<string> InvokeActions = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "click", "invoke");
    private readonly VelloSwapChainPanel _owner;
    private readonly AccessKitTreeSnapshot _snapshot;
    private readonly AccessKitNode _node;

    public AccessKitNodeAutomationPeer(VelloSwapChainPanel owner, AccessKitTreeSnapshot snapshot, AccessKitNode node)
    {
        _owner = owner;
        _snapshot = snapshot;
        _node = node;
    }

    protected override string GetClassNameCore() => "VelloNode";

    protected override AutomationControlType GetAutomationControlTypeCore()
        => MapRoleToControlType(_node.Role);

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override string? GetNameCore() => _node.Name;

    protected override Rect GetBoundingRectangleCore()
        => _node.Bounds ?? Rect.Empty;

    protected override bool IsEnabledCore() => !_node.IsDisabled;

    protected override List<AutomationPeer> GetChildrenCore()
    {
        if (_node.Children.IsDefaultOrEmpty)
        {
            return new List<AutomationPeer>();
        }

        var peers = new List<AutomationPeer>(_node.Children.Length);
        foreach (var child in _node.Children)
        {
            peers.Add(new AccessKitNodeAutomationPeer(_owner, _snapshot, child));
        }

        return peers;
    }

    protected override object? GetPatternCore(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.Invoke && SupportsInvoke())
        {
            return this;
        }

        return null;
    }

    public void Invoke()
    {
        if (!SupportsInvoke())
        {
            return;
        }

        _owner.RequestAccessKitAction("click", _node.Id);
    }

    private bool SupportsInvoke()
        => !_node.Actions.IsDefaultOrEmpty && _node.Actions.Any(action => InvokeActions.Contains(action));

    private static AutomationControlType MapRoleToControlType(string role)
        => role switch
        {
            "Button" => AutomationControlType.Button,
            "CheckBox" => AutomationControlType.CheckBox,
            "ComboBox" => AutomationControlType.ComboBox,
            "List" => AutomationControlType.List,
            "ListItem" => AutomationControlType.ListItem,
            "Menu" => AutomationControlType.Menu,
            "MenuItem" => AutomationControlType.MenuItem,
            "MenuBar" => AutomationControlType.MenuBar,
            "ProgressIndicator" => AutomationControlType.ProgressBar,
            "RadioButton" => AutomationControlType.RadioButton,
            "Slider" => AutomationControlType.Slider,
            "ScrollBar" => AutomationControlType.ScrollBar,
            "SpinButton" => AutomationControlType.Spinner,
            "TabList" => AutomationControlType.Tab,
            "Tab" => AutomationControlType.TabItem,
            "Table" => AutomationControlType.Table,
            "Toolbar" => AutomationControlType.ToolBar,
            "Tooltip" => AutomationControlType.ToolTip,
            "Tree" => AutomationControlType.Tree,
            "TreeItem" => AutomationControlType.TreeItem,
            "Link" => AutomationControlType.Hyperlink,
            "Image" => AutomationControlType.Image,
            "Label" => AutomationControlType.Text,
            "TextInput" => AutomationControlType.Edit,
            "Window" => AutomationControlType.Window,
            "Pane" => AutomationControlType.Pane,
            _ => AutomationControlType.Custom
        };
}
