#:project ../AgentMonitor.Core/AgentMonitor.Core.csproj
// Diagnostic: List all sessions from all providers with their status
using AgentMonitor.Models;
using AgentMonitor.Providers;
using AgentMonitor.Services;

var providers = new ISessionProvider[]
{
    new CopilotSessionProvider(),
    new ClaudeSessionProvider()
};
var monitor = new SessionMonitorService(providers);

var sessions = await monitor.GetAllSessionsAsync();

Console.WriteLine($"{"Status",-12} {"Type",-5} {"Running",-8} {"Summary",-35} {"LastEvent",-25} {"LastActivity"}");
Console.WriteLine(new string('-', 120));

foreach (var s in sessions)
{
    var summary = s.Summary.Length > 33 ? s.Summary[..30] + "..." : s.Summary;
    if (string.IsNullOrEmpty(summary)) summary = "(no summary)";
    Console.WriteLine($"{s.Status,-12} {s.Type,-5} {s.IsRunning,-8} {summary,-35} {s.LastEventType,-25} {s.LastActivity:yyyy-MM-dd HH:mm:ss}");
}

Console.WriteLine($"\nTotal: {sessions.Count}");
Console.WriteLine($"  Attention: {sessions.Count(s => s.Status == SessionStatus.Attention)}");
Console.WriteLine($"  Running:   {sessions.Count(s => s.Status == SessionStatus.Running)}");
Console.WriteLine($"  Idle:      {sessions.Count(s => s.Status == SessionStatus.Idle)}");
Console.WriteLine($"  Stopped:   {sessions.Count(s => s.Status == SessionStatus.Stopped)}");
