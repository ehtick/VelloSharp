using System.ComponentModel;

namespace VelloSharp.Composition.Accessibility;

public sealed class AccessibilityProperties : INotifyPropertyChanged
{
    private bool _isAccessible = true;
    private string? _name;
    private string? _helpText;
    private AccessibilityRole _role = AccessibilityRole.Custom;
    private AccessibilityLiveSetting _liveSetting = AccessibilityLiveSetting.Off;
    private string? _automationId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsAccessible
    {
        get => _isAccessible;
        set => SetField(ref _isAccessible, value, nameof(IsAccessible));
    }

    public string? Name
    {
        get => _name;
        set => SetField(ref _name, value, nameof(Name));
    }

    public string? HelpText
    {
        get => _helpText;
        set => SetField(ref _helpText, value, nameof(HelpText));
    }

    public AccessibilityRole Role
    {
        get => _role;
        set => SetField(ref _role, value, nameof(Role));
    }

    public AccessibilityLiveSetting LiveSetting
    {
        get => _liveSetting;
        set => SetField(ref _liveSetting, value, nameof(LiveSetting));
    }

    public string? AutomationId
    {
        get => _automationId;
        set => SetField(ref _automationId, value, nameof(AutomationId));
    }

    private void SetField<T>(ref T field, T value, string propertyName)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
