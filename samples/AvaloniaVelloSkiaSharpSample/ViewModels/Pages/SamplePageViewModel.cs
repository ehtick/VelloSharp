using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Navigation;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using MiniMvvm;
using SampleSkiaBackendDescriptor = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendDescriptor;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public abstract class SamplePageViewModel : ViewModelBase, ISkiaLeaseRenderer, ISkiaLeaseRendererInvalidation, ISamplePage
{
    private bool _isActive;
    private readonly SkiaCaptureRecorder? _captureRecorder;
    private bool _captureRequested;
    private bool _captureInProgress;
    private string _captureLabel = string.Empty;
    private string _captureStatus = string.Empty;
    private readonly SampleSkiaBackendService? _backendService;
    private SampleSkiaBackendDescriptor _backendDescriptor;
    private IReadOnlyList<DocumentationLink> _documentationLinks = Array.Empty<DocumentationLink>();

    protected SamplePageViewModel(
        string title,
        string description,
        string? icon = null,
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null,
        SkiaResourceService? resourceService = null)
    {
        Title = title;
        Description = description;
        Icon = icon;
        _captureRecorder = captureRecorder;
        CaptureSnapshotCommand = MiniCommand.CreateFromTask(ExecuteCaptureSnapshotAsync);
        ResourceService = resourceService;

        _backendService = backendService;
        if (_backendService is not null)
        {
            _backendDescriptor = _backendService.CurrentDescriptor;
            _backendService.BackendChanged += OnBackendServiceChanged;
        }
        else
        {
            _backendDescriptor = SampleSkiaBackendService.GpuDescriptor;
        }

        ContentFactory = CreateDefaultContentFactory();
    }

    public string Title { get; }

    public string Description { get; }

    public string? Icon { get; }

    public Func<Control> ContentFactory { get; }

    public IReadOnlyList<DocumentationLink> DocumentationLinks => _documentationLinks;

    protected SkiaResourceService? ResourceService { get; }

    public MiniCommand CaptureSnapshotCommand { get; }

    public string CaptureStatus
    {
        get => _captureStatus;
        private set => RaiseAndSetIfChanged(ref _captureStatus, value);
    }

    public event EventHandler? RenderInvalidated;

    public SampleSkiaBackendDescriptor BackendDescriptor
    {
        get => _backendDescriptor;
        private set
        {
            if (_backendDescriptor != value)
            {
                _backendDescriptor = value;
                RaisePropertyChanged();
                OnBackendChanged(value);
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _isActive, value))
            {
                if (value)
                {
                    OnActivated();
                }
                else
                {
                    OnDeactivated();
                }
            }
        }
    }

    public virtual void Render(in SkiaLeaseRenderContext context)
    {
    }

    public virtual void OnActivated()
    {
    }

    public virtual void OnDeactivated()
    {
    }

    protected virtual void OnBackendChanged(SampleSkiaBackendDescriptor descriptor)
    {
        RequestRender();
    }

    protected virtual string CaptureLabel => Title;

    protected void RequestRender() => RenderInvalidated?.Invoke(this, EventArgs.Empty);

    protected bool SetAndRequestRender<T>(ref T field, T value)
    {
        if (RaiseAndSetIfChanged(ref field, value))
        {
            RequestRender();
            return true;
        }

        return false;
    }

    protected void ProcessCapture(in SkiaLeaseRenderContext context)
    {
        if (!_captureRequested || _captureRecorder is null)
        {
            return;
        }

        if (_captureInProgress)
        {
            return;
        }

        _captureRequested = false;
        _captureInProgress = true;
        CaptureStatus = "Saving snapshot…";

        var recorder = _captureRecorder;
        var label = _captureLabel;
        var snapshot = context.Surface.Snapshot();

        try
        {
            var path = recorder.SaveImageAsync(snapshot, label).GetAwaiter().GetResult();
            UpdateCaptureStatus($"Saved {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            UpdateCaptureStatus($"Capture failed: {ex.Message}");
        }
        finally
        {
            snapshot.Dispose();
        }
    }

    private Task ExecuteCaptureSnapshotAsync()
    {
        if (_captureRecorder is null)
        {
            CaptureStatus = "Capture unavailable in this build.";
            return Task.CompletedTask;
        }

        _captureLabel = BuildCaptureLabel();
        _captureRequested = true;
        CaptureStatus = _captureInProgress ? "Capture queued…" : "Capture requested…";
        RequestRender();
        return Task.CompletedTask;
    }

    private void UpdateCaptureStatus(string message)
    {
        void SetStatus()
        {
            CaptureStatus = message;
            _captureInProgress = false;
        }

        try
        {
            var dispatcher = Dispatcher.UIThread;
            if (dispatcher.CheckAccess())
            {
                SetStatus();
            }
            else
            {
                dispatcher.Post(SetStatus, DispatcherPriority.Background);
            }
        }
        catch (InvalidOperationException)
        {
            SetStatus();
        }
    }

    private string BuildCaptureLabel()
    {
        var label = CaptureLabel;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = Title;
        }

        var builder = new StringBuilder(label.Length);
        foreach (var ch in label)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is ' ' or '-' or '_')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }
        }

        return builder.Length == 0 ? "capture" : builder.ToString().Trim('-');
    }

    protected void SetDocumentationLinks(params DocumentationLink[] links)
        => _documentationLinks = links?.Length > 0 ? Array.AsReadOnly(links) : Array.Empty<DocumentationLink>();

    private Func<Control> CreateDefaultContentFactory()
    {
        return () => ViewLocator.BuildStatic(this) ?? new TextBlock
        {
            Text = $"View not found for {GetType().Name}",
        };
    }

    private void OnBackendServiceChanged(object? sender, SampleSkiaBackendDescriptor descriptor)
    {
        BackendDescriptor = descriptor;
    }
}
