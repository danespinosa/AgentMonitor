#:project ../AgentMonitor.Core/AgentMonitor.Core.csproj
// Diagnostic: Read raw data from a specific Copilot session
using AgentMonitor.Providers;

var sessionId = args.Length > 0 ? args[0] : null;
var basePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".copilot", "session-state");

if (sessionId is null)
{
    Console.WriteLine("Usage: dotnet run diagnostics/inspect-session.cs -- <session-id>");
    Console.WriteLine("\nAvailable sessions (most recent first):");
    var dirs = Directory.GetDirectories(basePath)
        .Select(d => new { Dir = d, Events = Path.Combine(d, "events.jsonl") })
        .Where(d => File.Exists(d.Events))
        .OrderByDescending(d => File.GetLastWriteTimeUtc(d.Events))
        .Take(10);

    foreach (var d in dirs)
    {
        var id = Path.GetFileName(d.Dir);
        var wsFile = Path.Combine(d.Dir, "workspace.yaml");
        var summary = "";
        if (File.Exists(wsFile))
        {
            var lines = File.ReadAllLines(wsFile);
            summary = lines.FirstOrDefault(l => l.StartsWith("summary:"))?.Replace("summary:", "").Trim() ?? "";
        }
        var mtime = File.GetLastWriteTimeUtc(d.Events);
        Console.WriteLine($"  {id}  {mtime:yyyy-MM-dd HH:mm}  {summary}");
    }
    return;
}

var sessionDir = Path.Combine(basePath, sessionId);
if (!Directory.Exists(sessionDir))
{
    Console.Error.WriteLine($"Session not found: {sessionId}");
    return;
}

// workspace.yaml
var wsPath = Path.Combine(sessionDir, "workspace.yaml");
if (File.Exists(wsPath))
{
    Console.WriteLine("=== workspace.yaml ===");
    Console.WriteLine(File.ReadAllText(wsPath));
}

// Last 10 events
var eventsPath = Path.Combine(sessionDir, "events.jsonl");
if (File.Exists(eventsPath))
{
    var lines = File.ReadAllLines(eventsPath);
    Console.WriteLine($"\n=== events.jsonl ({lines.Length} total events, last 10) ===");
    foreach (var line in lines.TakeLast(10))
    {
        // Parse just type and timestamp
        using var doc = System.Text.Json.JsonDocument.Parse(line);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() : "?";
        var ts = root.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetString() : "?";
        Console.WriteLine($"  {ts}  {type}");
    }
}

// Todos from session.db
var dbFile = Path.Combine(sessionDir, "session.db");
if (File.Exists(dbFile))
{
    Console.WriteLine("\n=== session.db todos ===");
    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbFile};Mode=ReadOnly");
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='todos'";
    if (cmd.ExecuteScalar() is not null)
    {
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "SELECT id, title, status FROM todos ORDER BY created_at";
        using var reader = cmd2.ExecuteReader();
        while (reader.Read())
            Console.WriteLine($"  [{reader.GetString(2),-12}] {reader.GetString(0)}: {reader.GetString(1)}");
    }
    else
    {
        Console.WriteLine("  (no todos table)");
    }
}

// Files in session dir
Console.WriteLine($"\n=== Files ===");
foreach (var f in Directory.GetFiles(sessionDir))
    Console.WriteLine($"  {Path.GetFileName(f),-25} {new FileInfo(f).Length,10:N0} bytes  {File.GetLastWriteTimeUtc(f):yyyy-MM-dd HH:mm}");
foreach (var d in Directory.GetDirectories(sessionDir))
    Console.WriteLine($"  {Path.GetFileName(d) + "/",-25} (directory)");
