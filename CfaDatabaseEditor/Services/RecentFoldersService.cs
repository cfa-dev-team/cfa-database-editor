using System.IO;
using System.Text.Json;

namespace CfaDatabaseEditor.Services;

public static class RecentFoldersService
{
    private const int MaxEntries = 8;

    private static readonly string FilePath =
        Path.Combine(AppContext.BaseDirectory, "recent_folders.json");

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static void Add(string folder)
    {
        var list = Load();
        list.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, folder);
        if (list.Count > MaxEntries)
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        Save(list);
    }

    private static void Save(List<string> list)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch
        {
            // best-effort
        }
    }
}
