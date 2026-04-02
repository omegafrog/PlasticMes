using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public interface IMitsubishiSimulatorHost
{
    Task StartAsync(SimulatorHostConfig config, CancellationToken ct);

    Task StopAsync(CancellationToken ct);

    SimulatorHealthSnapshot GetHealth();
}
