namespace PlasticMes.MitsubishiSimulator.Contracts;

public sealed record DeviceWrite(
    DeviceRange Range,
    IReadOnlyList<ushort>? WordValues = null,
    IReadOnlyList<bool>? BitValues = null);
