namespace AgentMonitor.Services;

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;

[SupportedOSPlatform("windows")]
public static partial class ProcessDetector
{
    [GeneratedRegex(@"--resume[= ]([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})", RegexOptions.IgnoreCase)]
    private static partial Regex ResumeSessionIdRegex();

    /// <summary>
    /// Detects running copilot sessions using three methods:
    /// 1. File lock on session.db (most reliable when locks aren't stale)
    /// 2. Parse --resume session IDs from copilot.exe command lines
    /// 3. Match process start times to session dir creation times (fallback)
    /// </summary>
    public static HashSet<string> GetLockedCopilotSessionIds(string? basePath = null)
    {
        var lockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sessionStatePath = Path.Combine(
            basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot", "session-state");

        if (!Directory.Exists(sessionStatePath))
            return lockedIds;

        // Method 1: file lock detection
        foreach (var dir in Directory.GetDirectories(sessionStatePath))
        {
            var dbFile = Path.Combine(dir, "session.db");
            if (File.Exists(dbFile) && IsFileLocked(dbFile))
                lockedIds.Add(Path.GetFileName(dir));
        }

        // Get process info for methods 2 and 3
        var (resumeIds, processStartTimes) = GetCopilotProcessInfo();

        // Method 2: extract session IDs from --resume command line args
        foreach (var sessionId in resumeIds)
        {
            if (Directory.Exists(Path.Combine(sessionStatePath, sessionId)))
                lockedIds.Add(sessionId);
        }

        // Method 3: match process start times to session dir creation times
        foreach (var dir in Directory.GetDirectories(sessionStatePath))
        {
            var sessionId = Path.GetFileName(dir);
            if (lockedIds.Contains(sessionId))
                continue;

            var creationTime = Directory.GetCreationTimeUtc(dir);
            foreach (var procStart in processStartTimes)
            {
                if (Math.Abs((creationTime - procStart).TotalSeconds) < 5)
                {
                    var eventsFile = Path.Combine(dir, "events.jsonl");
                    if (!HasShutdownEvent(eventsFile))
                    {
                        lockedIds.Add(sessionId);
                        break;
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

    /// <summary>
    /// Gets copilot.exe process info by shelling out to pwsh to read command lines.
    /// WMI/System.Management doesn't work in .NET 10 (COM disabled).
    /// </summary>
    private static (HashSet<string> resumeIds, List<DateTime> startTimes) GetCopilotProcessInfo()
    {
        var resumeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var startTimes = new List<DateTime>();
        var regex = ResumeSessionIdRegex();

        // Get command lines via pwsh (WMI doesn't work in .NET 10)
        try
        {
            var psi = new ProcessStartInfo("pwsh", "-NoProfile -Command \"Get-CimInstance Win32_Process -Filter \\\"Name='copilot.exe'\\\" | Select-Object ProcessId, CommandLine | ConvertTo-Json -Compress\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            if (proc is not null)
            {
                var json = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    // Parse JSON — could be array or single object
                    using var doc = JsonDocument.Parse(json);
                    var items = doc.RootElement.ValueKind == JsonValueKind.Array
                        ? doc.RootElement.EnumerateArray().ToList()
                        : [doc.RootElement];

                    foreach (var item in items)
                    {
                        var cmdLine = item.GetProperty("CommandLine").GetString() ?? "";
                        var match = regex.Match(cmdLine);
                        if (match.Success)
                            resumeIds.Add(match.Groups[1].Value);
                    }
                }
            }
        }
        catch { }

        // Get start times via Process API (always works)
        try
        {
            foreach (var proc in Process.GetProcessesByName("copilot"))
            {
                try { startTimes.Add(proc.StartTime.ToUniversalTime()); }
                catch { }
                finally { proc.Dispose(); }
            }
        }
        catch { }

        return (resumeIds, startTimes);
    }

    private static bool HasShutdownEvent(string eventsFile)
    {
        if (!File.Exists(eventsFile))
            return false;
        try
        {
            using var fs = new FileStream(eventsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0) return false;

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
