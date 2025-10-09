namespace VelloSharp.Composition.Controls;

public class ContentControl : TemplatedControl
{
    private CompositionElement? _content;

    public ContentControl()
    {
        Template = CompositionTemplate.Create(static owner =>
        {
            var control = (ContentControl)owner;
            return control.BuildContent();
        });
    }

    public CompositionElement? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value))
            {
                return;
            }

            _content?.Unmount();
            _content = value;
            if (IsMounted)
            {
                _content?.Mount();
            }

            IsTemplateApplied = false;
        }
    }

    protected virtual CompositionElement? BuildContent() => _content;

    public override void Mount()
    {
        base.Mount();
        _content?.Mount();
    }

    public override void Unmount()
    {
        _content?.Unmount();
        base.Unmount();
    }
}
