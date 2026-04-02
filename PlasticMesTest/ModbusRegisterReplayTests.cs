using System.Net;
using System.Net.Sockets;
using PlasticMes.ModbusSimulator.Adapters.Csv;
using PlasticMes.ModbusSimulator.Adapters.Memory;
using PlasticMes.ModbusSimulator.Adapters.Tcp;
using PlasticMes.ModbusSimulator.Application;
using PlasticMes.ModbusSimulator.Contracts;
using PlasticMes.ModbusSimulator.Hosting;

namespace PlasticMesTest;

[TestClass]
public sealed class ModbusRegisterReplayTests
{
    [TestMethod]
    public async Task Csv_loader_parses_modbus_headers_and_normalizes_offsets()
    {
        var csvPath = await CreateCsvAsync(
            """
            time,C00001,HR40002
            100,1,10
            140,0,11
            """);
        var loader = new CsvReplayScenarioLoader();

        var result = await loader.LoadAsync(csvPath, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Scenario);
        Assert.HasCount(2, result.Scenario.Columns);
        Assert.AreEqual(ModbusArea.Coil, result.Scenario.Columns[0].Address.Area);
        Assert.AreEqual(0, result.Scenario.Columns[0].Address.Offset);
        Assert.AreEqual(1, result.Scenario.Columns[1].Address.Offset);
        Assert.AreEqual(TimeSpan.Zero, result.Scenario.Steps[0].Offset);
        Assert.AreEqual(TimeSpan.FromMilliseconds(40), result.Scenario.Steps[1].Offset);
    }

    [TestMethod]
    public async Task Csv_loader_rejects_reversed_time()
    {
        var csvPath = await CreateCsvAsync(
            """
            time,IR30001
            10,1
            9,2
            """);

        var result = await new CsvReplayScenarioLoader().LoadAsync(csvPath, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage!, "earlier");
    }

    [TestMethod]
    public async Task Replay_controller_updates_read_only_area_via_scenario_path()
    {
        var store = new InMemoryModbusMemoryStore();
        var controller = new RegisterReplayController(store);
        var scenario = new RegisterReplayScenario(
            "memory",
            [new ReplayColumn("IR30001", new ModbusAddress(ModbusArea.InputRegister, 0))],
            [
                new ReplayStep(TimeSpan.Zero, [new ModbusWrite(new ModbusRange(new ModbusAddress(ModbusArea.InputRegister, 0), 1), [7], null)], 2),
                new ReplayStep(TimeSpan.FromMilliseconds(20), [new ModbusWrite(new ModbusRange(new ModbusAddress(ModbusArea.InputRegister, 0), 1), [8], null)], 3),
            ]);

        await controller.StartAsync(scenario, CancellationToken.None);

        await Task.Delay(10);
        Assert.AreEqual((ushort)7, store.Read(new ModbusRange(new ModbusAddress(ModbusArea.InputRegister, 0), 1)).RegisterValues![0]);

        await Task.Delay(40);
        Assert.AreEqual((ushort)8, store.Read(new ModbusRange(new ModbusAddress(ModbusArea.InputRegister, 0), 1)).RegisterValues![0]);
        Assert.IsFalse(controller.GetStatus().IsRunning);
    }

    [TestMethod]
    public async Task Replay_controller_stops_before_later_steps_after_cancellation()
    {
        var delayGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new InMemoryModbusMemoryStore();
        var controller = new RegisterReplayController(
            store,
            (delay, ct) => delay > TimeSpan.Zero ? delayGate.Task.WaitAsync(ct) : Task.CompletedTask);
        var scenario = new RegisterReplayScenario(
            "memory",
            [new ReplayColumn("HR40001", new ModbusAddress(ModbusArea.HoldingRegister, 0))],
            [
                new ReplayStep(TimeSpan.Zero, [new ModbusWrite(new ModbusRange(new ModbusAddress(ModbusArea.HoldingRegister, 0), 1), [1], null)], 2),
                new ReplayStep(TimeSpan.FromMilliseconds(20), [new ModbusWrite(new ModbusRange(new ModbusAddress(ModbusArea.HoldingRegister, 0), 1), [2], null)], 3),
            ]);

        await controller.StartAsync(scenario, CancellationToken.None);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => store.Read(new ModbusRange(new ModbusAddress(ModbusArea.HoldingRegister, 0), 1)).RegisterValues?[0] == 1,
            TimeSpan.FromSeconds(1)));

        await controller.StopAsync(CancellationToken.None);
        delayGate.TrySetResult();

        Assert.AreEqual((ushort)1, store.Read(new ModbusRange(new ModbusAddress(ModbusArea.HoldingRegister, 0), 1)).RegisterValues![0]);
        Assert.AreEqual(1, controller.GetStatus().AppliedStepCount);
    }

    [TestMethod]
    public async Task Host_reads_values_written_by_replay_controller_over_tcp()
    {
        var store = new InMemoryModbusMemoryStore();
        var host = new ModbusSimulatorHost(
            new ModbusTcpFrameCodec(),
            new ModbusRequestHandler(store),
            new ConnectionSessionStore());
        var controller = new RegisterReplayController(store);
        var scenario = new RegisterReplayScenario(
            "memory",
            [new ReplayColumn("HR40001", new ModbusAddress(ModbusArea.HoldingRegister, 0))],
            [
                new ReplayStep(TimeSpan.Zero, [new ModbusWrite(new ModbusRange(new ModbusAddress(ModbusArea.HoldingRegister, 0), 1), [0x1234], null)], 2),
                new ReplayStep(TimeSpan.FromMilliseconds(20), [new ModbusWrite(new ModbusRange(new ModbusAddress(ModbusArea.HoldingRegister, 0), 1), [0x4567], null)], 3),
            ]);

        await host.StartAsync(new ModbusSimulatorHostConfig(IPAddress.Loopback, 0, 1), CancellationToken.None);
        await controller.StartAsync(scenario, CancellationToken.None);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, host.GetHealth().Port);
        using var stream = client.GetStream();

        await Task.Delay(10);
        await stream.WriteAsync(CreateReadRequest(1, 1, offset: 0, quantity: 1));
        var firstResponse = await ReadResponseAsync(stream);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0x02, 0x12, 0x34 }, firstResponse[7..]);

        await Task.Delay(40);
        await stream.WriteAsync(CreateReadRequest(2, 1, offset: 0, quantity: 1));
        var secondResponse = await ReadResponseAsync(stream);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0x02, 0x45, 0x67 }, secondResponse[7..]);

        await controller.StopAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
    }

    private static async Task<string> CreateCsvAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, content.ReplaceLineEndings(Environment.NewLine));
        return path;
    }

    private static byte[] CreateReadRequest(ushort transactionId, byte unitId, ushort offset, ushort quantity)
    {
        var frame = new byte[12];
        WriteUInt16BigEndian(frame, 0, transactionId);
        WriteUInt16BigEndian(frame, 2, 0);
        WriteUInt16BigEndian(frame, 4, 6);
        frame[6] = unitId;
        frame[7] = ModbusRequestHandler.ReadHoldingRegistersFunctionCode;
        WriteUInt16BigEndian(frame, 8, offset);
        WriteUInt16BigEndian(frame, 10, quantity);
        return frame;
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
