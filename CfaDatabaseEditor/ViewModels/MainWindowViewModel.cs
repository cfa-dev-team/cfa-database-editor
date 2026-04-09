using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CfaDatabaseEditor;
using CfaDatabaseEditor.Helpers;
using CfaDatabaseEditor.Models;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DatabaseService _db = new();
    private ImageService? _imageService;
    private List<Card> _allCardsList = new();

    // Status
    [ObservableProperty] private string _statusText = "No database loaded";
    [ObservableProperty] private bool _isLoaded;

    // Card list
    [ObservableProperty] private ObservableCollection<Card> _filteredCards = new();
    [ObservableProperty] private Card? _selectedCard;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ClanDefinition? _selectedNationFilter;
    [ObservableProperty] private ClanDefinition? _selectedClanFilter;
    [ObservableProperty] private bool _filterNoNation;
    [ObservableProperty] private bool _filterNoClan;
    [ObservableProperty] private int _selectedGradeFilter; // -1 = Any, 0-4 = exact, 5 = 5+

    // Filter options
    public ObservableCollection<ClanDefinition> NationOptions { get; } = new();
    public ObservableCollection<ClanDefinition> ClanOptions { get; } = new();
    public List<string> GradeOptions { get; } = new() { "Any Grade", "Grade 0", "Grade 1", "Grade 2", "Grade 3", "Grade 4", "Grade 5+" };

    // Card editor - bound to selected card
    [ObservableProperty] private string? _cardImagePath;

    // New card targets
    public IReadOnlyList<ClanDefinition> NewCardTargets => ClanRegistry.GetFileTargetsForNewCard();

    public DatabaseService Database => _db;

    private static readonly ClanDefinition AllNationsSentinel = new() { Id = -1, Name = "All Nations", Type = FactionType.Nation, DisplayColor = Avalonia.Media.Color.Parse("#808080") };
    private static readonly ClanDefinition AllClansSentinel = new() { Id = -1, Name = "All Clans", Type = FactionType.Clan, DisplayColor = Avalonia.Media.Color.Parse("#808080") };

    public MainWindowViewModel()
    {
        // Populate filter options with reset entries
        NationOptions.Add(AllNationsSentinel);
        foreach (var n in ClanRegistry.AllNations.Where(n => n.Era != FactionEra.Other))
            NationOptions.Add(n);
        ClanOptions.Add(AllClansSentinel);
        foreach (var c in ClanRegistry.AllClans.Where(c => c.Era != FactionEra.Other))
            ClanOptions.Add(c);

        SelectedNationFilter = AllNationsSentinel;
        SelectedClanFilter = AllClansSentinel;
    }

    partial void OnSelectedCardChanged(Card? value)
    {
        if (value != null)
            CardImagePath = _db.GetCardImagePath(value.CardStat);
        else
            CardImagePath = null;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedNationFilterChanged(ClanDefinition? value) => ApplyFilters();
    partial void OnSelectedClanFilterChanged(ClanDefinition? value) => ApplyFilters();
    partial void OnFilterNoNationChanged(bool value) => ApplyFilters();
    partial void OnSelectedGradeFilterChanged(int value) => ApplyFilters();
    partial void OnFilterNoClanChanged(bool value) => ApplyFilters();

    public async Task LoadFromPathAsync(string path)
    {

        StatusText = "Loading database...";
        try
        {
            Program.Log?.WriteLine($"[INFO] Opening folder: {path}");
            Program.Log?.WriteLine($"[INFO] Starting LoadDatabaseAsync...");
            await _db.LoadDatabaseAsync(path);
            Program.Log?.WriteLine($"[INFO] Loaded {_db.AllCards.Count} cards from {_db.CardFiles.Count} files");

            _imageService = new ImageService(_db);
            _allCardsList = new List<Card>(_db.AllCards);
            Program.Log?.WriteLine($"[INFO] Created card list copy");

            IsLoaded = true;
            Program.Log?.WriteLine($"[INFO] Calling ApplyFilters...");
            ApplyFilters();
            Program.Log?.WriteLine($"[INFO] ApplyFilters done, FilteredCards.Count={FilteredCards.Count}");
            StatusText = $"{_db.AllCards.Count} cards loaded from {_db.CardFiles.Count} files";
        }
        catch (Exception ex)
        {
            Program.Log?.WriteLine($"[ERROR] {ex}");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!_db.IsLoaded) return;

        StatusText = "Saving...";
        try
        {
            _db.SaveModifiedFiles();

            // Regenerate MD5 checksums
            if (_db.RootPath != null)
                await Md5ChecksumGenerator.RegenerateChecksumsAsync(_db.RootPath);

            StatusText = "Saved successfully";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddNewCard(ClanDefinition target)
    {
        if (!_db.IsLoaded || target.FileName == null) return;

        var card = _db.CreateNewCard(target.FileName);
        card.CardName = "New Card";
        card.DCards = target.Type == FactionType.Nation ? target.Id : 0;
        if (target.Type == FactionType.Clan)
            card.CardInClan = target.Id;

        _allCardsList = new List<Card>(_db.AllCards);
        ApplyFilters();
        SelectedCard = card;
        StatusText = $"Created card #{card.CardStat} in {target.FileName}";
    }

    [RelayCommand]
    private void DuplicateCard()
    {
        if (!_db.IsLoaded || SelectedCard == null) return;

        var src = SelectedCard;
        var card = _db.CreateNewCard(src.SourceFile);

        // Core
        card.CardName = src.CardName;
        card.CardText = src.CardText;
        card.UnitGrade = src.UnitGrade;
        card.DCards = src.DCards;
        card.PowerStat = src.PowerStat;
        card.DefensePowerStat = src.DefensePowerStat;

        // Nation/Clan
        card.DCards2 = src.DCards2;
        card.CardInClan = src.CardInClan;
        card.CardInClan2 = src.CardInClan2;
        card.CardInClan3 = src.CardInClan3;
        card.CardInClan4 = src.CardInClan4;
        card.CardInClan5 = src.CardInClan5;
        card.CardInClan6 = src.CardInClan6;
        card.CardInClan7 = src.CardInClan7;
        card.CardInClan8 = src.CardInClan8;
        card.CardInClan9 = src.CardInClan9;

        // Triggers
        card.TriggerUnit = src.TriggerUnit;

        // Persona Ride
        card.PersonaRide = src.PersonaRide;
        card.PersonaRideAct = src.PersonaRideAct;
        card.PersonaRideCardName = src.PersonaRideCardName;
        card.ForbidCrossPersonaRideUpon = src.ForbidCrossPersonaRideUpon;

        // Imaginary Gifts
        card.BlueTokenAdd = src.BlueTokenAdd;
        card.GreenTokenAdd = src.GreenTokenAdd;
        card.OrangeTokenAdd = src.OrangeTokenAdd;
        card.BlueTokenBoth = src.BlueTokenBoth;

        // Gift Generators
        card.ForceAdd = src.ForceAdd;
        card.ProtectAdd = src.ProtectAdd;
        card.AccelAdd = src.AccelAdd;
        card.GiftAdd = src.GiftAdd;
        card.GiftAddSelect = src.GiftAddSelect;

        // G Zone
        card.ExtraDeck = src.ExtraDeck;
        card.QuickShieldAdd = src.QuickShieldAdd;

        // Legality
        card.CardCopies = src.CardCopies;
        card.CardLimidet = src.CardLimidet;
        card.CardBanned = src.CardBanned;

        // Power Buffs
        card.AttackFromVBuff = src.AttackFromVBuff;
        card.AttackFromRBuff = src.AttackFromRBuff;
        card.AttackFromVRBuff = src.AttackFromVRBuff;

        // Back Row
        card.CanAttackFromBackRow = src.CanAttackFromBackRow;
        card.EnableAttackFromBackRow = src.EnableAttackFromBackRow;

        // Misc
        card.RemoveFromDrop = src.RemoveFromDrop;
        card.UnitGradeIncrementInDeck = src.UnitGradeIncrementInDeck;
        card.TriggerPowerUpEffectVCRC = src.TriggerPowerUpEffectVCRC;
        card.TriggerPowerUpEffectVC = src.TriggerPowerUpEffectVC;

        // Arms
        card.Arms = src.Arms;
        card.LeftArms = src.LeftArms;
        card.RightArms = src.RightArms;
        card.ArmsAsUnit = src.ArmsAsUnit;

        // Special
        card.RegalisPiece = src.RegalisPiece;
        card.Uranus = src.Uranus;
        card.MimicNotEvil = src.MimicNotEvil;
        card.UseCounters = src.UseCounters;
        card.DeleteAllExtraInEndPhase = src.DeleteAllExtraInEndPhase;
        card.ExtendedTextBox = src.ExtendedTextBox;
        card.TokenInHand = src.TokenInHand;

        // Legacy
        card.OldCardStat = src.OldCardStat;
        card.NewTriggerStat = src.NewTriggerStat;

        // Double-face / Reassign
        card.AnotherSide = src.AnotherSide;
        card.CardReassignedId = src.CardReassignedId;

        // Visibility
        card.DontShowInDeckEditor = src.DontShowInDeckEditor;

        // Token Summoner
        card.TokenSummoner = src.TokenSummoner;
        card.TokenSummoner2 = src.TokenSummoner2;
        card.TokenSummoner3 = src.TokenSummoner3;
        card.TokenSummonerPosition = src.TokenSummonerPosition;
        card.TokenSummonerQuantity = src.TokenSummonerQuantity;
        card.TokenSummonerRodeUponNumber = src.TokenSummonerRodeUponNumber;
        card.TokenSummoner2Text = src.TokenSummoner2Text;
        card.TokenSummoner2Button1 = src.TokenSummoner2Button1;
        card.TokenSummoner2Button2 = src.TokenSummoner2Button2;
        card.TokenSummoner2Button3 = src.TokenSummoner2Button3;

        // Search Effect
        card.SearchEffect = src.SearchEffect;
        card.SearchEffectPosition = src.SearchEffectPosition;
        card.SearchEffectLookAtQuantity = src.SearchEffectLookAtQuantity;
        card.SearchEffectMode = src.SearchEffectMode;
        card.SearchEffectArgument1 = src.SearchEffectArgument1;
        card.SearchEffectArgument2 = src.SearchEffectArgument2;
        card.SearchEffectArgument3 = src.SearchEffectArgument3;
        card.ActivateSearchFoundAction = src.ActivateSearchFoundAction;
        card.ActivateSearchRestAction = src.ActivateSearchRestAction;
        card.SearchEffectFindQuantity = src.SearchEffectFindQuantity;

        // Other
        card.RequiredVan = src.RequiredVan;
        card.MoveAll = src.MoveAll;
        card.RevealTopX = src.RevealTopX;
        card.Reveal = src.Reveal;
        card.SceneEffect = src.SceneEffect;
        card.CocoAdd = src.CocoAdd;
        card.BuddyWorld = src.BuddyWorld;
        card.GaugeCharge = src.GaugeCharge;

        _allCardsList = new List<Card>(_db.AllCards);
        ApplyFilters();
        SelectedCard = card;
        StatusText = $"Duplicated card #{src.CardStat} → #{card.CardStat} in {src.SourceFile}";
    }

    public void ReplaceImageFromFile(string path)
    {
        if (SelectedCard == null || _imageService == null) return;

        try
        {
            _imageService.ImportCardImage(SelectedCard.CardStat, path);
            CardImagePath = null;
            CardImagePath = _db.GetCardImagePath(SelectedCard.CardStat);
            StatusText = $"Image replaced for card #{SelectedCard.CardStat}";
        }
        catch (Exception ex)
        {
            StatusText = $"Image error: {ex.Message}";
        }
    }

    public void MarkCardModified()
    {
        if (SelectedCard == null) return;
        SelectedCard.IsModified = true;

        var file = _db.CardFiles.FirstOrDefault(f =>
            Path.GetFileName(f.FilePath).Equals(SelectedCard.SourceFile, StringComparison.OrdinalIgnoreCase));
        if (file != null)
            file.IsModified = true;

        var modCount = _db.GetModifiedFileCount();
        StatusText = $"{_db.AllCards.Count} cards loaded | {modCount} file(s) modified";
    }

    public void RefreshCardList()
    {
        _allCardsList = new List<Card>(_db.AllCards);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = _allCardsList.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            filtered = filtered.Where(c =>
                c.CardName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.CardText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.CardStat.ToString() == search);
        }

        if (FilterNoNation)
        {
            filtered = filtered.Where(c => c.DCards == 0);
        }
        else if (SelectedNationFilter != null && SelectedNationFilter != AllNationsSentinel)
        {
            var nationId = SelectedNationFilter.Id;
            filtered = filtered.Where(c => c.DCards == nationId || c.DCards2 == nationId);
        }

        if (FilterNoClan)
        {
            filtered = filtered.Where(c => !c.CardInClan.HasValue);
        }
        else if (SelectedClanFilter != null && SelectedClanFilter != AllClansSentinel)
        {
            var clanId = SelectedClanFilter.Id;
            filtered = filtered.Where(c =>
                c.CardInClan == clanId || c.CardInClan2 == clanId ||
                c.CardInClan3 == clanId || c.CardInClan4 == clanId ||
                c.CardInClan5 == clanId || c.CardInClan6 == clanId ||
                c.CardInClan7 == clanId || c.CardInClan8 == clanId ||
                c.CardInClan9 == clanId);
        }

        // Grade filter: 0 = Any, 1 = Grade 0, 2 = Grade 1, ... 5 = Grade 4, 6 = Grade 5+
        if (SelectedGradeFilter > 0)
        {
            int grade = SelectedGradeFilter - 1; // map index to grade value
            if (grade >= 5)
                filtered = filtered.Where(c => c.UnitGrade >= 5);
            else
                filtered = filtered.Where(c => c.UnitGrade == grade);
        }

        FilteredCards = new ObservableCollection<Card>(filtered);
    }
}
