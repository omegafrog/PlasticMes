using System.Net;
using System.Net.Sockets;
using PlasticMes.MitsubishiSimulator.Adapters.Memory;
using PlasticMes.MitsubishiSimulator.Adapters.Tcp;
using PlasticMes.MitsubishiSimulator.Application;
using PlasticMes.MitsubishiSimulator.Contracts;
using PlasticMes.MitsubishiSimulator.Hosting;

namespace PlasticMesTest;

[TestClass]
public sealed class SlmpSimulatorTests
{
    [TestMethod]
    public void Codec_decodes_batch_read_bit_request()
    {
        var codec = new Slmp3EFrameCodec();
        var frame = CreateBatchReadRequest(DeviceArea.M, 100, 8, SlmpRequestHandler.BitUnitSubcommand);

        var result = codec.Decode(frame);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(SlmpRequestHandler.BatchReadCommand, result.Value.Command);
        Assert.AreEqual(DeviceArea.M, result.Value.Range.Start.Area);
        Assert.AreEqual(100, result.Value.Range.Start.Offset);
        Assert.AreEqual(DeviceUnit.Bit, result.Value.Range.Start.Unit);
        Assert.AreEqual((ushort)8, result.Value.Range.Length);
    }

    [TestMethod]
    public async Task Handler_returns_packed_bit_payload_for_batch_read()
    {
        var store = new InMemoryDeviceMemoryStore();
        var handler = new SlmpRequestHandler(store);
        var write = new DeviceWrite(
            new DeviceRange(new DeviceAddress(DeviceArea.M, 100, DeviceUnit.Bit), 8),
            null,
            new[] { true, false, true, true, false, false, true, false });

        var writeResult = store.Write(write);
        Assert.IsTrue(writeResult.IsSuccess);

        var response = await handler.HandleAsync(
            new SlmpRequest(
                0x00,
                0xFF,
                0x03FF,
                0x00,
                0x0010,
                SlmpRequestHandler.BatchReadCommand,
                SlmpRequestHandler.BitUnitSubcommand,
                write.Range),
            CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x11, 0x00, 0x01 }, response.Data.ToArray());
    }

    [TestMethod]
    public void Store_rejects_out_of_range_access()
    {
        var store = new InMemoryDeviceMemoryStore(bitCapacityPerArea: 8, wordCapacity: 8);

        var result = store.Read(new DeviceRange(new DeviceAddress(DeviceArea.M, 4, DeviceUnit.Bit), 8));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(ProtocolErrorCode.InvalidAddress, result.Error.Code);
    }

    [TestMethod]
    public async Task Handler_returns_error_response_for_out_of_range_write()
    {
        var store = new InMemoryDeviceMemoryStore(wordCapacity: 1);
        var handler = new SlmpRequestHandler(store);

        var response = await handler.HandleAsync(
            new SlmpRequest(
                0x00,
                0xFF,
                0x03FF,
                0x00,
                0x0010,
                SlmpRequestHandler.BatchWriteCommand,
                SlmpRequestHandler.WordUnitSubcommand,
                new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 2),
                new DeviceWrite(
                    new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 2),
                    [0x1000, 0x1001],
                    null)),
            CancellationToken.None);

        Assert.AreEqual((ushort)ProtocolErrorCode.InvalidAddress, response.EndCode);
    }

    [TestMethod]
    public async Task Host_processes_word_write_then_read_over_tcp()
    {
        var store = new InMemoryDeviceMemoryStore();
        var codec = new Slmp3EFrameCodec();
        var handler = new SlmpRequestHandler(store);
        var sessions = new ConnectionSessionStore();
        var host = new MitsubishiSimulatorHost(codec, handler, sessions);

        await host.StartAsync(
            new SimulatorHostConfig(IPAddress.Loopback, 0, 0x00, 0xFF, 0x03FF, 0x00, TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        var port = host.GetHealth().Port;
        Assert.AreEqual(SimulatorHostState.Running, host.GetHealth().State);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var stream = client.GetStream();

        await stream.WriteAsync(CreateBatchWriteWordRequest(DeviceArea.D, 0, [0x1995, 0x1202]));
        var writeResponse = await ReadResponseAsync(stream);
        Assert.AreEqual((ushort)0x0000, ReadUInt16(writeResponse, 9));

        await stream.WriteAsync(CreateBatchReadRequest(DeviceArea.D, 0, 2, SlmpRequestHandler.WordUnitSubcommand));
        var readResponse = await ReadResponseAsync(stream);
        Assert.AreEqual((ushort)0x0000, ReadUInt16(readResponse, 9));
        CollectionAssert.AreEqual(new byte[] { 0x95, 0x19, 0x02, 0x12 }, readResponse[11..]);

        client.Close();
        await Task.Delay(50);
        Assert.AreEqual(0, host.GetHealth().SessionCount);

        await host.StopAsync(CancellationToken.None);
        Assert.AreEqual(SimulatorHostState.Stopped, host.GetHealth().State);
    }

    private static byte[] CreateBatchReadRequest(DeviceArea area, int offset, ushort count, ushort subcommand)
    {
        var frame = new byte[21];
        WriteCommonHeader(frame, 12);
        WriteUInt16(frame, 9, 0x0010);
        WriteUInt16(frame, 11, SlmpRequestHandler.BatchReadCommand);
        WriteUInt16(frame, 13, subcommand);
        WriteDeviceNumber(frame, 15, offset);
        frame[18] = (byte)area;
        WriteUInt16(frame, 19, count);
        return frame;
    }

    private static byte[] CreateBatchWriteWordRequest(DeviceArea area, int offset, IReadOnlyList<ushort> values)
    {
        var frame = new byte[21 + (values.Count * 2)];
        WriteCommonHeader(frame, checked((ushort)(12 + (values.Count * 2))));
        WriteUInt16(frame, 9, 0x0010);
        WriteUInt16(frame, 11, SlmpRequestHandler.BatchWriteCommand);
        WriteUInt16(frame, 13, SlmpRequestHandler.WordUnitSubcommand);
        WriteDeviceNumber(frame, 15, offset);
        frame[18] = (byte)area;
        WriteUInt16(frame, 19, (ushort)values.Count);

        for (var index = 0; index < values.Count; index++)
        {
            WriteUInt16(frame, 21 + (index * 2), values[index]);
        }

        return frame;
    }

    private static void WriteCommonHeader(Span<byte> frame, ushort requestLength)
    {
        WriteUInt16(frame, 0, 0x0050);
        frame[2] = 0x00;
        frame[3] = 0xFF;
        WriteUInt16(frame, 4, 0x03FF);
        frame[6] = 0x00;
        WriteUInt16(frame, 7, requestLength);
    }

    private static void WriteDeviceNumber(Span<byte> frame, int offset, int value)
    {
        frame[offset] = (byte)(value & 0xFF);
        frame[offset + 1] = (byte)((value >> 8) & 0xFF);
        frame[offset + 2] = (byte)((value >> 16) & 0xFF);
    }

    private static async Task<byte[]> ReadResponseAsync(NetworkStream stream)
    {
        var header = await ReadExactAsync(stream, 9);
        var length = ReadUInt16(header, 7);
        var body = await ReadExactAsync(stream, length);
        return [.. header, .. body];
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var chunk = await stream.ReadAsync(buffer.AsMemory(read, count - read));
            if (chunk == 0)
            {
                throw new IOException("Unexpected end of stream.");
            }

            read += chunk;
        }

        return buffer;
    }

    private static ushort ReadUInt16(IReadOnlyList<byte> buffer, int offset) =>
        (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    private static void WriteUInt16(Span<byte> buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)(value >> 8);
    }
}
