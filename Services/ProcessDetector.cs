namespace AgentMonitor.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
public static class ProcessDetector
{
    /// <summary>
    /// Returns session IDs whose session.db is currently locked by a running copilot process,
    /// plus sessions without session.db that match a running copilot.exe by creation time.
    /// </summary>
    public static HashSet<string> GetLockedCopilotSessionIds(string? basePath = null)
    {
        var lockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sessionStatePath = Path.Combine(
            basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot", "session-state");

        if (!Directory.Exists(sessionStatePath))
            return lockedIds;

        var dirsWithoutDb = new List<(string sessionId, DateTime creationTime)>();
        var dirsWithDb = new List<(string sessionId, DateTime creationTime)>();

        foreach (var dir in Directory.GetDirectories(sessionStatePath))
        {
            var dbFile = Path.Combine(dir, "session.db");
            var sessionId = Path.GetFileName(dir);

            if (File.Exists(dbFile))
            {
                if (IsFileLocked(dbFile))
                    lockedIds.Add(sessionId);
                else
                    dirsWithDb.Add((sessionId, Directory.GetCreationTimeUtc(dir)));
            }
            else
            {
                dirsWithoutDb.Add((sessionId, Directory.GetCreationTimeUtc(dir)));
            }
        }

        // Get running copilot.exe process count and start times for fallback matching
        var processStartTimes = GetCopilotProcessStartTimesUtc();
        var unmatchedProcesses = processStartTimes.Count - lockedIds.Count;

        // Fallback: match remaining copilot.exe processes to session dirs by creation time.
        // This handles: (1) sessions without session.db, (2) stale file locks after
        // sleep/hibernate where processes are still running but locks were released.
        if (unmatchedProcesses > 0)
        {
            var allUnmatched = dirsWithoutDb.Concat(dirsWithDb).ToList();
            foreach (var (sessionId, creationTime) in allUnmatched)
            {
                if (lockedIds.Contains(sessionId))
                    continue;

                foreach (var procStart in processStartTimes)
                {
                    var diff = Math.Abs((creationTime - procStart).TotalSeconds);
                    if (diff < 5)
                    {
                        var eventsFile = Path.Combine(sessionStatePath, sessionId, "events.jsonl");
                        if (!HasShutdownEvent(eventsFile))
                        {
                            lockedIds.Add(sessionId);
                            break;
                        }
                    }
                }
            }
        }

        return lockedIds;
    }

    /// <summary>
    /// Returns session IDs for Claude CLI by checking locked JSONL files.
    /// </summary>
    public static HashSet<string> GetLockedClaudeSessionIds(string? basePath = null)
    {
        var lockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projectsPath = Path.Combine(
            basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "projects");

        if (!Directory.Exists(projectsPath))
            return lockedIds;

        foreach (var projectDir in Directory.GetDirectories(projectsPath))
        {
            foreach (var jsonlFile in Directory.GetFiles(projectDir, "*.jsonl"))
            {
                if (IsFileLocked(jsonlFile))
                {
                    var sessionId = Path.GetFileNameWithoutExtension(jsonlFile);
                    lockedIds.Add(sessionId);
                }
            }
        }

        return lockedIds;
    }

    private static List<DateTime> GetCopilotProcessStartTimesUtc()
    {
        var times = new List<DateTime>();
        try
        {
            foreach (var proc in Process.GetProcessesByName("copilot"))
            {
                try
                {
                    times.Add(proc.StartTime.ToUniversalTime());
                }
                catch { }
                finally { proc.Dispose(); }
            }
        }
        catch { }
        return times;
    }

    private static bool HasShutdownEvent(string eventsFile)
    {
        if (!File.Exists(eventsFile))
            return false;
        try
        {
            // Read the last line efficiently
            using var fs = new FileStream(eventsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0) return false;

            // Seek near end and read last line
            var startPos = Math.Max(0, fs.Length - 4096);
            fs.Seek(startPos, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            string? lastLine = null;
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lastLine = line;
            }
            return lastLine?.Contains("\"session.shutdown\"", StringComparison.Ordinal) == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            // Use ReadWrite with FileShare.Read — this detects active write locks
            // held by running processes, while ignoring stale shared/read locks.
            using var fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
