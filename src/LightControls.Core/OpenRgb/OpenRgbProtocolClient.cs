using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using LightControls.Core.Models;

namespace LightControls.Core.OpenRgb;

public sealed class OpenRgbProtocolClient : IAsyncDisposable
{
    private const uint PacketRequestControllerCount = 0;
    private const uint PacketRequestControllerData = 1;
    private const uint PacketRequestProtocolVersion = 40;
    private const uint PacketSetClientName = 50;
    private const uint PacketUpdateLeds = 1050;
    private const uint PacketSetCustomMode = 1100;
    private const uint ClientProtocolVersion = 1;
    private const int HeaderLength = 16;

    private readonly TcpClient _tcpClient = new();
    private NetworkStream? _stream;
    private uint _protocolVersion;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await _tcpClient.ConnectAsync(host, port, cancellationToken);
        _stream = _tcpClient.GetStream();
        await NegotiateProtocolAsync(cancellationToken);
        await SetClientNameAsync("Light Controls", cancellationToken);
    }

    public async Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var countPacket = await SendAndReceiveAsync(PacketRequestControllerCount, 0, [], cancellationToken);
        var reader = new OpenRgbProtocolReader(countPacket.Data);
        var count = reader.ReadUInt32();
        var devices = new List<RgbDevice>();

        for (uint controllerIndex = 0; controllerIndex < count; controllerIndex++)
        {
            var payload = _protocolVersion == 0 ? [] : UInt32Payload(_protocolVersion);
            var dataPacket = await SendAndReceiveAsync(PacketRequestControllerData, controllerIndex, payload, cancellationToken);
            devices.Add(ParseDevice(controllerIndex, dataPacket.Data));
        }

        return devices;
    }

    public async Task ApplyColorAsync(int controllerIndex, int ledCount, RgbColor color, CancellationToken cancellationToken = default)
    {
        if (ledCount <= 0)
        {
            throw new InvalidOperationException("Device does not report controllable LEDs.");
        }

        await SendAsync(PacketSetCustomMode, (uint)controllerIndex, [], cancellationToken);

        var payload = new byte[4 + 2 + ledCount * 4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), (uint)payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), (ushort)ledCount);

        var openRgbColor = color.ToOpenRgbColor();
        for (var index = 0; index < ledCount; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(6 + index * 4, 4), openRgbColor);
        }

        await SendAsync(PacketUpdateLeds, (uint)controllerIndex, payload, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _tcpClient.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task NegotiateProtocolAsync(CancellationToken cancellationToken)
    {
        await SendAsync(PacketRequestProtocolVersion, 0, UInt32Payload(ClientProtocolVersion), cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(750));

        try
        {
            var response = await ReceiveAsync(timeout.Token);
            var reader = new OpenRgbProtocolReader(response.Data);
            _protocolVersion = Math.Min(ClientProtocolVersion, reader.ReadUInt32());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _protocolVersion = 0;
        }
    }

    private async Task SetClientNameAsync(string name, CancellationToken cancellationToken)
    {
        await SendAsync(PacketSetClientName, 0, Encoding.UTF8.GetBytes(name + "\0"), cancellationToken);
    }

    private async Task<OpenRgbPacket> SendAndReceiveAsync(uint packetId, uint deviceIndex, byte[] data, CancellationToken cancellationToken)
    {
        await SendAsync(packetId, deviceIndex, data, cancellationToken);
        return await ReceiveAsync(cancellationToken);
    }

    private async Task SendAsync(uint packetId, uint deviceIndex, byte[] data, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("OpenRGB client is not connected.");
        }

        var packet = new byte[HeaderLength + data.Length];
        Encoding.ASCII.GetBytes("ORGB", packet);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4, 4), deviceIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8, 4), packetId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12, 4), (uint)data.Length);
        data.CopyTo(packet.AsSpan(HeaderLength));

        await _stream.WriteAsync(packet, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    private async Task<OpenRgbPacket> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("OpenRGB client is not connected.");
        }

        var header = await ReadExactAsync(HeaderLength, cancellationToken);
        if (Encoding.ASCII.GetString(header, 0, 4) != "ORGB")
        {
            throw new InvalidDataException("OpenRGB server returned an invalid packet header.");
        }

        var deviceIndex = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
        var packetId = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4));
        var size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12, 4));
        var data = await ReadExactAsync(checked((int)size), cancellationToken);

        return new OpenRgbPacket(deviceIndex, packetId, data);
    }

    private async Task<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("OpenRGB client is not connected.");
        }

        var data = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await _stream.ReadAsync(data.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("OpenRGB server closed the connection.");
            }

            offset += read;
        }

        return data;
    }

    private RgbDevice ParseDevice(uint controllerIndex, byte[] data)
    {
        var reader = new OpenRgbProtocolReader(data);
        _ = reader.ReadUInt32();
        _ = reader.ReadInt32();
        var name = reader.ReadString();
        var vendor = _protocolVersion >= 1 ? reader.ReadString() : string.Empty;
        var description = reader.ReadString();
        _ = reader.ReadString();
        var serial = reader.ReadString();
        var location = reader.ReadString();

        var modeCount = reader.ReadUInt16();
        _ = reader.ReadInt32();
        for (var mode = 0; mode < modeCount; mode++)
        {
            SkipMode(reader);
        }

        var zoneCount = reader.ReadUInt16();
        for (var zone = 0; zone < zoneCount; zone++)
        {
            SkipZone(reader);
        }

        var ledCount = reader.ReadUInt16();
        for (var led = 0; led < ledCount; led++)
        {
            _ = reader.ReadString();
            _ = reader.ReadUInt32();
        }

        var colorCount = reader.ReadUInt16();
        reader.Skip(colorCount * 4);

        var isSupported = ledCount > 0;
        var id = CreateStableId(controllerIndex, vendor, name, serial, location);
        return new RgbDevice(
            id,
            checked((int)controllerIndex),
            name,
            vendor,
            description,
            serial,
            location,
            ledCount,
            isSupported,
            isSupported ? "Ready" : "No controllable LEDs reported");
    }

    private void SkipMode(OpenRgbProtocolReader reader)
    {
        _ = reader.ReadString();
        reader.Skip(4 + 4 + 4 + 4);
        reader.Skip(4 + 4 + 4 + 4);
        var colorCount = reader.ReadUInt16();
        reader.Skip(colorCount * 4);
    }

    private void SkipZone(OpenRgbProtocolReader reader)
    {
        _ = reader.ReadString();
        reader.Skip(4 + 4 + 4 + 4);
        var matrixLength = reader.ReadUInt16();
        if (matrixLength > 0)
        {
            reader.Skip(matrixLength);
        }
    }

    private static string CreateStableId(uint controllerIndex, string vendor, string name, string serial, string location)
    {
        var stablePart = string.Join('|', [vendor, name, serial, location]).Trim('|');
        return string.IsNullOrWhiteSpace(stablePart)
            ? $"openrgb:{controllerIndex}"
            : $"openrgb:{stablePart}";
    }

    private static byte[] UInt32Payload(uint value)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, value);
        return payload;
    }
}
