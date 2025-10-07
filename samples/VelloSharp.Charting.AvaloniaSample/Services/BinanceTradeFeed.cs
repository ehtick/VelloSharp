using System;
using System.Buffers;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VelloSharp.Charting.AvaloniaSample.Models;

namespace VelloSharp.Charting.AvaloniaSample.Services;

public sealed class BinanceTradeFeed : IAsyncDisposable
{
    private static readonly Uri Endpoint = new("wss://stream.binance.com:9443/ws/btcusdt@trade");

    private readonly ArrayBufferWriter<byte> _messageBuffer = new();
    private ClientWebSocket? _socket;

    public event Action<TradeUpdate>? TradeReceived;
    public event Action<string>? StatusChanged;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(cancellationToken).ConfigureAwait(false);
                await ReceiveLoopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Reconnecting: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _socket?.Dispose();
        _socket = new ClientWebSocket();

        StatusChanged?.Invoke("Connecting to Binance trade stream...");
        await _socket.ConnectAsync(Endpoint, cancellationToken).ConfigureAwait(false);
        StatusChanged?.Invoke("Connected – streaming BTC/USDT trades");
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_socket is null)
        {
            return;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
        try
        {
            while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await _socket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    StatusChanged?.Invoke($"Disconnected ({_socket.CloseStatus})");
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken).ConfigureAwait(false);
                    break;
                }

                _messageBuffer.Write(segment.AsSpan(0, result.Count));
                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(_messageBuffer.WrittenSpan);
                    ProcessMessage(json);
                    _messageBuffer.Clear();
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void ProcessMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("p", out var priceToken) ||
            !root.TryGetProperty("q", out var quantityToken) ||
            !root.TryGetProperty("E", out var eventTimeToken) ||
            !root.TryGetProperty("s", out var symbolToken))
        {
            return;
        }

        if (!double.TryParse(priceToken.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var price))
        {
            return;
        }

        if (!double.TryParse(quantityToken.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity))
        {
            quantity = 0d;
        }

        var eventTime = eventTimeToken.GetInt64();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var latency = Math.Max(0, now - eventTime);

        var update = new TradeUpdate(symbolToken.GetString() ?? "BTCUSDT", price, quantity, eventTime, latency);
        TradeReceived?.Invoke(update);
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket is { State: WebSocketState.Open })
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Ignore socket shutdown failures during disposal.
            }
        }

        _socket?.Dispose();
        _socket = null;
        _messageBuffer.Clear();
    }
}
