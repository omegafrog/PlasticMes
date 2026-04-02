using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public sealed class SlmpRequestHandler(IDeviceMemoryStore memoryStore) : ISlmpRequestHandler
{
    public const ushort BatchReadCommand = 0x0401;
    public const ushort BatchWriteCommand = 0x1401;
    public const ushort WordUnitSubcommand = 0x0000;
    public const ushort BitUnitSubcommand = 0x0001;

    public Task<SlmpResponse> HandleAsync(SlmpRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(request.Command switch
        {
            BatchReadCommand => HandleRead(request),
            BatchWriteCommand => HandleWrite(request),
            _ => CreateErrorResponse(request, ProtocolErrorCode.InvalidCommand, $"Unsupported command 0x{request.Command:X4}."),
        });
    }

    private SlmpResponse HandleRead(SlmpRequest request)
    {
        var result = memoryStore.Read(request.Range);
        if (!result.IsSuccess)
        {
            return CreateErrorResponse(request, result.Error!);
        }

        var payload = EncodeReadPayload(request.Range, request.Subcommand, result);
        if (payload is null)
        {
            return CreateErrorResponse(
                request,
                ProtocolErrorCode.InvalidCommand,
                $"Unsupported subcommand 0x{request.Subcommand:X4} for {request.Range.Start.Area}.");
        }

        return CreateSuccessResponse(request, payload);
    }

    private SlmpResponse HandleWrite(SlmpRequest request)
    {
        if (request.Write is null)
        {
            return CreateErrorResponse(request, ProtocolErrorCode.DecodeError, "Write request body is missing.");
        }

        var result = memoryStore.Write(request.Write);
        return result.IsSuccess
            ? CreateSuccessResponse(request, ReadOnlyMemory<byte>.Empty)
            : CreateErrorResponse(request, result.Error!);
    }

    private static byte[]? EncodeReadPayload(DeviceRange range, ushort subcommand, ReadResult result)
    {
        if (range.Start.Unit == DeviceUnit.Word)
        {
            if (subcommand != WordUnitSubcommand || result.WordValues is null)
            {
                return null;
            }

            var payload = new byte[result.WordValues.Count * sizeof(ushort)];
            for (var index = 0; index < result.WordValues.Count; index++)
            {
                var offset = index * sizeof(ushort);
                payload[offset] = (byte)(result.WordValues[index] & 0xFF);
                payload[offset + 1] = (byte)(result.WordValues[index] >> 8);
            }

            return payload;
        }

        if (result.BitValues is null)
        {
            return null;
        }

        return subcommand switch
        {
            BitUnitSubcommand => EncodePackedBitNibbles(result.BitValues),
            WordUnitSubcommand => EncodeBitWords(result.BitValues),
            _ => null,
        };
    }

    internal static byte[] EncodePackedBitNibbles(IReadOnlyList<bool> values)
    {
        var payload = new byte[(values.Count + 1) / 2];
        for (var index = 0; index < values.Count; index += 2)
        {
            var low = values[index] ? 0x01 : 0x00;
            var high = index + 1 < values.Count && values[index + 1] ? 0x10 : 0x00;
            payload[index / 2] = (byte)(low | high);
        }

        return payload;
    }

    internal static byte[] EncodeBitWords(IReadOnlyList<bool> values)
    {
        var wordCount = (values.Count + 15) / 16;
        var payload = new byte[wordCount * sizeof(ushort)];
        for (var wordIndex = 0; wordIndex < wordCount; wordIndex++)
        {
            ushort word = 0;
            for (var bitIndex = 0; bitIndex < 16; bitIndex++)
            {
                var sourceIndex = (wordIndex * 16) + bitIndex;
                if (sourceIndex < values.Count && values[sourceIndex])
                {
                    word |= (ushort)(1 << bitIndex);
                }
            }

            var offset = wordIndex * sizeof(ushort);
            payload[offset] = (byte)(word & 0xFF);
            payload[offset + 1] = (byte)(word >> 8);
        }

        return payload;
    }

    private static SlmpResponse CreateSuccessResponse(SlmpRequest request, ReadOnlyMemory<byte> data) =>
        new(request.NetworkNo, request.StationNo, request.ModuleIoNo, request.MultiDropNo, 0x0000, data);

    private static SlmpResponse CreateErrorResponse(SlmpRequest request, ProtocolError error) =>
        new(request.NetworkNo, request.StationNo, request.ModuleIoNo, request.MultiDropNo, (ushort)error.Code, ReadOnlyMemory<byte>.Empty, error);

    private static SlmpResponse CreateErrorResponse(SlmpRequest request, ProtocolErrorCode code, string message) =>
        CreateErrorResponse(request, new ProtocolError(code, message));
}
