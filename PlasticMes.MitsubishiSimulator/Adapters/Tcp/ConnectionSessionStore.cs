using System.Collections.Concurrent;
using PlasticMes.MitsubishiSimulator.Application;

namespace PlasticMes.MitsubishiSimulator.Adapters.Tcp;

public sealed class ConnectionSessionStore : IConnectionSessionStore
{
    private readonly ConcurrentDictionary<long, byte> sessions = new();

    public void Open(long id) => sessions.TryAdd(id, 0);

    public void Close(long id) => sessions.TryRemove(id, out _);

    public int Count() => sessions.Count;
}
