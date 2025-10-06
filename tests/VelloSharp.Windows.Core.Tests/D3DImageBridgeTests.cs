#nullable enable

using System;
using System.Reflection;
using System.Windows;
using VelloSharp;
using VelloSharp.Windows;
using Xunit;
using Xunit.Sdk;

namespace VelloSharp.Windows.Core.Tests;

public class D3DImageBridgeTests
{
    private static Type BridgeType =>
        typeof(VelloSharp.Wpf.Integration.VelloView).Assembly.GetType("VelloSharp.Wpf.Integration.D3DImageBridge", throwOnError: true)!;

    [StaFact]
    public void BeginDrawWithoutSharedTextureReturnsFalse()
    {
        var (instance, disposable) = CreateBridge();
        try
        {
            var beginDraw = BridgeType.GetMethod("BeginDraw", new[] { typeof(uint), typeof(SharedGpuTexture).MakeByRefType() })!;
            var parameters = new object?[] { 0u, null };
            var result = (bool)beginDraw.Invoke(instance, parameters)!;

            Assert.False(result);
            Assert.Null(parameters[1]);
        }
        finally
        {
            disposable.Dispose();
        }
    }

    [StaFact]
    public void EndDrawWithKeyedMutexHeldDoesNotThrow()
    {
        var (instance, disposable) = CreateBridge();
        try
        {
            SetField(instance, "_useKeyedMutex", true);
            SetField(instance, "_writerMutexHeld", true);

            var endDraw = BridgeType.GetMethod("EndDraw", new[] { typeof(bool) })!;
            var exception = Record.Exception(() => endDraw.Invoke(instance, new object[] { true }));

            Assert.Null(exception);
        }
        finally
        {
            disposable.Dispose();
        }
    }

    [StaFact]
    public void PresentWithoutBackBufferReturnsFalse()
    {
        var (instance, disposable) = CreateBridge();
        try
        {
            var present = BridgeType.GetMethod("Present", new[] { typeof(Int32Rect), typeof(bool) })!;
            var result = (bool)present.Invoke(instance, new object[] { new Int32Rect(0, 0, 32, 32), false })!;

            Assert.False(result);
        }
        finally
        {
            disposable.Dispose();
        }
    }

    private static (object Instance, IDisposable Disposable) CreateBridge()
    {
        var diagnostics = new WindowsGpuDiagnostics();
        var instance = Activator.CreateInstance(BridgeType, diagnostics)!;
        return (instance, (IDisposable)instance);
    }

    private static void SetField(object instance, string fieldName, object value)
    {
        var field = BridgeType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            ?? throw new MissingFieldException(BridgeType.FullName, fieldName);
        field.SetValue(instance, value);
    }
}




