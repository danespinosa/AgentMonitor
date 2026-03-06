namespace AgentMonitor.Providers;

using System.Text.Json;
using AgentMonitor.Models;

public sealed class ClaudeSessionProvider : ISessionProvider
{
    private readonly string _claudeBasePath;

    public AgentType Type => AgentType.ClaudeCli;

    public ClaudeSessionProvider(string? basePath = null)
    {
        _claudeBasePath = Path.Combine(
            basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude");
    }

    public async Task<List<AgentSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = new List<AgentSession>();
        var projectsDir = Path.Combine(_claudeBasePath, "projects");

        if (!Directory.Exists(projectsDir))
            return sessions;

        foreach (var projectDir in Directory.GetDirectories(projectsDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var projectSessions = await ReadProjectSessionsAsync(projectDir);
                sessions.AddRange(projectSessions);
            }
            catch
            {
                // Skip projects that can't be read
            }
        }

        return sessions;
    }

    private static async Task<List<AgentSession>> ReadProjectSessionsAsync(string projectDir)
    {
        var sessions = new List<AgentSession>();
        var indexFile = Path.Combine(projectDir, "sessions-index.json");

        if (!File.Exists(indexFile))
            return sessions;

        var json = await File.ReadAllTextAsync(indexFile);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("entries", out var entries))
            return sessions;

        var originalPath = root.TryGetProperty("originalPath", out var opProp) ? opProp.GetString() ?? "" : "";

        foreach (var entry in entries.EnumerateArray())
        {
            try
            {
                var session = ParseSessionEntry(entry, projectDir, originalPath);
                if (session is not null)
                    sessions.Add(session);
            }
            catch
            {
                // Skip malformed entries
            }
        }

        return sessions;
    }

    private static AgentSession? ParseSessionEntry(JsonElement entry, string projectDir, string originalPath)
    {
        var sessionId = entry.TryGetProperty("sessionId", out var idProp) ? idProp.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(sessionId))
            return null;

        var summary = entry.TryGetProperty("summary", out var sumProp) ? sumProp.GetString() ?? "" : "";
        var gitBranch = entry.TryGetProperty("gitBranch", out var brProp) ? brProp.GetString() ?? "" : "";
        var projectPath = entry.TryGetProperty("projectPath", out var ppProp) ? ppProp.GetString() ?? originalPath : originalPath;

        DateTime lastActivity = DateTime.MinValue;
        if (entry.TryGetProperty("modified", out var modProp) && DateTime.TryParse(modProp.GetString(), out var modified))
        {
            lastActivity = modified.ToUniversalTime();
        }

        // Also check the JSONL file mtime as a fallback/more accurate source
        if (entry.TryGetProperty("fullPath", out var fpProp))
        {
            var fullPath = fpProp.GetString();
            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
            {
                var fileMtime = File.GetLastWriteTimeUtc(fullPath);
                if (fileMtime > lastActivity)
                    lastActivity = fileMtime;
            }
        }

        var lastEventType = DetectLastEventType(entry);

        return new AgentSession
        {
            Type = AgentType.ClaudeCli,
            Id = sessionId,
            Summary = summary,
            Branch = gitBranch,
            WorkingDirectory = projectPath,
            LastActivity = lastActivity,
            LastEventType = lastEventType
        };
    }

    private static string DetectLastEventType(JsonElement entry)
    {
        // Claude sessions-index doesn't have event types, but we can infer from messageCount
        if (entry.TryGetProperty("messageCount", out var mcProp))
        {
            var count = mcProp.GetInt32();
            if (count == 0) return "no_messages";
        }

        return "session_active";
    }
}
