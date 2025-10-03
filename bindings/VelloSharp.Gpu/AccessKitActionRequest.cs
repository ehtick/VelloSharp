using System;
using System.Text.Json;

namespace VelloSharp;

public sealed class AccessKitActionRequest : IDisposable
{
    private nint _handle;

    private AccessKitActionRequest(nint handle)
    {
        if (handle == nint.Zero)
        {
            throw new InvalidOperationException("AccessKit action request handle was null.");
        }

        _handle = handle;
    }

    public static AccessKitActionRequest FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        NativeHelpers.ThrowOnError(AccessKitNativeMethods.accesskit_action_request_from_json(json, out var handle), nameof(AccessKitNativeMethods.accesskit_action_request_from_json));
        return new AccessKitActionRequest(handle);
    }

    public static AccessKitActionRequest FromJsonDocument(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return FromJson(document.RootElement.GetRawText());
    }

    public static AccessKitActionRequest FromObject<T>(T payload, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(payload, options ?? AccessKitJson.DefaultSerializerOptions);
        return FromJson(json);
    }

    public string ToJson()
    {
        EnsureNotDisposed();
        NativeHelpers.ThrowOnError(AccessKitNativeMethods.accesskit_action_request_to_json(_handle, out var jsonPtr), nameof(AccessKitNativeMethods.accesskit_action_request_to_json));
        return ConsumeString(jsonPtr);
    }

    public JsonDocument ToJsonDocument()
    {
        var json = ToJson();
        return JsonDocument.Parse(json);
    }

    public T ToObject<T>(JsonSerializerOptions? options = null)
    {
        var json = ToJson();
        return JsonSerializer.Deserialize<T>(json, options ?? AccessKitJson.DefaultSerializerOptions) ?? throw new InvalidOperationException("Failed to deserialize AccessKit action request.");
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            AccessKitNativeMethods.accesskit_action_request_destroy(_handle);
            _handle = nint.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~AccessKitActionRequest()
    {
        Dispose();
    }

    internal nint DangerousGetHandle()
    {
        EnsureNotDisposed();
        return _handle;
    }

    private static string ConsumeString(nint ptr)
    {
        if (ptr == nint.Zero)
        {
            return string.Empty;
        }

        try
        {
            return NativeHelpers.GetUtf8String(ptr);
        }
        finally
        {
            AccessKitNativeMethods.accesskit_string_free(ptr);
        }
    }

    private void EnsureNotDisposed()
    {
        if (_handle == nint.Zero)
        {
            throw new ObjectDisposedException(nameof(AccessKitActionRequest));
        }
    }
}
