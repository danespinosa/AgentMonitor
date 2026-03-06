namespace AgentMonitor.UI;

using AgentMonitor.Models;
using System.Diagnostics;

public static class SessionLauncher
{
    public static void ResumeSession(AgentSession session)
    {
        if (session.IsRunning)
            return; // Already running — nothing to do

        var (exe, args) = session.Type switch
        {
            AgentType.CopilotCli => ("copilot", $"--resume {session.Id}"),
            AgentType.ClaudeCli => ("claude", $"--resume --session-id {session.Id}"),
            _ => (null, null)
        };

        if (exe is null) return;

        // Launch in Windows Terminal if available, otherwise in a new cmd window
        var wtPath = FindWindowsTerminal();
        if (wtPath is not null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = wtPath,
                Arguments = $"new-tab --title \"{EscapeTitle(session.Summary)}\" " +
                            $"-d \"{session.WorkingDirectory}\" " +
                            $"cmd /k \"{exe} {args}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);
        }
        else
        {
            // Fallback: open in a new cmd window
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k cd /d \"{session.WorkingDirectory}\" && {exe} {args}",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
    }

    private static string? FindWindowsTerminal()
    {
        // Check common Windows Terminal paths
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WindowsApps\wt.exe"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        // Try PATH
        try
        {
            var psi = new ProcessStartInfo("where", "wt.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            if (proc is not null)
            {
                var output = proc.StandardOutput.ReadLine();
                proc.WaitForExit(2000);
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    return output.Trim();
            }
        }
        catch { }

        return null;
    }

    private static string EscapeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Agent Session";
        // Remove characters that could break command line quoting
        return title.Replace("\"", "'").Replace("&", "and");
    }
}
