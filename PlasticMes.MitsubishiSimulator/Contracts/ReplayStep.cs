namespace PlasticMes.MitsubishiSimulator.Contracts;

public sealed record ReplayStep(TimeSpan Offset, IReadOnlyList<DeviceWrite> Writes, int RowNumber);
