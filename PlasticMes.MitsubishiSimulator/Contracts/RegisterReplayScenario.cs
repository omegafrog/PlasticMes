namespace PlasticMes.MitsubishiSimulator.Contracts;

public sealed record RegisterReplayScenario(
    string SourcePath,
    IReadOnlyList<ReplayRegisterColumn> Columns,
    IReadOnlyList<ReplayStep> Steps);
