using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public sealed record ParseResult<T>(T? Value, SlmpResponse? ErrorResponse)
{
    public bool IsSuccess => ErrorResponse is null;

    public static ParseResult<T> Success(T value) => new(value, null);

    public static ParseResult<T> Failure(SlmpResponse errorResponse) => new(default, errorResponse);
}
