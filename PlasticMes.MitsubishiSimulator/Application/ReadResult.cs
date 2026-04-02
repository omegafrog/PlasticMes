using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public sealed record ReadResult(
    bool IsSuccess,
    IReadOnlyList<bool>? BitValues,
    IReadOnlyList<ushort>? WordValues,
    ProtocolError? Error)
{
    public static ReadResult SuccessBits(IReadOnlyList<bool> values) => new(true, values, null, null);

    public static ReadResult SuccessWords(IReadOnlyList<ushort> values) => new(true, null, values, null);

    public static ReadResult Failure(ProtocolError error) => new(false, null, null, error);
}
