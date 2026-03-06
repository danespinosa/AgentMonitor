namespace AgentMonitor.Providers;

using System.Text.Json;
using AgentMonitor.Models;
using Microsoft.Data.Sqlite;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed class CopilotSessionProvider : ISessionProvider
{
    private readonly string _sessionStatePath;

    public AgentType Type => AgentType.CopilotCli;

    public CopilotSessionProvider(string? basePath = null)
    {
        _sessionStatePath = Path.Combine(
            basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot", "session-state");
    }

    public async Task<List<AgentSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = new List<AgentSession>();

        if (!Directory.Exists(_sessionStatePath))
            return sessions;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var dir in Directory.GetDirectories(_sessionStatePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var session = await ReadSessionAsync(dir, deserializer);
                if (session is not null)
                    sessions.Add(session);
            }
            catch
            {
                // Skip sessions that can't be read
            }
        }

        return sessions;
    }

    private async Task<AgentSession?> ReadSessionAsync(string sessionDir, IDeserializer deserializer)
    {
        var workspaceFile = Path.Combine(sessionDir, "workspace.yaml");
        if (!File.Exists(workspaceFile))
            return null;

        var yaml = await File.ReadAllTextAsync(workspaceFile);
        var workspace = deserializer.Deserialize<WorkspaceYaml>(yaml);

        if (string.IsNullOrEmpty(workspace.Id))
            return null;

        var (lastActivity, lastEventType) = await GetLastEventInfoAsync(sessionDir);
        var (total, done, blocked) = await GetTodoStatsAsync(sessionDir);

        return new AgentSession
        {
            Type = AgentType.CopilotCli,
            Id = workspace.Id,
            Summary = workspace.Summary ?? string.Empty,
            Branch = workspace.Branch ?? string.Empty,
            WorkingDirectory = workspace.Cwd ?? string.Empty,
            LastActivity = lastActivity,
            LastEventType = lastEventType,
            TotalTodos = total,
            DoneTodos = done,
            BlockedTodos = blocked
        };
    }

    private static async Task<(DateTime lastActivity, string lastEventType)> GetLastEventInfoAsync(string sessionDir)
    {
        var eventsFile = Path.Combine(sessionDir, "events.jsonl");
        if (!File.Exists(eventsFile))
            return (File.GetLastWriteTimeUtc(Path.Combine(sessionDir, "workspace.yaml")), string.Empty);

        try
        {
            // Read last few lines efficiently
            var lines = await File.ReadAllLinesAsync(eventsFile);
            for (int i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                using var doc = JsonDocument.Parse(lines[i]);
                var root = doc.RootElement;

                var eventType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";

                if (root.TryGetProperty("timestamp", out var tsProp))
                {
                    if (DateTime.TryParse(tsProp.GetString(), out var ts))
                        return (ts.ToUniversalTime(), eventType);
                }
            }
        }
        catch
        {
            // Fall back to file modification time
        }

        return (File.GetLastWriteTimeUtc(eventsFile), string.Empty);
    }

    private static async Task<(int total, int done, int blocked)> GetTodoStatsAsync(string sessionDir)
    {
        var dbFile = Path.Combine(sessionDir, "session.db");
        if (!File.Exists(dbFile))
            return (0, 0, 0);

        try
        {
            using var connection = new SqliteConnection($"Data Source={dbFile};Mode=ReadOnly");
            await connection.OpenAsync();

            // Check if todos table exists
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='todos'";
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists is null)
                return (0, 0, 0);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT
                    COUNT(*) as total,
                    SUM(CASE WHEN status = 'done' THEN 1 ELSE 0 END) as done,
                    SUM(CASE WHEN status = 'blocked' THEN 1 ELSE 0 END) as blocked
                FROM todos
                """;

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            }
        }
        catch
        {
            // DB might be locked by another process
        }

        return (0, 0, 0);
    }

    private sealed class WorkspaceYaml
    {
        public string Id { get; set; } = string.Empty;
        public string? Cwd { get; set; }
        public string? Summary { get; set; }
        public string? Branch { get; set; }
        public string? GitRoot { get; set; }
        public string? CreatedAt { get; set; }
        public string? UpdatedAt { get; set; }
    }
}
