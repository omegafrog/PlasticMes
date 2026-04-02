namespace PlasticMes.MitsubishiSimulator.Contracts;

public sealed record SlmpResponse(
    byte NetworkNo,
    byte StationNo,
    ushort ModuleIoNo,
    byte MultiDropNo,
    ushort EndCode,
    ReadOnlyMemory<byte> Data,
    ProtocolError? Error = null);
