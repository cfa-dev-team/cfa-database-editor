using Avalonia.Media;

namespace CfaDatabaseEditor.Models;

public enum FactionType
{
    Nation,
    Clan,
    Collaboration,
    Special
}

public enum FactionEra
{
    Standard,
    VPremium,
    Crossover,
    Other,
    Custom
}

public class ClanDefinition
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public FactionType Type { get; init; }
    public FactionEra Era { get; init; }
    public int? ParentNationId { get; init; }
    public Color DisplayColor { get; init; }
    public string? FileName { get; init; }
    public bool IsCustom { get; init; }
    public int CustomIndex { get; init; }  // Array index in Custom Overrides.txt (100+)

    public override string ToString() => Name;
}

public static class ClanRegistry
{
    // Standard Nations
    public static readonly ClanDefinition DragonEmpire = new() { Id = 1, Name = "Dragon Empire", Type = FactionType.Nation, Era = FactionEra.Standard, DisplayColor = Color.Parse("#D32F2F"), FileName = "Dragon Empire.txt" };
    public static readonly ClanDefinition DarkStates = new() { Id = 2, Name = "Dark States", Type = FactionType.Nation, Era = FactionEra.Standard, DisplayColor = Color.Parse("#0D47A1"), FileName = "Dark States.txt" };
    public static readonly ClanDefinition KeterSanctuary = new() { Id = 3, Name = "Keter Sanctuary", Type = FactionType.Nation, Era = FactionEra.Standard, DisplayColor = Color.Parse("#FFC107"), FileName = "Keter Sanctuary.txt" };
    public static readonly ClanDefinition Stoicheia = new() { Id = 4, Name = "Stoicheia", Type = FactionType.Nation, Era = FactionEra.Standard, DisplayColor = Color.Parse("#388E3C"), FileName = "Stoicheia.txt" };
    public static readonly ClanDefinition BrandtGate = new() { Id = 5, Name = "Brandt Gate", Type = FactionType.Nation, Era = FactionEra.Standard, DisplayColor = Color.Parse("#BDBDBD"), FileName = "Brandt Gate.txt" };
    public static readonly ClanDefinition LyricalMonasterio = new() { Id = 6, Name = "Lyrical Monasterio", Type = FactionType.Nation, Era = FactionEra.Standard, DisplayColor = Color.Parse("#C48DB8"), FileName = "Lyrical Monasterio.txt" };

    // Other Nations
    public static readonly ClanDefinition MonsterStrike = new() { Id = 7, Name = "Monster Strike", Type = FactionType.Nation, Era = FactionEra.Crossover, DisplayColor = Color.Parse("#FF9800"), FileName = "Monster Strike.txt" };
    public static readonly ClanDefinition TechnicalNation = new() { Id = 8, Name = "Technical (Hidden)", Type = FactionType.Nation, Era = FactionEra.Other, DisplayColor = Color.Parse("#9E9E9E"), FileName = null };
    public static readonly ClanDefinition ShamanKing = new() { Id = 9, Name = "Shaman King", Type = FactionType.Nation, Era = FactionEra.Crossover, DisplayColor = Color.Parse("#FF9800"), FileName = "Shaman King.txt" };
    public static readonly ClanDefinition RecordOfRagnarok = new() { Id = 10, Name = "Record of Ragnarok", Type = FactionType.Nation, Era = FactionEra.Crossover, DisplayColor = Color.Parse("#FF9800"), FileName = "Record of Ragnarok.txt" };
    public static readonly ClanDefinition IronArmor = new() { Id = 11, Name = "Iron Armor Little Treasure", Type = FactionType.Nation, Era = FactionEra.Crossover, DisplayColor = Color.Parse("#FF9800"), FileName = "B-Robo Kabutack.txt" };
    public static readonly ClanDefinition Vspo = new() { Id = 12, Name = "VSPO!", Type = FactionType.Nation, Era = FactionEra.Crossover, DisplayColor = Color.Parse("#FF9800"), FileName = "VSPO.txt" };
    public static readonly ClanDefinition CoroCoro = new() { Id = 13, Name = "CoroCoro", Type = FactionType.Nation, Era = FactionEra.Crossover, DisplayColor = Color.Parse("#FF9800"), FileName = "Corocoro.txt" };
    public static readonly ClanDefinition Buddyfight = new() { Id = 14, Name = "Buddyfight", Type = FactionType.Nation, Era = FactionEra.Crossover, DisplayColor = Color.Parse("#FF9800"), FileName = "Buddyfight.txt" };

    // Clan color constants
    private static readonly Color ClanGrey = Color.Parse("#9E9E9E");
    private static readonly Color ClanBrandtGate = Color.Parse("#BDBDBD");     // Light grey
    private static readonly Color ClanDragonEmpire = Color.Parse("#D32F2F");   // Same as Dragon Empire nation
    private static readonly Color ClanDarkStates = Color.Parse("#0D47A1");     // Dark blue, same as Dark States nation
    private static readonly Color ClanSanctuary = Color.Parse("#FFC107");      // Same as Keter Sanctuary nation (United Sanctuary)
    private static readonly Color ClanMagellanica = Color.Parse("#5C9AC7");    // Aqua Force, Granblue, Bermuda
    private static readonly Color ClanZoo = Color.Parse("#6DAF6D");            // Megacolony, Great Nature, Neo Nectar
    private static readonly Color ClanPink = Color.Parse("#D4789C");           // Bang Dream
    private static readonly Color ClanLemon = Color.Parse("#E8D44D");          // Iconic

    // V/G Clans - Dragon Empire
    public static readonly ClanDefinition Kagero = new() { Id = 12, Name = "Kagero", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 1, DisplayColor = ClanDragonEmpire, FileName = "Kagero.txt" };
    public static readonly ClanDefinition Tachikaze = new() { Id = 17, Name = "Tachikaze", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 1, DisplayColor = ClanDragonEmpire, FileName = "Tachikaze.txt" };
    public static readonly ClanDefinition Narukami = new() { Id = 19, Name = "Narukami", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 1, DisplayColor = ClanDragonEmpire, FileName = "Narukami.txt" };
    public static readonly ClanDefinition Murakumo = new() { Id = 20, Name = "Murakumo", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 1, DisplayColor = ClanDragonEmpire, FileName = "Murakumo.txt" };
    public static readonly ClanDefinition Nubatama = new() { Id = 23, Name = "Nubatama", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 1, DisplayColor = ClanDragonEmpire, FileName = "Nubatama.txt" };

    // V/G Clans - Dark States
    public static readonly ClanDefinition DarkIrregulars = new() { Id = 11, Name = "Dark Irregulars", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 2, DisplayColor = ClanDarkStates, FileName = "Dark Irregulars.txt" };
    public static readonly ClanDefinition SpikeBrothers = new() { Id = 14, Name = "Spike Brothers", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 2, DisplayColor = ClanDarkStates, FileName = "Spike Brothers.txt" };
    public static readonly ClanDefinition PaleMoon = new() { Id = 18, Name = "Pale Moon", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 2, DisplayColor = ClanDarkStates, FileName = "Pale Moon.txt" };
    public static readonly ClanDefinition GearChronicle = new() { Id = 26, Name = "Gear Chronicle", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 2, DisplayColor = ClanDarkStates, FileName = "Gear Chronicle.txt" };

    // V/G Clans - United Sanctuary (Keter Sanctuary)
    public static readonly ClanDefinition ShadowPaladin = new() { Id = 1, Name = "Shadow Paladin", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 3, DisplayColor = ClanSanctuary, FileName = "Shadow Paladin.txt" };
    public static readonly ClanDefinition GoldPaladin = new() { Id = 5, Name = "Gold Paladin", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 3, DisplayColor = ClanSanctuary, FileName = "Gold Paladin.txt" };
    public static readonly ClanDefinition OracleThinkTank = new() { Id = 10, Name = "Oracle Think Tank", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 3, DisplayColor = ClanSanctuary, FileName = "Oracle.txt" };
    public static readonly ClanDefinition RoyalPaladin = new() { Id = 15, Name = "Royal Paladin", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 3, DisplayColor = ClanSanctuary, FileName = "Royal Paladin.txt" };
    public static readonly ClanDefinition AngelFeather = new() { Id = 16, Name = "Angel Feather", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 3, DisplayColor = ClanSanctuary, FileName = "Angle Feather.txt" };
    public static readonly ClanDefinition Genesis = new() { Id = 21, Name = "Genesis", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 3, DisplayColor = ClanSanctuary, FileName = "Genesis.txt" };

    // V/G Clans - Stoicheia (Zoo = green, Magellanica = blue)
    public static readonly ClanDefinition Megacolony = new() { Id = 2, Name = "Megacolony", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 4, DisplayColor = ClanZoo, FileName = "Megacolony.txt" };
    public static readonly ClanDefinition GreatNature = new() { Id = 7, Name = "Great Nature", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 4, DisplayColor = ClanZoo, FileName = "Great Nature.txt" };
    public static readonly ClanDefinition NeoNectar = new() { Id = 9, Name = "Neo Nectar", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 4, DisplayColor = ClanZoo, FileName = "Neo Nectar.txt" };
    public static readonly ClanDefinition AquaForce = new() { Id = 3, Name = "Aqua Force", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 4, DisplayColor = ClanMagellanica, FileName = "Aqua Force.txt" };
    public static readonly ClanDefinition Granblue = new() { Id = 8, Name = "Granblue", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 4, DisplayColor = ClanMagellanica, FileName = "Granblue.txt" };

    // V/G Clans - Star Gate (Brandt Gate) - light grey, no old nation distinction
    public static readonly ClanDefinition NovaGrappler = new() { Id = 4, Name = "Nova Grappler", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 5, DisplayColor = ClanBrandtGate, FileName = "Nova Grappler.txt" };
    public static readonly ClanDefinition DimensionPolice = new() { Id = 6, Name = "Dimension Police", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 5, DisplayColor = ClanBrandtGate, FileName = "Dimension Police.txt" };
    public static readonly ClanDefinition LinkJoker = new() { Id = 24, Name = "Link Joker", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 5, DisplayColor = ClanBrandtGate, FileName = "Link Joker.txt" };
    public static readonly ClanDefinition Etranger = new() { Id = 22, Name = "Etranger", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 5, DisplayColor = ClanBrandtGate, FileName = "Etrangers.txt" };
    public static readonly ClanDefinition MaskCollection = new() { Id = 28, Name = "Mask Collection", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 5, DisplayColor = ClanBrandtGate, FileName = "The Mask Collection.txt" };

    // V/G Clans - Lyrical (Bermuda = Magellanica = blue)
    public static readonly ClanDefinition BermudaTriangle = new() { Id = 13, Name = "Bermuda Triangle", Type = FactionType.Clan, Era = FactionEra.VPremium, ParentNationId = 6, DisplayColor = ClanMagellanica, FileName = "Bermuda.txt" };

    // Universal / Special Clans
    public static readonly ClanDefinition CrayElemental = new() { Id = 25, Name = "Cray Elemental", Type = FactionType.Clan, Era = FactionEra.Other, ParentNationId = 8, DisplayColor = ClanGrey, FileName = "Cray Elemental.txt" };
    public static readonly ClanDefinition ToukenRanbu = new() { Id = 27, Name = "Touken Ranbu", Type = FactionType.Clan, Era = FactionEra.Crossover, ParentNationId = 8, DisplayColor = ClanGrey, FileName = "Touken Ranbu.txt" };
    public static readonly ClanDefinition BangDream = new() { Id = 31, Name = "Bang Dream", Type = FactionType.Clan, Era = FactionEra.Crossover, ParentNationId = 8, DisplayColor = ClanPink, FileName = "Bang Dream.txt" };
    public static readonly ClanDefinition HiddenClan = new() { Id = 29, Name = "Hidden (Technical)", Type = FactionType.Clan, Era = FactionEra.Other, DisplayColor = Color.Parse("#616161") };
    public static readonly ClanDefinition OrderFilter = new() { Id = 30, Name = "Orders (Filter)", Type = FactionType.Clan, Era = FactionEra.Other, DisplayColor = ClanGrey, FileName = "Order Cards.txt" };

    // Collaboration Clans
    public static readonly ClanDefinition Game = new() { Id = 32, Name = "Game", Type = FactionType.Collaboration, Era = FactionEra.Crossover, ParentNationId = 5, DisplayColor = ClanGrey, FileName = "Game.txt" };
    public static readonly ClanDefinition Animation = new() { Id = 33, Name = "Animation", Type = FactionType.Collaboration, Era = FactionEra.Crossover, ParentNationId = 5, DisplayColor = ClanGrey, FileName = "Animation.txt" };
    public static readonly ClanDefinition Iconic = new() { Id = 34, Name = "Iconic", Type = FactionType.Collaboration, Era = FactionEra.Crossover, DisplayColor = ClanLemon, FileName = "Iconic.txt" };
    public static readonly ClanDefinition LiveAction = new() { Id = 35, Name = "Live Action", Type = FactionType.Collaboration, Era = FactionEra.Crossover, ParentNationId = 5, DisplayColor = ClanGrey, FileName = "Live Action.txt" };

    private static readonly Dictionary<int, ClanDefinition> _nationsById = new();
    private static readonly Dictionary<int, ClanDefinition> _clansById = new();

    private static readonly Color CustomNationColor = Color.Parse("#00897B");
    private static readonly Color CustomClanColor = Color.Parse("#26A69A");

    // Dynamic built-in factions get the same orange used by hardcoded crossover nations.
    private static readonly Color DynamicBuiltInNationColor = Color.Parse("#FF9800");
    private static readonly Color DynamicBuiltInClanColor = ClanGrey;

    private static readonly List<ClanDefinition> _builtInNations;
    private static readonly List<ClanDefinition> _builtInClans;
    private static readonly List<ClanDefinition> _customNations = new();
    private static readonly List<ClanDefinition> _customClans = new();
    private static readonly List<ClanDefinition> _dynamicBuiltInNations = new();
    private static readonly List<ClanDefinition> _dynamicBuiltInClans = new();

    public static IReadOnlyList<ClanDefinition> AllNations { get; private set; }
    public static IReadOnlyList<ClanDefinition> AllClans { get; private set; }
    public static IReadOnlyList<ClanDefinition> All { get; private set; }
    public static IReadOnlyList<ClanDefinition> CustomFactions => _customNations.Concat(_customClans).ToList();
    public static IReadOnlyList<ClanDefinition> DynamicBuiltInFactions => _dynamicBuiltInNations.Concat(_dynamicBuiltInClans).ToList();

    static ClanRegistry()
    {
        _builtInNations = new List<ClanDefinition>
        {
            DragonEmpire, DarkStates, KeterSanctuary, Stoicheia, BrandtGate, LyricalMonasterio,
            MonsterStrike, TechnicalNation, ShamanKing, RecordOfRagnarok, IronArmor, Vspo, CoroCoro, Buddyfight
        };

        _builtInClans = new List<ClanDefinition>
        {
            // Dragon Empire clans
            Kagero, Tachikaze, Narukami, Murakumo, Nubatama,
            // Dark States clans
            DarkIrregulars, SpikeBrothers, PaleMoon, GearChronicle,
            // Keter Sanctuary clans
            ShadowPaladin, GoldPaladin, OracleThinkTank, RoyalPaladin, AngelFeather, Genesis,
            // Stoicheia clans
            Megacolony, GreatNature, NeoNectar, AquaForce, Granblue,
            // Brandt Gate clans
            NovaGrappler, DimensionPolice, LinkJoker, Etranger, MaskCollection,
            // Lyrical clan
            BermudaTriangle,
            // Universal/Special
            CrayElemental, ToukenRanbu, BangDream, HiddenClan, OrderFilter,
            // Collaboration
            Game, Animation, Iconic, LiveAction
        };

        AllNations = _builtInNations.ToList();
        AllClans = _builtInClans.ToList();
        All = AllNations.Concat(AllClans).ToList();

        RebuildLookups();
    }

    private static void RebuildLookups()
    {
        _nationsById.Clear();
        _clansById.Clear();
        foreach (var n in AllNations)
            _nationsById[n.Id] = n;
        foreach (var c in AllClans)
            _clansById[c.Id] = c;
    }

    public static void RegisterCustomFactions(IEnumerable<ClanDefinition> factions)
    {
        _customNations.Clear();
        _customClans.Clear();

        foreach (var f in factions)
        {
            if (f.Type == FactionType.Nation)
                _customNations.Add(f);
            else
                _customClans.Add(f);
        }

        RebuildAggregateLists();
    }

    public static void ClearCustomFactions()
    {
        _customNations.Clear();
        _customClans.Clear();
        RebuildAggregateLists();
    }

    /// <summary>
    /// Registers built-in factions parsed from NoUse.txt's CustomFaction* array.
    /// Dynamic entries override hardcoded built-ins with the same Id+Type.
    /// </summary>
    public static void RegisterDynamicBuiltInFactions(IEnumerable<ClanDefinition> factions)
    {
        _dynamicBuiltInNations.Clear();
        _dynamicBuiltInClans.Clear();

        foreach (var f in factions)
        {
            if (f.Type == FactionType.Nation)
                _dynamicBuiltInNations.Add(f);
            else
                _dynamicBuiltInClans.Add(f);
        }

        RebuildAggregateLists();
    }

    public static void ClearDynamicBuiltInFactions()
    {
        _dynamicBuiltInNations.Clear();
        _dynamicBuiltInClans.Clear();
        RebuildAggregateLists();
    }

    private static void RebuildAggregateLists()
    {
        // Dynamic built-ins override hardcoded built-ins by Id.
        var dynamicNationIds = _dynamicBuiltInNations.Select(n => n.Id).ToHashSet();
        var dynamicClanIds = _dynamicBuiltInClans.Select(c => c.Id).ToHashSet();

        AllNations = _builtInNations.Where(n => !dynamicNationIds.Contains(n.Id))
            .Concat(_dynamicBuiltInNations)
            .Concat(_customNations)
            .ToList();
        AllClans = _builtInClans.Where(c => !dynamicClanIds.Contains(c.Id))
            .Concat(_dynamicBuiltInClans)
            .Concat(_customClans)
            .ToList();
        All = AllNations.Concat(AllClans).ToList();
        RebuildLookups();
    }

    public static ClanDefinition? GetNationById(int id) =>
        _nationsById.GetValueOrDefault(id);

    public static ClanDefinition? GetClanById(int id) =>
        _clansById.GetValueOrDefault(id);

    /// <summary>
    /// Gets files available for adding new cards, grouped and ordered per requirements:
    /// NoClan first, then 6 Standard Nations, then Other Nations, then V/G Clans, then Custom.
    /// </summary>
    public static IReadOnlyList<ClanDefinition> GetFileTargetsForNewCard()
    {
        var result = new List<ClanDefinition>();

        // NoClan files
        result.Add(CrayElemental);
        result.Add(OrderFilter);

        // Standard 6 Nations
        result.Add(DragonEmpire);
        result.Add(DarkStates);
        result.Add(KeterSanctuary);
        result.Add(Stoicheia);
        result.Add(BrandtGate);
        result.Add(LyricalMonasterio);

        // Other Nations
        result.Add(MonsterStrike);
        result.Add(ShamanKing);
        result.Add(RecordOfRagnarok);
        result.Add(IronArmor);
        result.Add(Vspo);
        result.Add(CoroCoro);
        result.Add(Buddyfight);

        // V/G Clans grouped by parent nation
        foreach (var clan in AllClans.Where(c => c.Era == FactionEra.VPremium))
            result.Add(clan);

        // Collaboration
        result.Add(Game);
        result.Add(Animation);
        result.Add(Iconic);
        result.Add(LiveAction);

        // Custom factions
        foreach (var f in _customNations)
            result.Add(f);
        foreach (var f in _customClans)
            result.Add(f);

        return result;
    }

    public static Color GetCustomNationColor() => CustomNationColor;
    public static Color GetCustomClanColor() => CustomClanColor;
    public static Color GetDynamicBuiltInNationColor() => DynamicBuiltInNationColor;
    public static Color GetDynamicBuiltInClanColor() => DynamicBuiltInClanColor;
}
