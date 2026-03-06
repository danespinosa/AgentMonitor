namespace AgentMonitor.Providers;

using AgentMonitor.Models;

public interface ISessionProvider
{
    AgentType Type { get; }
    Task<List<AgentSession>> GetSessionsAsync(CancellationToken cancellationToken = default);
}
