#:project ../AgentMonitor.Core/AgentMonitor.Core.csproj
// Diagnostic: Show which session.db files are locked (= running agents)
using AgentMonitor.Services;

Console.WriteLine("=== Locked Copilot Sessions (running agents) ===");
var lockedIds = ProcessDetector.GetLockedCopilotSessionIds();
Console.WriteLine($"Found {lockedIds.Count} locked session(s):\n");
foreach (var id in lockedIds)
{
    var wsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot", "session-state", id, "workspace.yaml");
    var summary = "(no workspace.yaml)";
    if (File.Exists(wsFile))
    {
        var lines = File.ReadAllLines(wsFile);
        summary = lines.FirstOrDefault(l => l.StartsWith("summary:"))?.Replace("summary:", "").Trim() ?? "(no summary)";
    }
    Console.WriteLine($"  {id}  =>  {summary}");
}

Console.WriteLine("\n=== Locked Claude Sessions ===");
var claudeIds = ProcessDetector.GetLockedClaudeSessionIds();
Console.WriteLine($"Found {claudeIds.Count} locked session(s):");
foreach (var id in claudeIds)
    Console.WriteLine($"  {id}");
