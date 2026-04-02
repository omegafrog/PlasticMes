using System.Net;
using System.Net.Sockets;
using PlasticMes.ModbusSimulator.Adapters.Memory;
using PlasticMes.ModbusSimulator.Adapters.Tcp;
using PlasticMes.ModbusSimulator.Application;
using PlasticMes.ModbusSimulator.Contracts;
using PlasticMes.ModbusSimulator.Hosting;

namespace PlasticMesTest;

[TestClass]
public sealed class ModbusSimulatorTests
{
    [TestMethod]
    public void Codec_decodes_read_holding_registers_request()
    {
        var codec = new ModbusTcpFrameCodec();
        var frame = CreateReadRequest(transactionId: 3, unitId: 7, ModbusRequestHandler.ReadHoldingRegistersFunctionCode, offset: 10, quantity: 2);

        var result = codec.Decode(frame);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual((ushort)3, result.Value.TransactionId);
        Assert.AreEqual((byte)7, result.Value.UnitId);
        Assert.AreEqual(ModbusArea.HoldingRegister, result.Value.Range.Start.Area);
        Assert.AreEqual(10, result.Value.Range.Start.Offset);
        Assert.AreEqual((ushort)2, result.Value.Range.Length);
    }

    [TestMethod]
    public void Codec_encodes_exception_response_with_high_bit()
    {
        var codec = new ModbusTcpFrameCodec();
        var response = new ModbusResponse(
            TransactionId: 1,
            UnitId: 1,
            FunctionCode: ModbusRequestHandler.ReadCoilsFunctionCode,
            Data: ReadOnlyMemory<byte>.Empty,
            ExceptionCode: (byte)ModbusExceptionCode.IllegalDataAddress,
            Error: new ModbusProtocolError(ModbusExceptionCode.IllegalDataAddress, "bad range"));

        var frame = codec.Encode(response).ToArray();

        Assert.AreEqual((byte)0x81, frame[7]);
        Assert.AreEqual((byte)ModbusExceptionCode.IllegalDataAddress, frame[8]);
    }

    [TestMethod]
    public void Store_rejects_client_writes_to_read_only_area()
    {
        var store = new InMemoryModbusMemoryStore();

        var result = store.WriteClient(new ModbusWrite(
            new ModbusRange(new ModbusAddress(ModbusArea.InputRegister, 0), 1),
            [123],
            null));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(ModbusExceptionCode.IllegalFunction, result.Error.ExceptionCode);
    }

    [TestMethod]
    public async Task Handler_returns_register_payload_for_read_request()
    {
        var store = new InMemoryModbusMemoryStore();
        var writeResult = store.WriteScenario(new ModbusWrite(
            new ModbusRange(new ModbusAddress(ModbusArea.HoldingRegister, 5), 2),
            [0x1234, 0x4567],
            null));
        Assert.IsTrue(writeResult.IsSuccess);

        var handler = new ModbusRequestHandler(store);
        var response = await handler.HandleAsync(
            new ModbusRequest(
                TransactionId: 1,
                UnitId: 1,
                FunctionCode: ModbusRequestHandler.ReadHoldingRegistersFunctionCode,
                Range: new ModbusRange(new ModbusAddress(ModbusArea.HoldingRegister, 5), 2)),
            CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x04, 0x12, 0x34, 0x45, 0x67 }, response.Data.ToArray());
    }

    [TestMethod]
    public async Task Host_processes_write_then_read_over_tcp()
    {
        var store = new InMemoryModbusMemoryStore();
        var host = new ModbusSimulatorHost(
            new ModbusTcpFrameCodec(),
            new ModbusRequestHandler(store),
            new ConnectionSessionStore());

        await host.StartAsync(new ModbusSimulatorHostConfig(IPAddress.Loopback, 0, 1), CancellationToken.None);
        Assert.AreEqual(SimulatorHostState.Running, host.GetHealth().State);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, host.GetHealth().Port);
        using var stream = client.GetStream();

        await stream.WriteAsync(CreateWriteSingleRegisterRequest(1, 1, offset: 4, value: 0x2222));
        var writeResponse = await ReadResponseAsync(stream);
        Assert.AreEqual((byte)ModbusRequestHandler.WriteSingleRegisterFunctionCode, writeResponse[7]);
        Assert.AreEqual((ushort)4, ReadUInt16BigEndian(writeResponse, 8));

        await stream.WriteAsync(CreateReadRequest(2, 1, ModbusRequestHandler.ReadHoldingRegistersFunctionCode, offset: 4, quantity: 1));
        var readResponse = await ReadResponseAsync(stream);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0x02, 0x22, 0x22 }, readResponse[7..]);

        client.Close();
        await Task.Delay(50);
        Assert.AreEqual(0, host.GetHealth().SessionCount);

        await host.StopAsync(CancellationToken.None);
    }

    private static byte[] CreateReadRequest(ushort transactionId, byte unitId, byte functionCode, ushort offset, ushort quantity)
    {
        var frame = new byte[12];
        WriteMbap(frame, transactionId, unitId, 6);
        frame[7] = functionCode;
        WriteUInt16BigEndian(frame, 8, offset);
        WriteUInt16BigEndian(frame, 10, quantity);
        return frame;
    }

    private static byte[] CreateWriteSingleRegisterRequest(ushort transactionId, byte unitId, ushort offset, ushort value)
    {
        var frame = new byte[12];
        WriteMbap(frame, transactionId, unitId, 6);
        frame[7] = ModbusRequestHandler.WriteSingleRegisterFunctionCode;
        WriteUInt16BigEndian(frame, 8, offset);
        WriteUInt16BigEndian(frame, 10, value);
        return frame;
    }

    private static void WriteMbap(Span<byte> frame, ushort transactionId, byte unitId, ushort length)
    {
        WriteUInt16BigEndian(frame, 0, transactionId);
        WriteUInt16BigEndian(frame, 2, 0);
        WriteUInt16BigEndian(frame, 4, length);
        frame[6] = unitId;
    }

    private static async Task<byte[]> ReadResponseAsync(NetworkStream stream)
    {
        var header = await ReadExactAsync(stream, 7);
        var length = ReadUInt16BigEndian(header, 4);
        var body = await ReadExactAsync(stream, length - 1);
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

    private static ushort ReadUInt16BigEndian(IReadOnlyList<byte> buffer, int offset) =>
        (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

    private static void WriteUInt16BigEndian(Span<byte> buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }
}
