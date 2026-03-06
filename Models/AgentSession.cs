namespace AgentMonitor.Models;

public enum AgentType
{
    CopilotCli,
    ClaudeCli
}

public enum SessionStatus
{
    Attention,  // Process running, waiting for user input
    Running,    // Process running, actively executing a command
    Idle,       // Process running, no recent activity
    Stopped     // No process running (historical session)
}

public sealed record AgentSession
{
    public required AgentType Type { get; init; }
    public required string Id { get; init; }
    public string Summary { get; init; } = string.Empty;
    public SessionStatus Status { get; set; } = SessionStatus.Idle;
    public string Branch { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public DateTime LastActivity { get; init; } = DateTime.MinValue;
    public string LastEventType { get; init; } = string.Empty;
    public int TotalTodos { get; init; }
    public int DoneTodos { get; init; }
    public int BlockedTodos { get; init; }
    public bool IsRunning { get; set; }
}
