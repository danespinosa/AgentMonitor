namespace AgentMonitor.UI;

using AgentMonitor.Models;

public sealed class DashboardState
{
    private static readonly string[] FilterLabels =
        ["Open", "All", "Attention", "Running", "Idle", "Stopped"];

    private int _selectedIndex;
    private int _filterIndex; // Start on "Open" (all with running process)
    private string _searchText = string.Empty;
    private bool _isSearchMode;

    public int SelectedIndex => _selectedIndex;
    public string FilterLabel => FilterLabels[_filterIndex];
    public string SearchText => _searchText;
    public bool IsSearchMode => _isSearchMode;

    public IReadOnlyList<AgentSession> ApplyFilter(IReadOnlyList<AgentSession> sessions)
    {
        IEnumerable<AgentSession> result = _filterIndex switch
        {
            0 => sessions.Where(s => s.IsRunning),
            2 => sessions.Where(s => s.Status == SessionStatus.Attention),
            3 => sessions.Where(s => s.Status == SessionStatus.Running),
            4 => sessions.Where(s => s.Status == SessionStatus.Idle),
            5 => sessions.Where(s => s.Status == SessionStatus.Stopped),
            _ => sessions
        };

        if (!string.IsNullOrEmpty(_searchText))
        {
            var search = _searchText;
            result = result.Where(s =>
                s.WorkingDirectory.Contains(search, StringComparison.OrdinalIgnoreCase)
                || s.Summary.Contains(search, StringComparison.OrdinalIgnoreCase)
                || s.Branch.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return result.ToList();
    }

    public void EnterSearchMode()
    {
        _isSearchMode = true;
        // Switch to "All" filter when searching so results aren't limited to open sessions
        _filterIndex = 1;
    }

    public void ExitSearchMode()
    {
        _isSearchMode = false;
    }

    public void ClearSearch()
    {
        _searchText = string.Empty;
        _isSearchMode = false;
        _filterIndex = 0; // Back to "Open"
        _selectedIndex = 0;
    }

    public void AppendSearchChar(char c)
    {
        _searchText += c;
        _selectedIndex = 0;
    }

    public void BackspaceSearch()
    {
        if (_searchText.Length > 0)
        {
            _searchText = _searchText[..^1];
            _selectedIndex = 0;
        }
    }

    public void MoveUp()
    {
        if (_selectedIndex > 0)
            _selectedIndex--;
    }

    public void MoveDown(int maxIndex)
    {
        if (_selectedIndex < maxIndex)
            _selectedIndex++;
    }

    public void CycleFilter()
    {
        _filterIndex = (_filterIndex + 1) % FilterLabels.Length;
        _selectedIndex = 0;
    }

    public void PageUp(int pageSize)
    {
        _selectedIndex = Math.Max(0, _selectedIndex - pageSize);
    }

    public void PageDown(int maxIndex, int pageSize)
    {
        _selectedIndex = Math.Min(maxIndex, _selectedIndex + pageSize);
    }

    public void ClampSelection(int itemCount)
    {
        if (itemCount == 0)
            _selectedIndex = 0;
        else if (_selectedIndex >= itemCount)
            _selectedIndex = itemCount - 1;
    }

    public AgentSession? GetSelectedSession(IReadOnlyList<AgentSession> filtered)
    {
        if (filtered.Count == 0 || _selectedIndex >= filtered.Count)
            return null;
        return filtered[_selectedIndex];
    }
}
