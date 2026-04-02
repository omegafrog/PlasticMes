using PlasticMes.MitsubishiSimulator.Application;
using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Adapters.Tcp;

public sealed class Slmp3EFrameCodec : ISlmpFrameCodec
{
    private const ushort RequestSubheader = 0x0050;
    private const ushort ResponseSubheader = 0x00D0;

    public ParseResult<SlmpRequest> Decode(ReadOnlyMemory<byte> payload)
    {
        var buffer = payload.Span;
        if (buffer.Length < 21)
        {
            return ParseResult<SlmpRequest>.Failure(CreateDecodeErrorResponse(0, 0, 0, 0, "Frame is shorter than the 3E binary minimum."));
        }

        var subheader = ReadUInt16(buffer, 0);
        var networkNo = buffer[2];
        var stationNo = buffer[3];
        var moduleIoNo = ReadUInt16(buffer, 4);
        var multiDropNo = buffer[6];
        if (subheader != RequestSubheader)
        {
            return ParseResult<SlmpRequest>.Failure(CreateDecodeErrorResponse(networkNo, stationNo, moduleIoNo, multiDropNo, $"Unsupported subheader 0x{subheader:X4}."));
        }

        var declaredLength = ReadUInt16(buffer, 7);
        if (buffer.Length != declaredLength + 9)
        {
            return ParseResult<SlmpRequest>.Failure(CreateDecodeErrorResponse(networkNo, stationNo, moduleIoNo, multiDropNo, "Declared request length does not match the payload."));
        }

        var monitoringTimer = ReadUInt16(buffer, 9);
        var command = ReadUInt16(buffer, 11);
        var subcommand = ReadUInt16(buffer, 13);

        if (buffer.Length < 21)
        {
            return ParseResult<SlmpRequest>.Failure(CreateDecodeErrorResponse(networkNo, stationNo, moduleIoNo, multiDropNo, "Device access payload is incomplete."));
        }

        try
        {
            var address = new DeviceAddress(
                ResolveDeviceArea(buffer[18], networkNo, stationNo, moduleIoNo, multiDropNo),
                ReadDeviceNumber(buffer, 15),
                ResolveUnit(buffer[18], subcommand));
            var range = new DeviceRange(address, ReadUInt16(buffer, 19));
            var request = new SlmpRequest(networkNo, stationNo, moduleIoNo, multiDropNo, monitoringTimer, command, subcommand, range);

            return command switch
            {
                SlmpRequestHandler.BatchReadCommand => ParseResult<SlmpRequest>.Success(request),
                SlmpRequestHandler.BatchWriteCommand => ParseWriteRequest(buffer, request),
                _ => ParseResult<SlmpRequest>.Success(request),
            };
        }
        catch (InvalidOperationException ex)
        {
            return ParseResult<SlmpRequest>.Failure(CreateDecodeErrorResponse(networkNo, stationNo, moduleIoNo, multiDropNo, ex.Message));
        }
    }

    public ReadOnlyMemory<byte> Encode(SlmpResponse response)
    {
        var dataLength = checked((ushort)(2 + response.Data.Length));
        var frame = new byte[9 + dataLength];

        WriteUInt16(frame, 0, ResponseSubheader);
        frame[2] = response.NetworkNo;
        frame[3] = response.StationNo;
        WriteUInt16(frame, 4, response.ModuleIoNo);
        frame[6] = response.MultiDropNo;
        WriteUInt16(frame, 7, dataLength);
        WriteUInt16(frame, 9, response.EndCode);
        response.Data.Span.CopyTo(frame.AsSpan(11));

        return frame;
    }

    private ParseResult<SlmpRequest> ParseWriteRequest(ReadOnlySpan<byte> buffer, SlmpRequest request)
    {
        var valueOffset = 21;

        try
        {
            if (request.Range.Start.Area == DeviceArea.D)
            {
                if (request.Subcommand != SlmpRequestHandler.WordUnitSubcommand)
                {
                    throw new InvalidOperationException("D supports only word-unit write requests.");
                }

                var values = ParseWordDeviceWrite(buffer[valueOffset..], request.Range.Length);

                return ParseResult<SlmpRequest>.Success(request with
                {
                    Write = new DeviceWrite(request.Range, values, null),
                });
            }

            var bitValues = request.Subcommand switch
            {
                SlmpRequestHandler.BitUnitSubcommand => ParsePackedBitWrite(buffer[valueOffset..], request.Range.Length),
                SlmpRequestHandler.WordUnitSubcommand => ParseBitWordWrite(buffer[valueOffset..], request.Range.Length),
                _ => throw new InvalidOperationException($"Unsupported bit write subcommand 0x{request.Subcommand:X4}."),
            };

            return ParseResult<SlmpRequest>.Success(request with
            {
                Write = new DeviceWrite(request.Range, null, bitValues),
            });
        }
        catch (InvalidOperationException ex)
        {
            return ParseResult<SlmpRequest>.Failure(CreateDecodeErrorResponse(
                request.NetworkNo,
                request.StationNo,
                request.ModuleIoNo,
                request.MultiDropNo,
                ex.Message));
        }
    }

    private static List<ushort> ParseWordDeviceWrite(ReadOnlySpan<byte> data, ushort count)
    {
        if (data.Length != count * sizeof(ushort))
        {
            throw new InvalidOperationException("Word write data length does not match the device count.");
        }

        var values = new List<ushort>(count);
        for (var index = 0; index < count; index++)
        {
            values.Add(ReadUInt16(data, index * sizeof(ushort)));
        }

        return values;
    }

    private static List<bool> ParseBitWordWrite(ReadOnlySpan<byte> data, ushort count)
    {
        var expectedWordBytes = ((count + 15) / 16) * sizeof(ushort);
        if (data.Length != expectedWordBytes)
        {
            throw new InvalidOperationException("Bit word write data length does not match the device count.");
        }

        var values = new List<bool>(count);
        for (var index = 0; index < data.Length; index += sizeof(ushort))
        {
            var word = ReadUInt16(data, index);
            for (var bitIndex = 0; bitIndex < 16 && values.Count < count; bitIndex++)
            {
                values.Add((word & (1 << bitIndex)) != 0);
            }
        }

        return values;
    }

    private static List<bool> ParsePackedBitWrite(ReadOnlySpan<byte> data, ushort count)
    {
        var expectedByteCount = (count + 1) / 2;
        if (data.Length != expectedByteCount)
        {
            throw new InvalidOperationException("Bit write data length does not match the device count.");
        }

        var values = new List<bool>(count);
        for (var index = 0; index < count; index += 2)
        {
            var packed = data[index / 2];
            values.Add((packed & 0x01) == 0x01);
            if (index + 1 < count)
            {
                values.Add((packed & 0x10) == 0x10);
            }
        }

        return values;
    }

    private static DeviceArea ResolveDeviceArea(byte deviceCode, byte networkNo, byte stationNo, ushort moduleIoNo, byte multiDropNo) =>
        deviceCode switch
        {
            (byte)DeviceArea.X => DeviceArea.X,
            (byte)DeviceArea.Y => DeviceArea.Y,
            (byte)DeviceArea.M => DeviceArea.M,
            (byte)DeviceArea.D => DeviceArea.D,
            _ => throw new InvalidOperationException(
                CreateDecodeErrorResponse(networkNo, stationNo, moduleIoNo, multiDropNo, $"Unsupported device code 0x{deviceCode:X2}.").Error!.Message),
        };

    private static DeviceUnit ResolveUnit(byte deviceCode, ushort subcommand)
    {
        if (deviceCode == (byte)DeviceArea.D)
        {
            return DeviceUnit.Word;
        }

        return DeviceUnit.Bit;
    }

    private static int ReadDeviceNumber(ReadOnlySpan<byte> buffer, int offset) =>
        buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);

    private static ushort ReadUInt16(ReadOnlySpan<byte> buffer, int offset) =>
        (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    private static void WriteUInt16(Span<byte> buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)(value >> 8);
    }

    private static SlmpResponse CreateDecodeErrorResponse(
        byte networkNo,
        byte stationNo,
        ushort moduleIoNo,
        byte multiDropNo,
        string message) =>
        new(networkNo, stationNo, moduleIoNo, multiDropNo, (ushort)ProtocolErrorCode.DecodeError, ReadOnlyMemory<byte>.Empty, new ProtocolError(ProtocolErrorCode.DecodeError, message));
}
