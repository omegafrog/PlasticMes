using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public interface IRegisterReplayController
{
    Task StartAsync(RegisterReplayScenario scenario, CancellationToken ct);

    Task StopAsync(CancellationToken ct);

    ReplayStatusSnapshot GetStatus();
}
