using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CfaDatabaseEditor.Services;

public class LayoutConfig
{
    [JsonPropertyName("leftPanelWidth")]
    public double? LeftPanelWidth { get; set; }

    [JsonPropertyName("rightPanelWidth")]
    public double? RightPanelWidth { get; set; }

    [JsonPropertyName("previewImageHeight")]
    public double? PreviewImageHeight { get; set; }
}

public class AppConfig
{
    [JsonPropertyName("recentFolders")]
    public List<string> RecentFolders { get; set; } = new();

    [JsonPropertyName("layout")]
    public LayoutConfig? Layout { get; set; }
}

public static class ConfigService
{
    private const int MaxRecentEntries = 8;

    private static readonly string FilePath =
        Path.Combine(AppContext.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static void Save(AppConfig config)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(config, JsonOptions));
        }
        catch
        {
            // best-effort
        }
    }

    public static List<string> GetRecentFolders()
    {
        return Load().RecentFolders;
    }

    public static void AddRecentFolder(string folder)
    {
        var config = Load();
        config.RecentFolders.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
        config.RecentFolders.Insert(0, folder);
        if (config.RecentFolders.Count > MaxRecentEntries)
            config.RecentFolders.RemoveRange(MaxRecentEntries, config.RecentFolders.Count - MaxRecentEntries);
        Save(config);
    }
}
