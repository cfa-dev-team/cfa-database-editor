using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace CfaDatabaseEditor.Models;

/// <summary>
/// Card data model. Uses plain auto-properties for fast parsing.
/// Implements INotifyPropertyChanged manually so the editor panel
/// can bind two-way to the currently selected card.
/// </summary>
public class Card : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly Encoding Win1251 = Encoding.GetEncoding(1251);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(CardName) or nameof(CardText))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasEncodingIssue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EncodingIssueDetails)));
        }
    }

    public bool HasEncodingIssue =>
        (!string.IsNullOrEmpty(_cardName) && !CanEncodeWin1251(_cardName)) ||
        (!string.IsNullOrEmpty(_cardText) && !CanEncodeWin1251(_cardText));

    public string EncodingIssueDetails
    {
        get
        {
            if (!HasEncodingIssue) return string.Empty;
            var bad = new List<char>();
            foreach (var text in new[] { _cardName, _cardText })
            {
                if (string.IsNullOrEmpty(text)) continue;
                foreach (char c in text)
                {
                    var bytes = Win1251.GetBytes(new[] { c });
                    var roundtrip = Win1251.GetString(bytes);
                    if (roundtrip[0] != c && !bad.Contains(c))
                        bad.Add(c);
                }
            }
            return $"Characters not in Windows-1251: {string.Join(", ", bad.Select(c => $"'{c}' (U+{(int)c:X4})"))}";
        }
    }

    private static bool CanEncodeWin1251(string text)
    {
        var bytes = Win1251.GetBytes(text);
        var roundtrip = Win1251.GetString(bytes);
        return roundtrip == text;
    }

    // === Core ===
    private int _cardStat;
    public int CardStat { get => _cardStat; set => Set(ref _cardStat, value); }

    private string _cardName = string.Empty;
    public string CardName { get => _cardName; set => Set(ref _cardName, value); }

    private string _cardText = string.Empty;
    public string CardText { get => _cardText; set => Set(ref _cardText, value); }

    private int _unitGrade;
    public int UnitGrade { get => _unitGrade; set => Set(ref _unitGrade, value); }

    private int _dCards;
    public int DCards { get => _dCards; set => Set(ref _dCards, value); }

    private int _powerStat;
    public int PowerStat { get => _powerStat; set => Set(ref _powerStat, value); }

    private int _defensePowerStat;
    public int DefensePowerStat { get => _defensePowerStat; set => Set(ref _defensePowerStat, value); }

    // === Nation/Clan ===
    public int? DCards2 { get; set; }
    public int? CardInClan { get; set; }
    public int? CardInClan2 { get; set; }
    public int? CardInClan3 { get; set; }
    public int? CardInClan4 { get; set; }
    public int? CardInClan5 { get; set; }
    public int? CardInClan6 { get; set; }
    public int? CardInClan7 { get; set; }
    public int? CardInClan8 { get; set; }
    public int? CardInClan9 { get; set; }

    // === Triggers ===
    private int? _triggerUnit;
    public int? TriggerUnit { get => _triggerUnit; set => Set(ref _triggerUnit, value); }

    // === Persona Ride ===
    public bool PersonaRide { get; set; }
    public bool PersonaRideAct { get; set; }
    public string? PersonaRideCardName { get; set; }
    public bool ForbidCrossPersonaRideUpon { get; set; }

    // === Imaginary Gifts on Ride ===
    public bool BlueTokenAdd { get; set; }
    public bool GreenTokenAdd { get; set; }
    public bool OrangeTokenAdd { get; set; }
    public bool BlueTokenBoth { get; set; }

    // === Gift Generators ===
    public bool ForceAdd { get; set; }
    public bool ProtectAdd { get; set; }
    public bool AccelAdd { get; set; }
    public bool GiftAdd { get; set; }
    public bool GiftAddSelect { get; set; }

    // === G Zone / Extra Deck ===
    public bool ExtraDeck { get; set; }
    public bool QuickShieldAdd { get; set; }

    // === Card Copies / Legality ===
    public int? CardCopies { get; set; }
    public bool CardLimidet { get; set; }
    public bool CardBanned { get; set; }

    // === Power Buffs ===
    public int? AttackFromVBuff { get; set; }
    public int? AttackFromRBuff { get; set; }
    public int? AttackFromVRBuff { get; set; }

    // === Back Row ===
    public bool CanAttackFromBackRow { get; set; }
    public bool EnableAttackFromBackRow { get; set; }

    // === Misc Mechanics ===
    public bool RemoveFromDrop { get; set; }
    public int? UnitGradeIncrementInDeck { get; set; }
    public int? TriggerPowerUpEffectVCRC { get; set; }
    public int? TriggerPowerUpEffectVC { get; set; }

    // === Arms ===
    public bool Arms { get; set; }
    public bool LeftArms { get; set; }
    public bool RightArms { get; set; }
    public bool ArmsAsUnit { get; set; }

    // === Special Mechanics ===
    public bool RegalisPiece { get; set; }
    public bool Uranus { get; set; }
    public bool MimicNotEvil { get; set; }
    public bool UseCounters { get; set; }
    public bool DeleteAllExtraInEndPhase { get; set; }

    private bool _extendedTextBox;
    public bool ExtendedTextBox { get => _extendedTextBox; set => Set(ref _extendedTextBox, value); }

    public bool TokenInHand { get; set; }

    // === Legacy Format ===
    public bool OldCardStat { get; set; }
    public bool NewTriggerStat { get; set; }

    // === Double-Face / Reassign ===
    public int? AnotherSide { get; set; }
    public int? CardReassignedId { get; set; }

    // === Visibility ===
    public bool DontShowInDeckEditor { get; set; }

    // === Token Summoner ===
    public int? TokenSummoner { get; set; }
    public int? TokenSummoner2 { get; set; }
    public int? TokenSummoner3 { get; set; }
    public string? TokenSummonerPosition { get; set; }
    public int? TokenSummonerQuantity { get; set; }
    public int? TokenSummonerRodeUponNumber { get; set; }
    public string? TokenSummoner2Text { get; set; }
    public string? TokenSummoner2Button1 { get; set; }
    public string? TokenSummoner2Button2 { get; set; }
    public string? TokenSummoner2Button3 { get; set; }

    // === Search Effect ===
    public bool SearchEffect { get; set; }
    public string? SearchEffectPosition { get; set; }
    public int? SearchEffectLookAtQuantity { get; set; }
    public string? SearchEffectMode { get; set; }
    public string? SearchEffectArgument1 { get; set; }
    public string? SearchEffectArgument2 { get; set; }
    public string? SearchEffectArgument3 { get; set; }
    public string? ActivateSearchFoundAction { get; set; }
    public string? ActivateSearchRestAction { get; set; }
    public int? SearchEffectFindQuantity { get; set; }

    // === Vanguard Requirement ===
    public string? RequiredVan { get; set; }

    // === Move All ===
    public string? MoveAll { get; set; }

    // === Reveal Top X ===
    public int? RevealTopX { get; set; }

    // === Card-Specific ===
    public bool Reveal { get; set; }
    public bool SceneEffect { get; set; }
    public bool CocoAdd { get; set; }

    // === Buddyfight ===
    public int? BuddyWorld { get; set; }
    public int? GaugeCharge { get; set; }

    // === Metadata (not serialized to GML) ===
    public string SourceFile { get; set; } = string.Empty;
    public int SourceLineStart { get; set; }
    public int SourceLineEnd { get; set; }
    public bool IsModified { get; set; }

    public string DisplayName => $"[{CardStat}] {CardName}";
}
