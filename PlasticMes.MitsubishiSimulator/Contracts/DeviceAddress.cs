namespace PlasticMes.MitsubishiSimulator.Contracts;

public readonly record struct DeviceAddress(DeviceArea Area, int Offset, DeviceUnit Unit)
{
    public bool IsBitDevice => Unit == DeviceUnit.Bit;
}
