namespace PlasticMes.MitsubishiSimulator.Contracts;

public sealed record ProtocolError(ProtocolErrorCode Code, string Message);
