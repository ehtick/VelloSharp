using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace VelloSharp.TreeDataGrid.Templates;

internal static class TreeTemplateXamlParser
{
    internal const string DefaultNamespace = "http://schemas.vello.dev/tdg";

    public static TreeTemplateSyntaxTree Parse(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
        {
            throw new ArgumentException("Template XAML cannot be null or empty.", nameof(xaml));
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (Exception ex)
        {
            throw new TreeTemplateParseException("Failed to parse template XAML.", ex);
        }

        if (document.Root is null)
        {
            throw new TreeTemplateParseException("Template XAML does not contain a root element.");
        }

        var root = ParseElement(document.Root);
        return new TreeTemplateSyntaxTree(root);
    }

    private static TreeTemplateSyntaxNode ParseElement(XElement element)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in element.Attributes())
        {
            attributes[attribute.Name.LocalName] = attribute.Value;
        }

        string? textContent = null;
        foreach (var node in element.Nodes())
        {
            if (node is XText text)
            {
                var value = text.Value.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    textContent = value;
                    break;
                }
            }
        }

        var children = element.Elements()
            .Select(ParseElement)
            .ToList();

        return new TreeTemplateSyntaxNode(
            element.Name.LocalName,
            element.Name.NamespaceName,
            attributes,
            children,
            textContent);
    }
}

internal sealed record TreeTemplateSyntaxTree(TreeTemplateSyntaxNode Root);

internal sealed record TreeTemplateSyntaxNode(
    string Name,
    string Namespace,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<TreeTemplateSyntaxNode> Children,
    string? Text);

internal sealed class TreeTemplateParseException : Exception
{
    public TreeTemplateParseException(string message)
        : base(message)
    {
    }

    public TreeTemplateParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public enum TreeTemplateNodeKind
{
    Templates,
    RowTemplate,
    GroupHeaderTemplate,
    SummaryTemplate,
    ChromeTemplate,
    PaneTemplate,
    CellTemplate,
    Stack,
    Text,
    TextBlock = Text,
    Rectangle,
    Image,
    ContentPresenter,
    AccessText,
    TextBox,
    Unknown,
}

public enum TreeTemplateValueKind
{
    String,
    Number,
    Boolean,
    Binding,
    Color,
    Unknown,
}

public sealed record TreeTemplateValue(TreeTemplateValueKind Kind, string Raw, string? BindingPath = null, double? Number = null, bool? Boolean = null);

internal sealed record TreeTemplateExpression(
    TreeTemplateNodeKind Kind,
    IReadOnlyDictionary<string, TreeTemplateValue> Properties,
    IReadOnlyList<TreeTemplateExpression> Children);

internal static class TreeTemplateExpressionBuilder
{
    public static TreeTemplateExpression Build(TreeTemplateSyntaxTree syntaxTree)
    {
        if (syntaxTree.Root.Namespace.Length > 0 &&
            !string.Equals(syntaxTree.Root.Namespace, TreeTemplateXamlParser.DefaultNamespace, StringComparison.Ordinal))
        {
            throw new TreeTemplateParseException(
                $"Unsupported namespace '{syntaxTree.Root.Namespace}'. Expected '{TreeTemplateXamlParser.DefaultNamespace}'.");
        }

        return ConvertNode(syntaxTree.Root);
    }

    private static TreeTemplateExpression ConvertNode(TreeTemplateSyntaxNode node)
    {
        var kind = MapKind(node.Name);
        var properties = new Dictionary<string, TreeTemplateValue>(StringComparer.Ordinal);

        foreach (var entry in node.Attributes)
        {
            properties[entry.Key] = ParseValue(entry.Value);
        }

        if (!string.IsNullOrEmpty(node.Text))
        {
            properties["Content"] = ParseValue(node.Text);
        }

        var children = node.Children
            .Select(ConvertNode)
            .ToArray();

        return new TreeTemplateExpression(kind, properties, children);
    }

    private static TreeTemplateNodeKind MapKind(string name)
        => name switch
        {
            "Templates" => TreeTemplateNodeKind.Templates,
            "RowTemplate" => TreeTemplateNodeKind.RowTemplate,
            "GroupHeaderTemplate" => TreeTemplateNodeKind.GroupHeaderTemplate,
            "SummaryTemplate" => TreeTemplateNodeKind.SummaryTemplate,
            "ChromeTemplate" => TreeTemplateNodeKind.ChromeTemplate,
            "PaneTemplate" => TreeTemplateNodeKind.PaneTemplate,
            "CellTemplate" => TreeTemplateNodeKind.CellTemplate,
            "Stack" or "StackPanel" => TreeTemplateNodeKind.Stack,
            "Text" or "TextBlock" => TreeTemplateNodeKind.Text,
            "AccessText" => TreeTemplateNodeKind.AccessText,
            "TextBox" => TreeTemplateNodeKind.TextBox,
            "Rectangle" => TreeTemplateNodeKind.Rectangle,
            "Image" => TreeTemplateNodeKind.Image,
            "ContentPresenter" => TreeTemplateNodeKind.ContentPresenter,
            _ => TreeTemplateNodeKind.Unknown,
        };

    private static TreeTemplateValue ParseValue(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("{Binding", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            var bindingExpression = trimmed[1..^1]; // strip braces
            var parts = bindingExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var path = parts[1];
                if (path.StartsWith("Path=", StringComparison.Ordinal))
                {
                    path = path[5..];
                }

                return new TreeTemplateValue(TreeTemplateValueKind.Binding, raw, path);
            }

            if (parts.Length == 1)
            {
                return new TreeTemplateValue(TreeTemplateValueKind.Binding, raw, string.Empty);
            }
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return new TreeTemplateValue(TreeTemplateValueKind.Number, trimmed, Number: number);
        }

        if (bool.TryParse(trimmed, out var boolean))
        {
            return new TreeTemplateValue(TreeTemplateValueKind.Boolean, trimmed, Boolean: boolean);
        }

        if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.Contains(",", StringComparison.Ordinal))
        {
            return new TreeTemplateValue(TreeTemplateValueKind.Color, trimmed);
        }

        return new TreeTemplateValue(TreeTemplateValueKind.String, trimmed);
    }
}

public enum TreeTemplateOpCode
{
    OpenNode,
    CloseNode,
    SetProperty,
    BindProperty,
}

public readonly struct TreeTemplateInstruction
{
    public TreeTemplateInstruction(
        TreeTemplateOpCode opCode,
        TreeTemplateNodeKind nodeKind,
        string? propertyName,
        TreeTemplateValue value)
    {
        OpCode = opCode;
        NodeKind = nodeKind;
        PropertyName = propertyName;
        Value = value;
    }

    public TreeTemplateOpCode OpCode { get; }
    public TreeTemplateNodeKind NodeKind { get; }
    public string? PropertyName { get; }
    public TreeTemplateValue Value { get; }
}

internal static class TreeTemplateInstructionEmitter
{
    public static IReadOnlyList<TreeTemplateInstruction> Emit(TreeTemplateExpression expression)
    {
        var buffer = new List<TreeTemplateInstruction>();
        EmitNode(expression, buffer);
        return buffer;
    }

    private static void EmitNode(TreeTemplateExpression expression, List<TreeTemplateInstruction> buffer)
    {
        buffer.Add(new TreeTemplateInstruction(
            TreeTemplateOpCode.OpenNode,
            expression.Kind,
            null,
            new TreeTemplateValue(TreeTemplateValueKind.Unknown, string.Empty)));

        foreach (var property in expression.Properties)
        {
            var op = property.Value.Kind == TreeTemplateValueKind.Binding
                ? TreeTemplateOpCode.BindProperty
                : TreeTemplateOpCode.SetProperty;

            buffer.Add(new TreeTemplateInstruction(
                op,
                expression.Kind,
                property.Key,
                property.Value));
        }

        foreach (var child in expression.Children)
        {
            EmitNode(child, buffer);
        }

        buffer.Add(new TreeTemplateInstruction(
            TreeTemplateOpCode.CloseNode,
            expression.Kind,
            null,
            new TreeTemplateValue(TreeTemplateValueKind.Unknown, string.Empty)));
    }
}
