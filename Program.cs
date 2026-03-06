using AgentMonitor.Models;
using AgentMonitor.Providers;
using AgentMonitor.Services;
using AgentMonitor.UI;
using Spectre.Console;

var providers = new ISessionProvider[]
{
    new CopilotSessionProvider(),
    new ClaudeSessionProvider()
};

var monitor = new SessionMonitorService(providers);
var state = new DashboardState();

AnsiConsole.Clear();
AnsiConsole.Write(new FigletText("Agent Monitor").Color(Color.Blue));

using var cts = new CancellationTokenSource();
var inputReady = new AutoResetEvent(false);
List<AgentSession> lastSessions = [];

// Input handling on background thread
_ = Task.Run(() =>
{
    while (!cts.IsCancellationRequested)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);

            // Search mode: capture typed characters
            if (state.IsSearchMode)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Escape:
                        state.ClearSearch();
                        inputReady.Set();
                        break;
                    case ConsoleKey.Enter:
                        state.ExitSearchMode();
                        inputReady.Set();
                        break;
                    case ConsoleKey.Backspace:
                        state.BackspaceSearch();
                        inputReady.Set();
                        break;
                    case ConsoleKey.UpArrow:
                        state.MoveUp();
                        inputReady.Set();
                        break;
                    case ConsoleKey.DownArrow:
                        state.MoveDown(int.MaxValue);
                        inputReady.Set();
                        break;
                    default:
                        if (key.KeyChar >= 32) // Printable character
                        {
                            state.AppendSearchChar(key.KeyChar);
                            inputReady.Set();
                        }
                        break;
                }
            }
            else
            {
                switch (key.Key)
                {
                    case ConsoleKey.Q:
                        cts.Cancel();
                        return;
                    case ConsoleKey.UpArrow:
                        state.MoveUp();
                        inputReady.Set();
                        break;
                    case ConsoleKey.DownArrow:
                        state.MoveDown(int.MaxValue);
                        inputReady.Set();
                        break;
                    case ConsoleKey.PageUp:
                        state.PageUp(20);
                        inputReady.Set();
                        break;
                    case ConsoleKey.PageDown:
                        state.PageDown(int.MaxValue, 20);
                        inputReady.Set();
                        break;
                    case ConsoleKey.Home:
                        state.PageUp(int.MaxValue);
                        inputReady.Set();
                        break;
                    case ConsoleKey.End:
                        state.PageDown(int.MaxValue, int.MaxValue);
                        inputReady.Set();
                        break;
                    case ConsoleKey.F:
                        state.CycleFilter();
                        inputReady.Set();
                        break;
                    case ConsoleKey.Escape:
                        state.ClearSearch();
                        inputReady.Set();
                        break;
                    case ConsoleKey.Enter:
                        var selectedForResume = state.GetSelectedSession(
                            state.ApplyFilter(lastSessions));
                        if (selectedForResume is not null)
                            SessionLauncher.ResumeSession(selectedForResume);
                        break;
                    default:
                        if (key.KeyChar == '/' || key.KeyChar == '\\')
                        {
                            state.EnterSearchMode();
                            inputReady.Set();
                        }
                        break;
                }
            }
        }
        Thread.Sleep(50);
    }
});


try
{
    await AnsiConsole.Live(new Text("Loading..."))
        .AutoClear(true)
        .StartAsync(async ctx =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    lastSessions = await monitor.GetAllSessionsAsync(cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch { /* Keep last sessions on error */ }

                // Render loop — re-render on input or every 200ms for time updates
                var nextDataRefresh = DateTime.UtcNow.AddSeconds(5);
                while (!cts.IsCancellationRequested && DateTime.UtcNow < nextDataRefresh)
                {
                    var filtered = state.ApplyFilter(lastSessions);
                    state.ClampSelection(filtered.Count);
                    var dashboard = DashboardRenderer.Render(lastSessions, filtered, state, DateTime.UtcNow);
                    ctx.UpdateTarget(dashboard);

                    // Wait for input or timeout
                    inputReady.WaitOne(200);
                }
            }
        });
}
catch (OperationCanceledException)
{
    // Clean exit
}

AnsiConsole.MarkupLine("[dim]Agent Monitor stopped.[/]");
