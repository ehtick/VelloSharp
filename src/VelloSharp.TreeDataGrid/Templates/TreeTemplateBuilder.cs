using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

namespace VelloSharp.TreeDataGrid.Templates;

public static class TreeTemplateBuilder
{
    public static TreeTemplateDefinition<TRow, TreeColumnContext> Row<TRow>(
        Action<TreeRowTemplateBuilder<TRow, TreeColumnContext>> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var builder = new TreeRowTemplateBuilder<TRow, TreeColumnContext>();
        configure(builder);
        return new TreeTemplateDefinition<TRow, TreeColumnContext>(builder.BuildRow());
    }

    public static TreeTemplateDefinition<TRow, TColumn> Row<TRow, TColumn>(
        Action<TreeRowTemplateBuilder<TRow, TColumn>> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var builder = new TreeRowTemplateBuilder<TRow, TColumn>();
        configure(builder);
        return new TreeTemplateDefinition<TRow, TColumn>(builder.BuildRow());
    }
}

public sealed class TreeTemplateDefinition<TRow, TColumn>
{
    internal TreeTemplateDefinition(TreeTemplateExpression expression)
    {
        Expression = expression;
    }

    internal TreeTemplateExpression Expression { get; }

    public TreeCompiledTemplate Compile(TreeTemplateCompiler compiler, in TreeTemplateCompileOptions options)
    {
        if (compiler is null)
        {
            throw new ArgumentNullException(nameof(compiler));
        }

        return compiler.Compile(Expression, options);
    }
}

public sealed class TreeRowTemplateBuilder<TRow, TColumn> : TreeTemplateNodeBuilderBase<TRow, TColumn>
{
    internal TreeRowTemplateBuilder()
        : base(TreeTemplateNodeKind.RowTemplate)
    {
    }

    public TreeRowTemplateBuilder<TRow, TColumn> Pane(
        TreeFrozenKind paneKind,
        Action<TreePaneTemplateBuilder<TRow, TColumn>> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var pane = new TreePaneTemplateBuilder<TRow, TColumn>(paneKind);
        configure(pane);
        AddChild(pane);
        return this;
    }

    public TreeRowTemplateBuilder<TRow, TColumn> PrimaryPane(
        Action<TreePaneTemplateBuilder<TRow, TColumn>> configure)
        => Pane(TreeFrozenKind.None, configure);

    public TreeRowTemplateBuilder<TRow, TColumn> LeadingPane(
        Action<TreePaneTemplateBuilder<TRow, TColumn>> configure)
        => Pane(TreeFrozenKind.Leading, configure);

    public TreeRowTemplateBuilder<TRow, TColumn> TrailingPane(
        Action<TreePaneTemplateBuilder<TRow, TColumn>> configure)
        => Pane(TreeFrozenKind.Trailing, configure);

    internal TreeTemplateExpression BuildRow() => Build();
}

public sealed class TreePaneTemplateBuilder<TRow, TColumn> : TreeTemplateContentNodeBuilder<TRow, TColumn>
{
    internal TreePaneTemplateBuilder(TreeFrozenKind paneKind)
        : base(TreeTemplateNodeKind.PaneTemplate)
    {
        SetString("Pane", TreeTemplatePaneName.From(paneKind));
    }

    public TreePaneTemplateBuilder<TRow, TColumn> Cell(
        string columnKey,
        Action<TreeCellTemplateBuilder<TRow, TColumn>> configure)
    {
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            throw new ArgumentException("Column key must be provided.", nameof(columnKey));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var cell = new TreeCellTemplateBuilder<TRow, TColumn>(columnKey);
        configure(cell);
        AddChild(cell);
        return this;
    }

    public TreePaneTemplateBuilder<TRow, TColumn> Cell(
        uint columnKey,
        Action<TreeCellTemplateBuilder<TRow, TColumn>> configure)
        => Cell(columnKey.ToString(CultureInfo.InvariantCulture), configure);

    public TreePaneTemplateBuilder<TRow, TColumn> Material(uint materialId)
    {
        SetNumber("Material", materialId);
        return this;
    }

    public TreePaneTemplateBuilder<TRow, TColumn> RenderHook(uint renderHookId)
    {
        SetNumber("RenderHook", renderHookId);
        return this;
    }
}

public sealed class TreeCellTemplateBuilder<TRow, TColumn> : TreeTemplateContentNodeBuilder<TRow, TColumn>
{
    internal TreeCellTemplateBuilder(string columnKey)
        : base(TreeTemplateNodeKind.CellTemplate)
    {
        SetString("ColumnKey", columnKey);
    }

    public TreeCellTemplateBuilder<TRow, TColumn> Fallback(TreeFrozenKind paneKind)
    {
        SetString("FallbackPane", TreeTemplatePaneName.From(paneKind));
        return this;
    }

    public TreeCellTemplateBuilder<TRow, TColumn> Material(uint materialId)
    {
        SetNumber("Material", materialId);
        return this;
    }

    public TreeCellTemplateBuilder<TRow, TColumn> RenderHook(uint renderHookId)
    {
        SetNumber("RenderHook", renderHookId);
        return this;
    }
}

public class TreeTemplateContentNodeBuilder<TRow, TColumn> : TreeTemplateNodeBuilderBase<TRow, TColumn>
{
    internal TreeTemplateContentNodeBuilder(TreeTemplateNodeKind kind)
        : base(kind)
    {
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Stack(
        Action<TreeTemplateContentNodeBuilder<TRow, TColumn>> configure)
        => Stack(TreeTemplateStackOrientation.Horizontal, configure);

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Stack(
        TreeTemplateStackOrientation orientation,
        Action<TreeTemplateContentNodeBuilder<TRow, TColumn>> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var stack = new TreeTemplateContentNodeBuilder<TRow, TColumn>(TreeTemplateNodeKind.Stack);
        stack.SetString("Orientation", orientation.ToString());
        configure(stack);
        AddChild(stack);
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Text(
        string content)
        => Text(builder => builder.Content(content));

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Text(
        Action<TreeTemplateLeafBuilder<TRow, TColumn>> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var text = new TreeTemplateLeafBuilder<TRow, TColumn>(TreeTemplateNodeKind.Text);
        configure(text);
        AddChild(text);
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Rectangle(
        Action<TreeTemplateLeafBuilder<TRow, TColumn>> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var rectangle = new TreeTemplateLeafBuilder<TRow, TColumn>(TreeTemplateNodeKind.Rectangle);
        configure(rectangle);
        AddChild(rectangle);
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> ContentPresenter(
        Action<TreeTemplateLeafBuilder<TRow, TColumn>> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var presenter = new TreeTemplateLeafBuilder<TRow, TColumn>(TreeTemplateNodeKind.ContentPresenter);
        configure(presenter);
        AddChild(presenter);
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Property(string propertyName, string value)
    {
        SetString(propertyName, value);
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Property(string propertyName, double value)
    {
        SetNumber(propertyName, value);
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Property(string propertyName, bool value)
    {
        SetBoolean(propertyName, value);
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> RowBinding(
        string propertyName,
        Expression<Func<TRow, object?>> binding)
    {
        if (binding is null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        SetBinding(propertyName, TreeTemplateBindingPath.ForRow(binding));
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> ColumnBinding(
        string propertyName,
        Expression<Func<TColumn, object?>> binding)
    {
        if (binding is null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        SetBinding(propertyName, TreeTemplateBindingPath.ForColumn(binding));
        return this;
    }

    public TreeTemplateContentNodeBuilder<TRow, TColumn> Spacing(double value)
    {
        SetNumber("Spacing", value);
        return this;
    }
}

public sealed class TreeTemplateLeafBuilder<TRow, TColumn> : TreeTemplateNodeBuilderBase<TRow, TColumn>
{
    internal TreeTemplateLeafBuilder(TreeTemplateNodeKind kind)
        : base(kind)
    {
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> Content(string text)
    {
        SetString("Content", text);
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> BindRowContent(
        Expression<Func<TRow, object?>> binding)
    {
        if (binding is null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        SetBinding("Content", TreeTemplateBindingPath.ForRow(binding));
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> BindColumnContent(
        Expression<Func<TColumn, object?>> binding)
    {
        if (binding is null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        SetBinding("Content", TreeTemplateBindingPath.ForColumn(binding));
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> Foreground(string color)
    {
        SetColor("Foreground", color);
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> Background(string color)
    {
        SetColor("Background", color);
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> Width(double value)
    {
        SetNumber("Width", value);
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> Property(string propertyName, string value)
    {
        SetString(propertyName, value);
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> Property(string propertyName, double value)
    {
        SetNumber(propertyName, value);
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> RowBinding(
        string propertyName,
        Expression<Func<TRow, object?>> binding)
    {
        if (binding is null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        SetBinding(propertyName, TreeTemplateBindingPath.ForRow(binding));
        return this;
    }

    public TreeTemplateLeafBuilder<TRow, TColumn> ColumnBinding(
        string propertyName,
        Expression<Func<TColumn, object?>> binding)
    {
        if (binding is null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        SetBinding(propertyName, TreeTemplateBindingPath.ForColumn(binding));
        return this;
    }
}

public enum TreeTemplateStackOrientation
{
    Horizontal,
    Vertical,
}

public readonly record struct TreeColumnContext(string Key, int Index, object? Metadata = null);

internal static class TreeTemplatePaneName
{
    public static string From(TreeFrozenKind kind)
        => kind switch
        {
            TreeFrozenKind.Leading => "Leading",
            TreeFrozenKind.Trailing => "Trailing",
            _ => "Primary",
        };
}

public abstract class TreeTemplateNodeBuilderBase<TRow, TColumn>
{
    private readonly TreeTemplateNodeKind _kind;
    private readonly Dictionary<string, TreeTemplateValue> _properties = new(StringComparer.Ordinal);
    private readonly List<TreeTemplateExpression> _children = new();

    protected TreeTemplateNodeBuilderBase(TreeTemplateNodeKind kind)
    {
        _kind = kind;
    }

    protected void SetString(string propertyName, string value)
        => _properties[propertyName] = new TreeTemplateValue(TreeTemplateValueKind.String, value);

    protected void SetNumber(string propertyName, double value)
        => _properties[propertyName] = new TreeTemplateValue(
            TreeTemplateValueKind.Number,
            value.ToString("G", CultureInfo.InvariantCulture),
            Number: value);

    protected void SetBoolean(string propertyName, bool value)
        => _properties[propertyName] = new TreeTemplateValue(
            TreeTemplateValueKind.Boolean,
            value ? "true" : "false",
            Boolean: value);

    protected void SetColor(string propertyName, string value)
        => _properties[propertyName] = new TreeTemplateValue(TreeTemplateValueKind.Color, value);

    protected void SetBinding(string propertyName, string bindingPath)
        => _properties[propertyName] = new TreeTemplateValue(
            TreeTemplateValueKind.Binding,
            $"{{Binding Path={bindingPath}}}",
            bindingPath);

    protected void AddChild(TreeTemplateNodeBuilderBase<TRow, TColumn> builder)
        => _children.Add(builder.Build());

    internal TreeTemplateExpression Build()
        => new TreeTemplateExpression(_kind, _properties, _children);
}

internal static class TreeTemplateBindingPath
{
    public static string ForRow<TRow, TValue>(Expression<Func<TRow, TValue>> expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        return Build("Row", expression.Body);
    }

    public static string ForColumn<TColumn, TValue>(Expression<Func<TColumn, TValue>> expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        return Build("Column", expression.Body);
    }

    private static string Build(string prefix, Expression expression)
    {
        var segments = new Stack<string>();
        var current = expression;
        while (current is not null)
        {
            switch (current)
            {
                case MemberExpression member:
                    segments.Push(member.Member.Name);
                    current = member.Expression;
                    break;
                case UnaryExpression unary when unary.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked:
                    current = unary.Operand;
                    break;
                case ParameterExpression:
                    current = null;
                    break;
                default:
                    throw new NotSupportedException("Only member access expressions are supported for template bindings.");
            }
        }

        return segments.Count == 0
            ? prefix
            : $"{prefix}.{string.Join('.', segments)}";
    }
}
