namespace PlasticMes.MitsubishiSimulator.Application;

public interface IConnectionSessionStore
{
    void Open(long id);

    void Close(long id);

    int Count();
}
