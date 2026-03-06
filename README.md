# 🤖 Agent Monitor

A .NET 10 console application that monitors **GitHub Copilot CLI** and **Claude CLI** agent sessions with a rich terminal UI dashboard.

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **Real-time dashboard** — Auto-refreshing TUI built with [Spectre.Console](https://spectreconsole.net/)
- **Status detection** — Classifies each agent session as:
  - 🔴 **Attention** — Process running, waiting for user input
  - 🟢 **Running** — Process running, actively executing
  - 🟡 **Idle** — Process running, no recent activity
  - ⚪ **Stopped** — No process running (historical session)
- **Keyboard navigation** — Arrow keys, PgUp/PgDn, Home/End
- **Filtering** — Cycle through filters (Open/All/Attention/Running/Idle/Stopped) with `F`
- **Search** — Press `/` to search sessions by path, summary, or branch
- **Resume sessions** — Press `Enter` on a stopped session to resume it in a new Windows Terminal tab
- **Hybrid process detection** — Uses file locks on `session.db` + process start time matching for reliable detection

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows (process detection uses Windows-specific APIs)

### Run from source

```bash
git clone https://github.com/danespinosa/AgentMonitor.git
cd AgentMonitor
dotnet run
```

### Publish and install

```bash
dotnet publish -c Release -o bin/publish
```

Then run `bin/publish/AgentMonitor.exe` directly, create a desktop shortcut, or add a Windows Terminal profile:

```json
{
    "name": "Agent Monitor",
    "commandline": "C:\\path\\to\\AgentMonitor.exe",
    "icon": "🤖",
    "colorScheme": "One Half Dark"
}
```

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `↑` `↓` | Navigate sessions |
| `PgUp` `PgDn` | Page through sessions |
| `Home` `End` | Jump to first/last |
| `F` | Cycle filter (Open → All → Attention → Running → Idle → Stopped) |
| `/` | Enter search mode |
| `Enter` | Resume selected stopped session |
| `Esc` | Clear search / exit search mode |
| `Q` | Quit |

## Architecture

```
AgentMonitor/
├── AgentMonitor.Core/          # Class library (reusable core)
│   ├── Models/                 # AgentSession, AgentType, SessionStatus
│   ├── Providers/              # Session discovery from file system
│   │   ├── CopilotSessionProvider.cs   # Reads ~/.copilot/session-state/
│   │   └── ClaudeSessionProvider.cs    # Reads ~/.claude/projects/
│   └── Services/
│       ├── SessionMonitorService.cs    # Orchestrates detection + status
│       └── ProcessDetector.cs          # File lock + process time matching
├── UI/
│   ├── DashboardRenderer.cs    # Stateless Spectre.Console renderer
│   ├── DashboardState.cs       # Cursor, filter, search state
│   └── SessionLauncher.cs      # Resume sessions in new terminal
├── Program.cs                  # Entry point with Live display loop
└── diagnostics/                # Standalone .NET 10 file-based scripts
    ├── check-locks.cs          # Show locked session.db files
    ├── list-sessions.cs        # List all sessions with status
    ├── inspect-session.cs      # Deep inspect a specific session
    └── search-sessions.cs      # Search sessions by text
```

### How detection works

1. **File lock detection** — Each running `copilot.exe` holds a write lock on its `session.db`. We attempt `FileAccess.ReadWrite` with `FileShare.Read` — if it throws `IOException`, the file is locked by a real process.

2. **Process time matching** (fallback) — Some sessions don't create `session.db` (e.g., before any conversation starts). For these, we match `copilot.exe` process start times to session directory creation times within a 5-second window, and verify the session hasn't emitted a `session.shutdown` event.

### Data sources

| CLI | Session data location | Key files |
|-----|----------------------|-----------|
| Copilot CLI | `~/.copilot/session-state/{uuid}/` | `workspace.yaml`, `events.jsonl`, `session.db` |
| Claude CLI | `~/.claude/projects/{project}/` | `sessions-index.json`, `{session-id}.jsonl` |

## Diagnostic Scripts

The `diagnostics/` folder contains standalone [.NET 10 file-based apps](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10#file-based-apps) that reference the `AgentMonitor.Core` class library. Run them from the `diagnostics/` directory:

```bash
cd diagnostics

# Show which sessions have active file locks
dotnet run check-locks.cs

# List all sessions with status
dotnet run list-sessions.cs

# Search sessions by path, summary, or branch
dotnet run search-sessions.cs -- jaws

# Deep inspect a specific session
dotnet run inspect-session.cs -- <session-id>
```

## Contributing

1. Fork the repo
2. Create a feature branch
3. Submit a PR — requires 1 approving review before merge

## License

MIT
