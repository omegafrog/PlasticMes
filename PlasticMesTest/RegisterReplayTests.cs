using System.Net;
using System.Net.Sockets;
using PlasticMes.MitsubishiSimulator.Adapters.Csv;
using PlasticMes.MitsubishiSimulator.Adapters.Memory;
using PlasticMes.MitsubishiSimulator.Adapters.Tcp;
using PlasticMes.MitsubishiSimulator.Application;
using PlasticMes.MitsubishiSimulator.Contracts;
using PlasticMes.MitsubishiSimulator.Hosting;

namespace PlasticMesTest;

[TestClass]
public sealed class RegisterReplayTests
{
    [TestMethod]
    public async Task Csv_loader_parses_columns_and_normalizes_step_offsets()
    {
        var csvPath = await CreateCsvAsync(
            """
            time,M100,D200
            100,1,10
            120,0,11
            """);
        var loader = new CsvReplayScenarioLoader();

        var result = await loader.LoadAsync(csvPath, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Scenario);
        Assert.HasCount(2, result.Scenario.Columns);
        Assert.AreEqual(TimeSpan.Zero, result.Scenario.Steps[0].Offset);
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), result.Scenario.Steps[1].Offset);
        Assert.HasCount(2, result.Scenario.Steps[0].Writes);
        Assert.IsTrue(result.Scenario.Steps[0].Writes[0].BitValues![0]);
        Assert.AreEqual((ushort)10, result.Scenario.Steps[0].Writes[1].WordValues![0]);
    }

    [TestMethod]
    public async Task Csv_loader_rejects_empty_cells()
    {
        var csvPath = await CreateCsvAsync(
            """
            time,M100
            0,
            """);
        var loader = new CsvReplayScenarioLoader();

        var result = await loader.LoadAsync(csvPath, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage!, "is empty");
    }

    [TestMethod]
    public async Task Replay_controller_applies_steps_and_updates_shared_store()
    {
        var store = new InMemoryDeviceMemoryStore();
        var controller = new RegisterReplayController(store);
        var scenario = new RegisterReplayScenario(
            "memory",
            [new ReplayRegisterColumn("D0", new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word))],
            [
                new ReplayStep(TimeSpan.Zero, [new DeviceWrite(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1), [1], null)], 2),
                new ReplayStep(TimeSpan.FromMilliseconds(20), [new DeviceWrite(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1), [2], null)], 3),
            ]);

        await controller.StartAsync(scenario, CancellationToken.None);

        await Task.Delay(10);
        var firstRead = store.Read(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1));
        Assert.IsTrue(firstRead.IsSuccess);
        Assert.AreEqual((ushort)1, firstRead.WordValues![0]);

        await Task.Delay(40);
        var secondRead = store.Read(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1));
        Assert.IsTrue(secondRead.IsSuccess);
        Assert.AreEqual((ushort)2, secondRead.WordValues![0]);

        var status = controller.GetStatus();
        Assert.IsFalse(status.IsRunning);
        Assert.AreEqual(2, status.AppliedStepCount);
        Assert.AreEqual(3, status.CurrentRowNumber);
    }

    [TestMethod]
    public async Task Replay_controller_stops_before_later_steps_after_cancellation()
    {
        var delayGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new InMemoryDeviceMemoryStore();
        var controller = new RegisterReplayController(
            store,
            (delay, ct) => delay > TimeSpan.Zero
                ? delayGate.Task.WaitAsync(ct)
                : Task.CompletedTask);
        var scenario = new RegisterReplayScenario(
            "memory",
            [new ReplayRegisterColumn("D0", new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word))],
            [
                new ReplayStep(TimeSpan.Zero, [new DeviceWrite(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1), [1], null)], 2),
                new ReplayStep(TimeSpan.FromMilliseconds(20), [new DeviceWrite(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1), [2], null)], 3),
            ]);

        await controller.StartAsync(scenario, CancellationToken.None);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => store.Read(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1)).WordValues?[0] == 1,
            TimeSpan.FromSeconds(1)));

        await controller.StopAsync(CancellationToken.None);
        delayGate.TrySetResult();

        var read = store.Read(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1));
        Assert.IsTrue(read.IsSuccess);
        Assert.AreEqual((ushort)1, read.WordValues![0]);

        var status = controller.GetStatus();
        Assert.IsFalse(status.IsRunning);
        Assert.AreEqual(1, status.AppliedStepCount);
    }

    [TestMethod]
    public async Task Host_reads_values_written_by_replay_controller_over_tcp()
    {
        var store = new InMemoryDeviceMemoryStore();
        var codec = new Slmp3EFrameCodec();
        var handler = new SlmpRequestHandler(store);
        var sessions = new ConnectionSessionStore();
        var host = new MitsubishiSimulatorHost(codec, handler, sessions);
        var controller = new RegisterReplayController(store);
        var scenario = new RegisterReplayScenario(
            "memory",
            [new ReplayRegisterColumn("D0", new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word))],
            [
                new ReplayStep(TimeSpan.Zero, [new DeviceWrite(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1), [0x1234], null)], 2),
                new ReplayStep(TimeSpan.FromMilliseconds(20), [new DeviceWrite(new DeviceRange(new DeviceAddress(DeviceArea.D, 0, DeviceUnit.Word), 1), [0x4567], null)], 3),
            ]);

        await host.StartAsync(
            new SimulatorHostConfig(IPAddress.Loopback, 0, 0x00, 0xFF, 0x03FF, 0x00, TimeSpan.FromSeconds(1)),
            CancellationToken.None);
        await controller.StartAsync(scenario, CancellationToken.None);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, host.GetHealth().Port);
        using var stream = client.GetStream();

        await Task.Delay(10);
        await stream.WriteAsync(CreateBatchReadRequest(DeviceArea.D, 0, 1, SlmpRequestHandler.WordUnitSubcommand));
        var firstResponse = await ReadResponseAsync(stream);
        Assert.AreEqual((ushort)0x1234, ReadUInt16(firstResponse, 11));

        await Task.Delay(40);
        await stream.WriteAsync(CreateBatchReadRequest(DeviceArea.D, 0, 1, SlmpRequestHandler.WordUnitSubcommand));
        var secondResponse = await ReadResponseAsync(stream);
        Assert.AreEqual((ushort)0x4567, ReadUInt16(secondResponse, 11));

        await controller.StopAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
    }

    private static async Task<string> CreateCsvAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, content.ReplaceLineEndings(Environment.NewLine));
        return path;
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
