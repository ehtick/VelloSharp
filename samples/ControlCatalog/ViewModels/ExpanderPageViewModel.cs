using Avalonia;
using ReactiveUI;

namespace ControlCatalog.ViewModels
{
    public class ExpanderPageViewModel : ReactiveObject
    {
        private object _cornerRadius = AvaloniaProperty.UnsetValue;
        private bool _rounded;

        public object CornerRadius
        {
            get => _cornerRadius;
            private set => this.RaiseAndSetIfChanged(ref _cornerRadius, value);
        }

        public bool Rounded
        {
            get => _rounded;
            set
            {
                if (this.RaiseAndSetIfChanged(ref _rounded, value))
                    CornerRadius = _rounded ? new CornerRadius(25) : AvaloniaProperty.UnsetValue;
            }
        }
    }
}
