using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public interface IReplayScenarioLoader
{
    Task<ReplayScenarioLoadResult> LoadAsync(string csvPath, CancellationToken ct);
}
