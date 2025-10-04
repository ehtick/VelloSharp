using System;
using System.ComponentModel.DataAnnotations;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Dialogs;
using Avalonia.Platform;
using ReactiveUI;

namespace ControlCatalog.ViewModels
{
    class MainWindowViewModel : ReactiveObject
    {
        private WindowState _windowState;
        private WindowState[] _windowStates = Array.Empty<WindowState>();
        private ExtendClientAreaChromeHints _chromeHints = ExtendClientAreaChromeHints.PreferSystemChrome;
        private bool _extendClientAreaEnabled;
        private bool _systemTitleBarEnabled;
        private bool _preferSystemChromeEnabled;
        private double _titleBarHeight;
        private bool _isSystemBarVisible;
        private bool _displayEdgeToEdge;
        private Thickness _safeAreaPadding;
        private bool _canResize;
        private bool _canMinimize;
        private bool _canMaximize;

        public MainWindowViewModel()
        {
            AboutCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var dialog = new AboutAvaloniaDialog();

                if ((App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is { } mainWindow)
                {
                    await dialog.ShowDialog(mainWindow);
                }
            });
            ExitCommand = ReactiveCommand.Create(() =>
            {
                (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            });

            WindowState = WindowState.Normal;

            WindowStates = new WindowState[]
            {
                WindowState.Minimized,
                WindowState.Normal,
                WindowState.Maximized,
                WindowState.FullScreen,
            };

            PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName is nameof(SystemTitleBarEnabled) or nameof(PreferSystemChromeEnabled))
                    {
                        var hints = ExtendClientAreaChromeHints.NoChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;

                        if (SystemTitleBarEnabled)
                        {
                            hints |= ExtendClientAreaChromeHints.SystemChrome;
                        }
                        if (PreferSystemChromeEnabled)
                        {
                            hints |= ExtendClientAreaChromeHints.PreferSystemChrome;
                        }
                        ChromeHints = hints;
                    }
                };

            SystemTitleBarEnabled = true;
            TitleBarHeight = -1;
            CanResize = true;
            CanMinimize = true;
            CanMaximize = true;
        }        
        
        public ExtendClientAreaChromeHints ChromeHints
        {
            get { return _chromeHints; }
            set { this.RaiseAndSetIfChanged(ref _chromeHints, value); }
        }

        public bool ExtendClientAreaEnabled
        {
            get { return _extendClientAreaEnabled; }
            set
            {
                if (this.RaiseAndSetIfChanged(ref _extendClientAreaEnabled, value) && !value)
                {
                    SystemTitleBarEnabled = true;
                }
            }
        }

        public bool SystemTitleBarEnabled
        {
            get { return _systemTitleBarEnabled; }
            set
            {
                if (this.RaiseAndSetIfChanged(ref _systemTitleBarEnabled, value) && !value)
                {
                    TitleBarHeight = -1;
                }
            }
        }

        public bool PreferSystemChromeEnabled
        {
            get { return _preferSystemChromeEnabled; }
            set { this.RaiseAndSetIfChanged(ref _preferSystemChromeEnabled, value); }
        }

        public double TitleBarHeight
        {
            get { return _titleBarHeight; }
            set { this.RaiseAndSetIfChanged(ref _titleBarHeight, value); }
        }

        public WindowState WindowState
        {
            get { return _windowState; }
            set { this.RaiseAndSetIfChanged(ref _windowState, value); }
        }

        public WindowState[] WindowStates
        {
            get { return _windowStates; }
            set { this.RaiseAndSetIfChanged(ref _windowStates, value); }
        }

        public bool IsSystemBarVisible
        {
            get { return _isSystemBarVisible; }
            set { this.RaiseAndSetIfChanged(ref _isSystemBarVisible, value); }
        }

        public bool DisplayEdgeToEdge
        {
            get { return _displayEdgeToEdge; }
            set { this.RaiseAndSetIfChanged(ref _displayEdgeToEdge, value); }
        }
        
        public Thickness SafeAreaPadding
        {
            get { return _safeAreaPadding; }
            set { this.RaiseAndSetIfChanged(ref _safeAreaPadding, value); }
        }

        public bool CanResize
        {
            get { return _canResize; }
            set { this.RaiseAndSetIfChanged(ref _canResize, value); }
        }

        public bool CanMinimize
        {
            get { return _canMinimize; }
            set { this.RaiseAndSetIfChanged(ref _canMinimize, value); }
        }

        public bool CanMaximize
        {
            get { return _canMaximize; }
            set { this.RaiseAndSetIfChanged(ref _canMaximize, value); }
        }


        public ReactiveCommand<Unit, Unit> AboutCommand { get; }

        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        private DateTime? _validatedDateExample;

        /// <summary>
        ///    A required DateTime which should demonstrate validation for the DateTimePicker
        /// </summary>
        [Required]
        public DateTime? ValidatedDateExample
        {
            get => _validatedDateExample;
            set => this.RaiseAndSetIfChanged(ref _validatedDateExample, value);
        }
    }
}
