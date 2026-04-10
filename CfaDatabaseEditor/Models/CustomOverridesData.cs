namespace CfaDatabaseEditor.Models;

/// <summary>
/// Raw data for a single custom faction entry parsed from Custom Overrides.txt.
/// </summary>
public class CustomFactionData
{
    public int Index { get; set; }            // Array index [100], [101], etc.
    public int ClanId { get; set; }           // 0 = nation, >0 = it's a clan with this ID
    public int NationId { get; set; }         // For nations: the ID used in DCards. For clans: parent nation or -1
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
}

/// <summary>
/// All custom overrides parsed from Custom Overrides.txt.
/// </summary>
public class CustomOverridesData
{
    public List<CustomFactionData> Factions { get; set; } = new();
    public int MaxCustomFaction { get; set; }
    public int? AllCardOverride { get; set; }
    public int? CustomCardStartId { get; set; }

    /// <summary>
    /// Lines from Custom Overrides.txt that are NOT faction-related or AllCard,
    /// preserved so we don't lose other user overrides when rewriting.
    /// </summary>
    public List<string> OtherLines { get; set; } = new();
}
