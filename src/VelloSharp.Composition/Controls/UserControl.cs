namespace VelloSharp.Composition.Controls;

public class UserControl : TemplatedControl
{
    private CompositionElement? _content;

    public UserControl()
    {
        Template = CompositionTemplate.Create(static owner =>
        {
            var control = (UserControl)owner;
            return control._content;
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
