using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public interface ISlmpRequestHandler
{
    Task<SlmpResponse> HandleAsync(SlmpRequest request, CancellationToken ct);
}
