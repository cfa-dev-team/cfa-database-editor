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
    private readonly GitService _git = new();
    private ImageService? _imageService;
    private List<Card> _allCardsList = new();

    // Status
    [ObservableProperty] private string _statusText = "No database loaded";
    [ObservableProperty] private bool _isLoaded;

    // Git
    [ObservableProperty] private string _gitBranchDisplay = "";
    [ObservableProperty] private Avalonia.Media.IBrush _gitBranchColor = Avalonia.Media.Brushes.Gray;
    [ObservableProperty] private bool _isGitRepo;

    public GitService Git => _git;

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
    [ObservableProperty] private string _duplicateNameWarning = string.Empty;

    // New card targets - refreshed after loading to include custom factions
    [ObservableProperty] private ObservableCollection<ClanDefinition> _newCardTargets = new();

    public DatabaseService Database => _db;

    private static readonly ClanDefinition AllNationsSentinel = new() { Id = -1, Name = "All Nations", Type = FactionType.Nation, DisplayColor = Avalonia.Media.Color.Parse("#808080") };
    private static readonly ClanDefinition AllClansSentinel = new() { Id = -1, Name = "All Clans", Type = FactionType.Clan, DisplayColor = Avalonia.Media.Color.Parse("#808080") };

    public MainWindowViewModel()
    {
        RebuildFilterOptions();
    }

    public void RebuildFilterOptions()
    {
        NationOptions.Clear();
        NationOptions.Add(AllNationsSentinel);
        foreach (var n in ClanRegistry.AllNations.Where(n => n.Era != FactionEra.Other))
            NationOptions.Add(n);

        ClanOptions.Clear();
        ClanOptions.Add(AllClansSentinel);
        foreach (var c in ClanRegistry.AllClans.Where(c => c.Era != FactionEra.Other))
            ClanOptions.Add(c);

        SelectedNationFilter = AllNationsSentinel;
        SelectedClanFilter = AllClansSentinel;

        NewCardTargets = new ObservableCollection<ClanDefinition>(ClanRegistry.GetFileTargetsForNewCard());
    }

    private Card? _subscribedCard;

    partial void OnSelectedCardChanged(Card? value)
    {
        if (_subscribedCard != null)
            _subscribedCard.PropertyChanged -= OnSelectedCardPropertyChanged;

        _subscribedCard = value;

        if (value != null)
        {
            value.PropertyChanged += OnSelectedCardPropertyChanged;
            CardImagePath = _db.GetCardImagePath(value.CardStat);
            CheckDuplicateName();
        }
        else
        {
            CardImagePath = null;
            DuplicateNameWarning = string.Empty;
        }
    }

    private static readonly HashSet<string> MetadataProperties = new()
    {
        nameof(Card.IsModified), nameof(Card.IsCustomCard),
        nameof(Card.SourceFile), nameof(Card.SourceLineStart), nameof(Card.SourceLineEnd),
        nameof(Card.DisplayName), nameof(Card.HasEncodingIssue), nameof(Card.EncodingIssueDetails)
    };

    private void OnSelectedCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Card.CardName))
            CheckDuplicateName();

        // Auto-mark card (and its file) as modified when any data property changes
        if (e.PropertyName != null && !MetadataProperties.Contains(e.PropertyName))
            MarkCardModified();
    }

    private void CheckDuplicateName()
    {
        if (SelectedCard == null || string.IsNullOrEmpty(SelectedCard.CardName))
        {
            DuplicateNameWarning = string.Empty;
            return;
        }

        var duplicate = _allCardsList.FirstOrDefault(c =>
            c != SelectedCard && c.CardName == SelectedCard.CardName);

        DuplicateNameWarning = duplicate != null
            ? $"Duplicate name! Card #{duplicate.CardStat} has the same name. CFA requires unique card names."
            : string.Empty;
    }

    [RelayCommand]
    private void FixDuplicateName()
    {
        if (SelectedCard == null) return;

        var name = SelectedCard.CardName + " ";
        var names = new HashSet<string>(_allCardsList
            .Where(c => c != SelectedCard)
            .Select(c => c.CardName));

        while (names.Contains(name))
            name += " ";

        SelectedCard.CardName = name;
        MarkCardModified();
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

            // Rebuild filter options to include custom factions
            RebuildFilterOptions();

            IsLoaded = true;
            Program.Log?.WriteLine($"[INFO] Calling ApplyFilters...");
            ApplyFilters();
            Program.Log?.WriteLine($"[INFO] ApplyFilters done, FilteredCards.Count={FilteredCards.Count}");
            StatusText = $"{_db.AllCards.Count} cards loaded from {_db.CardFiles.Count} files";

            // Initialise git (non-blocking — if git isn't available, fields stay hidden)
            await InitGitAsync(path);
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
                await Md5ChecksumGenerator.RegenerateChecksumsAsync(_db.RootPath, _db.BuiltInAllCardValue);

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

        Card card;
        if (target.IsCustom)
        {
            try
            {
                card = _db.CreateNewCustomCard(target.FileName);
            }
            catch (InvalidOperationException ex)
            {
                StatusText = ex.Message;
                return;
            }
        }
        else
        {
            card = _db.CreateNewCard(target.FileName);
        }

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
        Card card;
        if (src.IsCustomCard)
        {
            try { card = _db.CreateNewCustomCard(src.SourceFile); }
            catch (InvalidOperationException ex) { StatusText = ex.Message; return; }
        }
        else
        {
            card = _db.CreateNewCard(src.SourceFile);
        }

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

    public void RefreshAfterCustomFactionChange()
    {
        RebuildFilterOptions();
        ApplyFilters();
    }

    // ── Git ──

    public async Task InitGitAsync(string path)
    {
        try
        {
            await _git.InitAsync(path);
            IsGitRepo = _git.IsRepository;
            UpdateGitBranchDisplay();
        }
        catch (Exception ex)
        {
            Program.Log?.WriteLine($"[WARN] Git init failed: {ex.Message}");
            IsGitRepo = false;
        }
    }

    public async Task RefreshGitStatusAsync()
    {
        if (!_git.IsRepository) return;
        await _git.RefreshStatusAsync();
        UpdateGitBranchDisplay();
    }

    private void UpdateGitBranchDisplay()
    {
        if (!_git.IsRepository)
        {
            GitBranchDisplay = "";
            return;
        }

        var display = $"\ue0a0 {_git.CurrentBranch}"; // branch icon
        if (_git.AheadCount > 0) display += $" \u2191{_git.AheadCount}";
        if (_git.BehindCount > 0) display += $" \u2193{_git.BehindCount}";
        if (_git.HasChanges) display += " *";
        GitBranchDisplay = display;

        // Color: green = clean, yellow = dirty, blue = ahead/behind
        if (_git.HasChanges)
            GitBranchColor = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E65100"));
        else if (_git.AheadCount > 0 || _git.BehindCount > 0)
            GitBranchColor = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1565C0"));
        else
            GitBranchColor = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2E7D32"));
    }

    /// <summary>Reload the database from the same path (used after checkout/pull).</summary>
    public async Task ReloadDatabaseAsync()
    {
        if (_db.RootPath == null) return;
        await LoadFromPathAsync(_db.RootPath);
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
