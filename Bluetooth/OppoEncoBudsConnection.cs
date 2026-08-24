using System.IO;
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using EncoBudsTray.Models;
using EncoBudsTray.Protocol;

namespace EncoBudsTray.Bluetooth;

/// <summary>
/// Owns one RFCOMM connection and exposes only battery retrieval.
/// </summary>
public sealed class OppoEncoBudsConnection : IAsyncDisposable
{
    private readonly RfcommDeviceService _service;
    private readonly OppoBatteryProtocol _protocol;
    private readonly List<byte> _receiveBuffer = new();

    private StreamSocket? _socket;
    private DataReader? _reader;
    private DataWriter? _writer;

    public OppoEncoBudsConnection(RfcommDeviceService service, OppoBatteryProtocol protocol)
    {
        _service = service;
        _protocol = protocol;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _socket = new StreamSocket();

        try
        {
            await _socket.ConnectAsync(
                    _service.ConnectionHostName,
                    _service.ConnectionServiceName)
                .AsTask(cancellationToken);

            _reader = new DataReader(_socket.InputStream)
            {
                InputStreamOptions = InputStreamOptions.Partial
            };
            _writer = new DataWriter(_socket.OutputStream);
        }
        catch
        {
            _socket.Dispose();
            _socket = null;
            throw;
        }
    }

    public async Task<BatteryState?> ReadBatteryAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (_socket is null || _reader is null)
            throw new InvalidOperationException("Not connected.");

        var request = _protocol.CreateBatteryRequest();

        _writer.WriteBytes(request);
        await _writer.StoreAsync().AsTask(cancellationToken);
        await _writer.FlushAsync().AsTask(cancellationToken);

        var deadline = DateTimeOffset.UtcNow + timeout;
        var chunk = new byte[512];

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readCts.CancelAfter(remaining);

            uint count;
            try
            {
                count = await _reader.LoadAsync((uint)chunk.Length)
                    .AsTask(readCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (count == 0)
                throw new IOException("RFCOMM connection closed.");

            var received = new byte[count];
            _reader.ReadBytes(received);
            _receiveBuffer.AddRange(received);

            var parsed = _protocol.ParseResponses(CollectionsMarshal.AsSpan(_receiveBuffer));
            _receiveBuffer.Clear();
            _receiveBuffer.AddRange(parsed.Remaining);

            if (parsed.Battery is not null)
                return parsed.Battery;
        }

        return null;
    }

    public ValueTask DisposeAsync()
    {
        _reader?.Dispose();
        _reader = null;
        _writer?.Dispose();
        _writer = null;
        _socket?.Dispose();
        _socket = null;
        _service.Dispose();
        return ValueTask.CompletedTask;
    }
}
