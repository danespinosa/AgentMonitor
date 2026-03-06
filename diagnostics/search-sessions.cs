#:project ../AgentMonitor.Core/AgentMonitor.Core.csproj
// Diagnostic: Search sessions by path, branch, or summary text
using AgentMonitor.Models;
using AgentMonitor.Providers;
using AgentMonitor.Services;

var query = args.Length > 0 ? string.Join(" ", args) : null;
if (query is null)
{
    Console.WriteLine("Usage: dotnet run diagnostics/search-sessions.cs -- <search-text>");
    Console.WriteLine("  Searches path, summary, and branch (case-insensitive)");
    Console.WriteLine("  Example: dotnet run diagnostics/search-sessions.cs -- jaws");
    return;
}

var providers = new ISessionProvider[]
{
    new CopilotSessionProvider(),
    new ClaudeSessionProvider()
};
var monitor = new SessionMonitorService(providers);
var sessions = await monitor.GetAllSessionsAsync();

var matches = sessions.Where(s =>
    s.WorkingDirectory.Contains(query, StringComparison.OrdinalIgnoreCase)
    || s.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
    || s.Branch.Contains(query, StringComparison.OrdinalIgnoreCase))
    .ToList();

Console.WriteLine($"Found {matches.Count} session(s) matching \"{query}\":\n");
Console.WriteLine($"{"Status",-12} {"Type",-5} {"Summary",-35} {"Branch",-22} {"Path"}");
Console.WriteLine(new string('-', 120));

foreach (var s in matches)
{
    var summary = s.Summary.Length > 33 ? s.Summary[..30] + "..." : s.Summary;
    if (string.IsNullOrEmpty(summary)) summary = "(no summary)";
    var branch = s.Branch.Length > 20 ? s.Branch[..17] + "..." : s.Branch;
    Console.WriteLine($"{s.Status,-12} {(s.Type == AgentType.CopilotCli ? "GHC" : "CC"),-5} {summary,-35} {branch,-22} {s.WorkingDirectory}");
}
