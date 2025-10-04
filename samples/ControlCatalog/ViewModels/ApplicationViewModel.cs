using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;

namespace ControlCatalog.ViewModels
{
    public class ApplicationViewModel : ReactiveObject
    {
        public ApplicationViewModel()
        {
            ExitCommand = ReactiveCommand.Create(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                {
                    lifetime.Shutdown();
                }
            });

            RestoreDefault = ReactiveCommand.Create(() => { });
        }

        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        public ReactiveCommand<Unit, Unit> RestoreDefault { get; }
    }
}
