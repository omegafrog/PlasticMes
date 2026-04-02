using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public interface ISlmpFrameCodec
{
    ParseResult<SlmpRequest> Decode(ReadOnlyMemory<byte> payload);

    ReadOnlyMemory<byte> Encode(SlmpResponse response);
}
