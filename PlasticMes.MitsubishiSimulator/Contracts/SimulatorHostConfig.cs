using System.Net;

namespace PlasticMes.MitsubishiSimulator.Contracts;

public sealed record SimulatorHostConfig(
    IPAddress BindAddress,
    int Port,
    byte NetworkNo,
    byte StationNo,
    ushort ModuleIoNo,
    byte MultiDropNo,
    TimeSpan RequestTimeout);
