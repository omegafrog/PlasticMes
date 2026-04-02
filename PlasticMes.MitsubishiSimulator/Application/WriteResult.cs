using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public sealed record WriteResult(bool IsSuccess, ProtocolError? Error)
{
    public static WriteResult Success() => new(true, null);

    public static WriteResult Failure(ProtocolError error) => new(false, error);
}
