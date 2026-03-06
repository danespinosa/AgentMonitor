namespace AgentMonitor.Services;

using AgentMonitor.Models;
using AgentMonitor.Providers;

public sealed class SessionMonitorService
{
    private static readonly TimeSpan RunningThreshold = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(10);

    private readonly IReadOnlyList<ISessionProvider> _providers;

    public SessionMonitorService(IEnumerable<ISessionProvider> providers)
    {
        _providers = providers.ToList();
    }

    public async Task<List<AgentSession>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        // IMPORTANT: Detect file locks BEFORE reading sessions, because reading
        // session.db (SQLite) creates temporary locks that would cause false positives.
        var lockedCopilotIds = ProcessDetector.GetLockedCopilotSessionIds();
        var lockedClaudeIds = ProcessDetector.GetLockedClaudeSessionIds();

        var allSessions = new List<AgentSession>();

        foreach (var provider in _providers)
        {
            var sessions = await provider.GetSessionsAsync(cancellationToken);
            allSessions.AddRange(sessions);
        }

        var now = DateTime.UtcNow;
        foreach (var session in allSessions)
        {
            if (session.Type == AgentType.CopilotCli)
                session.IsRunning = lockedCopilotIds.Contains(session.Id);
            else if (session.Type == AgentType.ClaudeCli)
                session.IsRunning = lockedClaudeIds.Contains(session.Id);

            session.Status = DetermineStatus(session, now);
        }

        // Sort: Attention first, then Running, then Idle, then Stopped
        allSessions.Sort((a, b) =>
        {
            var pri = a.Status.CompareTo(b.Status);
            if (pri != 0) return pri;
            return b.LastActivity.CompareTo(a.LastActivity);
        });

        return allSessions;
    }

    private static SessionStatus DetermineStatus(AgentSession session, DateTime now)
    {
        // No process running → Stopped
        if (!session.IsRunning)
            return SessionStatus.Stopped;

        var elapsed = now - session.LastActivity;

        // Process is running — determine what it's doing
        if (elapsed <= RunningThreshold && IsActiveEvent(session.LastEventType))
        {
            // Recent tool/command activity → Running
            return SessionStatus.Running;
        }

        if (elapsed <= IdleThreshold)
        {
            // Process open, last event was turn_end or similar → Attention (waiting for user)
            return SessionStatus.Attention;
        }

        // Process open but no activity for a while → Idle
        return SessionStatus.Idle;
    }

    private static bool IsActiveEvent(string eventType)
    {
        // Events that indicate the agent is actively working
        return eventType is "tool.execution_start" or "tool.result"
            or "assistant.tool_call" or "tool.execution_end";
    }
}
