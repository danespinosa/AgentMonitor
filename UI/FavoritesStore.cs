namespace AgentMonitor.UI;

using System.Text.Json;

/// <summary>
/// Persists favorite session IDs to a JSON file in ~/.copilot/agent-monitor-favorites.json
/// </summary>
public sealed class FavoritesStore
{
    private readonly string _filePath;
    private HashSet<string> _favorites;

    public FavoritesStore()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot", "agent-monitor-favorites.json");
        _favorites = Load();
    }

    public bool IsFavorite(string sessionId) =>
        _favorites.Contains(sessionId);

    public void Toggle(string sessionId)
    {
        if (!_favorites.Remove(sessionId))
            _favorites.Add(sessionId);
        Save();
    }

    public int Count => _favorites.Count;

    private HashSet<string> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var ids = JsonSerializer.Deserialize<string[]>(json);
                return ids is not null
                    ? new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase)
                    : new(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { }
        return new(StringComparer.OrdinalIgnoreCase);
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_favorites.ToArray(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}
