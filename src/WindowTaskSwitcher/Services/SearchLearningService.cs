using System.IO;
using System.Text.Json;

namespace WindowTaskSwitcher.Services;

public sealed class SearchLearningService
{
    private readonly string _historyPath;
    private Dictionary<string, int> _history = new();
    private const int MaxBoostCount = 10;
    private const double BoostFactor = 0.2;

    public SearchLearningService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "WindowTaskSwitcher");
        Directory.CreateDirectory(dir);
        _historyPath = Path.Combine(dir, "search_history.json");
        Load();
    }

    public void RecordSelection(string query, string processName)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(processName))
            return;

        // Use first 3 chars of query as the key prefix for generalization
        string prefix = query.Length > 3 ? query[..3] : query;
        string key = $"{prefix.ToLowerInvariant()}|{processName.ToLowerInvariant()}";

        _history.TryGetValue(key, out int count);
        _history[key] = Math.Min(count + 1, MaxBoostCount * 2); // Allow some headroom for decay
        Save();
    }

    public double GetBoost(string query, string processName)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(processName))
            return 1.0;

        string prefix = query.Length > 3 ? query[..3] : query;
        string key = $"{prefix.ToLowerInvariant()}|{processName.ToLowerInvariant()}";

        if (_history.TryGetValue(key, out int count))
        {
            int effectiveCount = Math.Min(count, MaxBoostCount);
            return 1.0 + BoostFactor * effectiveCount;
        }

        return 1.0;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                string json = File.ReadAllText(_historyPath);
                _history = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
            }
        }
        catch
        {
            _history = new();
        }
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyPath, json);
        }
        catch
        {
            // Non-critical — don't crash for history persistence
        }
    }
}
