namespace PlasticMes.MitsubishiSimulator.Contracts;

public enum ProtocolErrorCode
{
    None = 0x0000,
    InvalidCommand = 0xC059,
    InvalidAddress = 0xC051,
    DecodeError = 0xC06F,
    ConnectionError = 0xCF00,
}
