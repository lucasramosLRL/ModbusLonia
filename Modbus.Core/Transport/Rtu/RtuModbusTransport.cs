using System.IO.Ports;
using Modbus.Core.Domain.ValueObjects;
using DomainParity = Modbus.Core.Domain.Enums.Parity;
using DomainStopBits = Modbus.Core.Domain.Enums.StopBits;

namespace Modbus.Core.Transport.Rtu;

public class RtuModbusTransport : IModbusTransport
{
    private readonly RtuConfig _config;
    private SerialPort? _port;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Timeout in milliseconds used when reading variable-length responses (expectedResponseLength = 0).
    /// Covers the Modbus inter-frame silence requirement plus USB-serial adapter latency.
    /// </summary>
    private const int VariableLengthReadTimeoutMs = 100;

    public bool IsConnected => _port?.IsOpen ?? false;

    public RtuModbusTransport(RtuConfig config) => _config = config;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _port = new SerialPort(
            _config.PortName,
            _config.BaudRate,
            MapParity(_config.Parity),
            _config.DataBits,
            MapStopBits(_config.StopBits))
        {
            ReadTimeout  = 1000,
            WriteTimeout = 1000
        };
        _port.Open();
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _port?.Close();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a request and reads the response.
    /// When <paramref name="expectedResponseLength"/> is 0, reads until the inter-frame
    /// timeout fires — use this for FC17 (Report Slave ID) whose response length is unknown.
    /// </summary>
    public async Task<byte[]> SendAsync(byte[] request, int expectedResponseLength, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var port = _port ?? throw new InvalidOperationException("Serial port is not open.");

            port.DiscardInBuffer();
            await port.BaseStream.WriteAsync(request, cancellationToken);

            return expectedResponseLength > 0
                ? await ReadExactAsync(port.BaseStream, expectedResponseLength, cancellationToken)
                : await ReadUntilSilenceAsync(port, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        int received = 0;
        while (received < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(received), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("Serial port stream ended unexpectedly.");
            received += read;
        }
        return buffer;
    }

    /// <summary>
    /// Reads bytes until no new data arrives within <see cref="VariableLengthReadTimeoutMs"/>.
    /// Uses polling on <see cref="SerialPort.BytesToRead"/> because
    /// SerialPort.BaseStream.ReadAsync on Windows ignores both CancellationToken and ReadTimeout.
    /// </summary>
    private static async Task<byte[]> ReadUntilSilenceAsync(SerialPort port, CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        var silenceTimer = System.Diagnostics.Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            if (port.BytesToRead > 0)
            {
                var available = port.BytesToRead;
                var temp = new byte[available];
                port.Read(temp, 0, available);
                buffer.AddRange(temp);
                silenceTimer.Restart();
            }
            else if (buffer.Count > 0 && silenceTimer.ElapsedMilliseconds > VariableLengthReadTimeoutMs)
            {
                // Received data + silence detected → frame complete.
                break;
            }
            else if (buffer.Count == 0 && silenceTimer.ElapsedMilliseconds > 1000)
            {
                // No data at all within 1 second → no device at this address.
                break;
            }
            else
            {
                await Task.Delay(5, cancellationToken);
            }
        }

        return [.. buffer];
    }

    private static Parity MapParity(DomainParity parity) => parity switch
    {
        DomainParity.Even => Parity.Even,
        DomainParity.Odd  => Parity.Odd,
        _                 => Parity.None
    };

    private static StopBits MapStopBits(DomainStopBits stopBits) => stopBits switch
    {
        DomainStopBits.Two => StopBits.Two,
        _                  => StopBits.One
    };

    public void Dispose()
    {
        _port?.Dispose();
        _lock.Dispose();
    }
}
