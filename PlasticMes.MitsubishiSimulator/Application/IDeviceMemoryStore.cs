using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Application;

public interface IDeviceMemoryStore
{
    ReadResult Read(DeviceRange range);

    WriteResult Write(DeviceWrite write);
}
