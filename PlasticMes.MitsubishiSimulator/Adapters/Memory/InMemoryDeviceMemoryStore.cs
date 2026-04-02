using PlasticMes.MitsubishiSimulator.Application;
using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Adapters.Memory;

public sealed class InMemoryDeviceMemoryStore(
    int bitCapacityPerArea = 4096,
    int wordCapacity = 4096) : IDeviceMemoryStore
{
    private readonly bool[] x = new bool[bitCapacityPerArea];
    private readonly bool[] y = new bool[bitCapacityPerArea];
    private readonly bool[] m = new bool[bitCapacityPerArea];
    private readonly ushort[] d = new ushort[wordCapacity];
    private readonly Lock gate = new();

    public ReadResult Read(DeviceRange range)
    {
        var validation = ValidateRange(range);
        if (validation is not null)
        {
            return ReadResult.Failure(validation);
        }

        lock (gate)
        {
            if (range.Start.Unit == DeviceUnit.Word)
            {
                var source = d.AsSpan(range.Start.Offset, range.Length);
                return ReadResult.SuccessWords(source.ToArray());
            }

            var sourceBits = ResolveBitArea(range.Start.Area).AsSpan(range.Start.Offset, range.Length);
            return ReadResult.SuccessBits(sourceBits.ToArray());
        }
    }

    public WriteResult Write(DeviceWrite write)
    {
        var validation = ValidateWrite(write);
        if (validation is not null)
        {
            return WriteResult.Failure(validation);
        }

        lock (gate)
        {
            if (write.Range.Start.Unit == DeviceUnit.Word)
            {
                for (var index = 0; index < write.Range.Length; index++)
                {
                    d[write.Range.Start.Offset + index] = write.WordValues![index];
                }

                return WriteResult.Success();
            }

            var target = ResolveBitArea(write.Range.Start.Area);
            for (var index = 0; index < write.Range.Length; index++)
            {
                target[write.Range.Start.Offset + index] = write.BitValues![index];
            }

            return WriteResult.Success();
        }
    }

    private ProtocolError? ValidateWrite(DeviceWrite write)
    {
        var validation = ValidateRange(write.Range);
        if (validation is not null)
        {
            return validation;
        }

        if (write.Range.Start.Unit == DeviceUnit.Word)
        {
            if (write.WordValues is null || write.WordValues.Count != write.Range.Length)
            {
                return new ProtocolError(ProtocolErrorCode.DecodeError, "Word payload length does not match device count.");
            }
        }
        else if (write.BitValues is null || write.BitValues.Count != write.Range.Length)
        {
            return new ProtocolError(ProtocolErrorCode.DecodeError, "Bit payload length does not match device count.");
        }

        return null;
    }

    private ProtocolError? ValidateRange(DeviceRange range)
    {
        if (range.Length == 0)
        {
            return new ProtocolError(ProtocolErrorCode.InvalidAddress, "Device count must be greater than zero.");
        }

        if (range.Start.Offset < 0)
        {
            return new ProtocolError(ProtocolErrorCode.InvalidAddress, "Device offset must not be negative.");
        }

        if (range.Start.Unit == DeviceUnit.Word && range.Start.Area != DeviceArea.D)
        {
            return new ProtocolError(ProtocolErrorCode.InvalidAddress, "Only D supports word access.");
        }

        if (range.Start.Unit == DeviceUnit.Bit && range.Start.Area == DeviceArea.D)
        {
            return new ProtocolError(ProtocolErrorCode.InvalidAddress, "D supports only word access.");
        }

        var capacity = range.Start.Unit == DeviceUnit.Word
            ? d.Length
            : ResolveBitArea(range.Start.Area).Length;
        if (range.Start.Offset + range.Length > capacity)
        {
            return new ProtocolError(ProtocolErrorCode.InvalidAddress, "Requested device range exceeds the configured memory.");
        }

        return null;
    }

    private bool[] ResolveBitArea(DeviceArea area) => area switch
    {
        DeviceArea.X => x,
        DeviceArea.Y => y,
        DeviceArea.M => m,
        _ => throw new InvalidOperationException($"Area {area} does not support bit access."),
    };
}
