namespace AgentMonitor.UI;

using AgentMonitor.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

public static class DashboardRenderer
{
    private const int PageSize = 20;

    public static IRenderable Render(
        IReadOnlyList<AgentSession> allSessions,
        IReadOnlyList<AgentSession> filtered,
        DashboardState state,
        DateTime lastRefresh)
    {
        var layout = new Rows(
            RenderHeader(state),
            RenderTable(filtered, state),
            RenderDetailPanel(state.GetSelectedSession(filtered)),
            RenderFooter(allSessions, filtered, state, lastRefresh));

        return layout;
    }

    private static IRenderable RenderHeader(DashboardState state)
    {
        var header = $"[bold blue]🤖 Agent Monitor Dashboard[/]  │  Filter: [bold yellow]{state.FilterLabel}[/]";

        if (state.IsSearchMode || !string.IsNullOrEmpty(state.SearchText))
        {
            var cursor = state.IsSearchMode ? "[blink]_[/]" : "";
            var searchDisplay = Markup.Escape(state.SearchText);
            header += $"  │  Search: [bold cyan]{searchDisplay}{cursor}[/]";
        }

        return new Markup(header + Environment.NewLine);
    }

    private static IRenderable RenderTable(IReadOnlyList<AgentSession> sessions, DashboardState state)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Expand();

        table.AddColumn(new TableColumn("[bold] [/]").Width(3));
        table.AddColumn(new TableColumn("[bold]Type[/]").Centered().Width(6));
        table.AddColumn(new TableColumn("[bold]Status[/]").Centered().Width(16));
        table.AddColumn(new TableColumn("[bold]Summary[/]").Width(35));
        table.AddColumn(new TableColumn("[bold]Branch[/]").Width(20));
        table.AddColumn(new TableColumn("[bold]Working Dir[/]").Width(25));
        table.AddColumn(new TableColumn("[bold]Todos[/]").Centered().Width(12));
        table.AddColumn(new TableColumn("[bold]Last Activity[/]").RightAligned().Width(14));

        if (sessions.Count == 0)
        {
            table.AddRow(new Markup(""), new Markup("[grey]No sessions match the current filter[/]"));
            return table;
        }

        // Compute visible window around selected index
        int startIdx = Math.Max(0, state.SelectedIndex - PageSize / 2);
        int endIdx = Math.Min(sessions.Count, startIdx + PageSize);
        if (endIdx - startIdx < PageSize && startIdx > 0)
            startIdx = Math.Max(0, endIdx - PageSize);

        if (startIdx > 0)
            table.AddRow(new Markup("[dim]  ▲[/]"), new Markup($"[dim]  ... {startIdx} more above[/]"));

        for (int i = startIdx; i < endIdx; i++)
        {
            var session = sessions[i];
            bool isSelected = i == state.SelectedIndex;
            var pointer = isSelected ? new Markup("[bold cyan]►[/]") : new Markup(" ");

            table.AddRow(
                pointer,
                FormatType(session.Type, isSelected),
                FormatStatus(session.Status, isSelected, session.IsRunning),
                FormatSummary(session.Summary, session.Status, isSelected),
                FormatBranch(session.Branch, isSelected),
                FormatWorkingDir(session.WorkingDirectory, isSelected),
                FormatTodos(session, isSelected),
                FormatLastActivity(session.LastActivity, isSelected));
        }

        if (endIdx < sessions.Count)
            table.AddRow(new Markup("[dim]  ▼[/]"), new Markup($"[dim]  ... {sessions.Count - endIdx} more below[/]"));

        return table;
    }

    private static IRenderable RenderDetailPanel(AgentSession? session)
    {
        if (session is null)
            return new Markup("");

        var summary = string.IsNullOrWhiteSpace(session.Summary) ? "No summary" : session.Summary;
        var cwd = string.IsNullOrWhiteSpace(session.WorkingDirectory) ? "—" : session.WorkingDirectory;
        var branch = string.IsNullOrWhiteSpace(session.Branch) ? "—" : session.Branch;
        var typeLabel = session.Type == AgentType.CopilotCli ? "Copilot CLI" : "Claude CLI";

        return new Panel(
            new Markup(
                $"[bold]{Markup.Escape(summary)}[/]" + Environment.NewLine +
                $"  Type: [cyan]{typeLabel}[/]  │  Branch: [italic]{Markup.Escape(branch)}[/]  │  " +
                $"Last Event: [dim]{Markup.Escape(session.LastEventType)}[/]" + Environment.NewLine +
                $"  Path: [dim]{Markup.Escape(cwd)}[/]"))
            .Header("[bold]Selected Session[/]")
            .BorderColor(Color.Cyan1)
            .Expand();
    }

    private static IRenderable RenderFooter(
        IReadOnlyList<AgentSession> allSessions,
        IReadOnlyList<AgentSession> filtered,
        DashboardState state,
        DateTime lastRefresh)
    {
        var attention = allSessions.Count(s => s.Status == SessionStatus.Attention);
        var running = allSessions.Count(s => s.Status == SessionStatus.Running);
        var idle = allSessions.Count(s => s.Status == SessionStatus.Idle);
        var stopped = allSessions.Count(s => s.Status == SessionStatus.Stopped);

        var elapsed = FormatTimeAgo(lastRefresh);
        var position = filtered.Count > 0 ? $"{state.SelectedIndex + 1}/{filtered.Count}" : "0/0";

        var helpText = state.IsSearchMode
            ? "  [dim][bold]Type[/] to search  [bold]Enter[/] confirm  [bold]Esc[/] clear  [bold]↑↓[/] Navigate[/]"
            : "  [dim][bold]↑↓[/] Navigate  [bold]PgUp/PgDn[/] Page  [bold]Enter[/] Resume  [bold]F[/] Filter  [bold]/[/] Search  [bold]Esc[/] Clear  [bold]Q[/] Quit[/]";

        return new Markup(
            $"  [red]●{attention}[/] Attention  [green]●{running}[/] Running  " +
            $"[yellow]●{idle}[/] Idle  [grey]●{stopped}[/] Stopped  " +
            $"│  Showing: {position}  │  Refreshed: {elapsed}" +
            Environment.NewLine + helpText);
    }

    private static Markup FormatType(AgentType type, bool selected)
    {
        var bg = selected ? " on grey23" : "";
        var label = type == AgentType.CopilotCli ? "GHC" : "CC";
        var color = type == AgentType.CopilotCli ? "cyan" : "magenta";
        return new Markup($"[bold {color}{bg}]{label}[/]");
    }

    private static Markup FormatStatus(SessionStatus status, bool selected, bool isRunning)
    {
        var bg = selected ? " on grey23" : "";
        return status switch
        {
            SessionStatus.Attention => new Markup($"[bold red{bg}]🔴 Attention[/]"),
            SessionStatus.Running => new Markup($"[bold green{bg}]🟢 Running[/]"),
            SessionStatus.Idle => new Markup($"[bold yellow{bg}]🟡 Idle[/]"),
            SessionStatus.Stopped => new Markup($"[grey{bg}]⚪ Stopped[/]"),
            _ => new Markup($"[grey{bg}]?[/]")
        };
    }

    private static Markup FormatSummary(string summary, SessionStatus status, bool selected)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return new Markup(selected ? "[dim italic on grey23]No summary[/]" : "[dim italic]No summary[/]");

        var truncated = summary.Length > 30 ? summary[..27] + "..." : summary;
        var escaped = Markup.Escape(truncated);
        var bg = selected ? " on grey23" : "";

        return status switch
        {
            SessionStatus.Attention => new Markup($"[bold red{bg}]{escaped}[/]"),
            SessionStatus.Running => new Markup($"[green{bg}]{escaped}[/]"),
            SessionStatus.Idle => new Markup($"[yellow{bg}]{escaped}[/]"),
            _ => new Markup($"[grey{bg}]{escaped}[/]")
        };
    }

    private static Markup FormatBranch(string branch, bool selected)
    {
        if (string.IsNullOrWhiteSpace(branch))
            return new Markup(selected ? "[dim on grey23]—[/]" : "[dim]—[/]");

        var truncated = branch.Length > 18 ? branch[..15] + "..." : branch;
        var bg = selected ? " on grey23" : "";
        return new Markup($"[italic{bg}]{Markup.Escape(truncated)}[/]");
    }

    private static Markup FormatWorkingDir(string cwd, bool selected)
    {
        if (string.IsNullOrWhiteSpace(cwd))
            return new Markup(selected ? "[dim on grey23]—[/]" : "[dim]—[/]");

        var parts = cwd.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var display = parts.Length > 2
            ? "..." + Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar, parts[^2..])
            : cwd;

        if (display.Length > 23)
            display = display[..20] + "...";

        var bg = selected ? " on grey23" : "";
        return new Markup($"[dim{bg}]{Markup.Escape(display)}[/]");
    }

    private static Markup FormatTodos(AgentSession session, bool selected)
    {
        if (session.TotalTodos == 0)
            return new Markup(selected ? "[dim on grey23]—[/]" : "[dim]—[/]");

        var color = session.BlockedTodos > 0 ? "red" : "green";
        var bg = selected ? " on grey23" : "";
        return new Markup($"[{color}{bg}]{session.DoneTodos}/{session.TotalTodos}[/]" +
            (session.BlockedTodos > 0 ? $" [red{bg}]({session.BlockedTodos}⛔)[/]" : ""));
    }

    private static Markup FormatLastActivity(DateTime lastActivity, bool selected)
    {
        if (lastActivity == DateTime.MinValue)
            return new Markup(selected ? "[dim on grey23]Unknown[/]" : "[dim]Unknown[/]");

        return new Markup(FormatTimeAgo(lastActivity, selected));
    }

    private static string FormatTimeAgo(DateTime utcTime, bool selected = false)
    {
        var elapsed = DateTime.UtcNow - utcTime;
        var bg = selected ? " on grey23" : "";

        if (elapsed.TotalSeconds < 60) return $"[green{bg}]{(int)elapsed.TotalSeconds}s ago[/]";
        if (elapsed.TotalMinutes < 60) return $"[yellow{bg}]{(int)elapsed.TotalMinutes}m ago[/]";
        if (elapsed.TotalHours < 24) return $"[grey{bg}]{(int)elapsed.TotalHours}h ago[/]";
        return $"[dim{bg}]{(int)elapsed.TotalDays}d ago[/]";
    }
}
