namespace PlasticMes.MitsubishiSimulator.Contracts;

public sealed record SlmpRequest(
    byte NetworkNo,
    byte StationNo,
    ushort ModuleIoNo,
    byte MultiDropNo,
    ushort MonitoringTimer,
    ushort Command,
    ushort Subcommand,
    DeviceRange Range,
    DeviceWrite? Write = null);
