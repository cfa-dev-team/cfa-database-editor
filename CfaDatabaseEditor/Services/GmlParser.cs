using System.Text;
using System.Text.RegularExpressions;
using CfaDatabaseEditor.Models;

namespace CfaDatabaseEditor.Services;

public static class GmlParser
{
    private static readonly Encoding Win1251 = Encoding.GetEncoding(1251);

    public static CardFile ParseFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Win1251).ToList();
        var cardFile = new CardFile
        {
            FilePath = filePath,
            RawLines = lines
        };

        var cards = new List<Card>();
        int currentCardStat = -1;
        int currentCardStartLine = -1;
        Card? currentCard = null;
        bool inMultiLineString = false;
        string multiLinePropName = "";
        string multiLineArrayIndex = "";
        StringBuilder multiLineValue = new();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Handle multi-line string continuation
            if (inMultiLineString)
            {
                multiLineValue.AppendLine();
                multiLineValue.Append(line);

                // Check if this line closes the string (contains unescaped closing single quote)
                if (HasClosingSingleQuote(line))
                {
                    inMultiLineString = false;
                    var fullValue = multiLineValue.ToString();
                    if (currentCard != null)
                        SetCardProperty(currentCard, multiLinePropName, multiLineArrayIndex, fullValue);
                }
                continue;
            }

            // Skip empty lines, braces, comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "{" || trimmed == "}")
                continue;
            if (trimmed.StartsWith("//"))
                continue;

            // Check for CardStat assignment
            var cardStatMatch = Regex.Match(trimmed, @"^CardStat\s*=\s*(\d+)");
            if (cardStatMatch.Success)
            {
                // Finalize previous card
                if (currentCard != null)
                {
                    currentCard.SourceLineEnd = i - 1;
                    // Trim trailing blank lines from the card's range
                    while (currentCard.SourceLineEnd > currentCard.SourceLineStart &&
                           string.IsNullOrWhiteSpace(lines[currentCard.SourceLineEnd]))
                        currentCard.SourceLineEnd--;
                    cards.Add(currentCard);
                }

                currentCardStat = int.Parse(cardStatMatch.Groups[1].Value);
                currentCardStartLine = i;
                currentCard = new Card
                {
                    CardStat = currentCardStat,
                    SourceFile = Path.GetFileName(filePath),
                    SourceLineStart = i
                };
                continue;
            }

            // Check for global property assignment
            if (currentCard != null && trimmed.StartsWith("global."))
            {
                ParsePropertyLine(trimmed, currentCard, ref inMultiLineString,
                    ref multiLinePropName, ref multiLineArrayIndex, multiLineValue);
            }
        }

        // Finalize last card
        if (currentCard != null)
        {
            currentCard.SourceLineEnd = lines.Count - 1;
            while (currentCard.SourceLineEnd > currentCard.SourceLineStart &&
                   string.IsNullOrWhiteSpace(lines[currentCard.SourceLineEnd]))
                currentCard.SourceLineEnd--;
            cards.Add(currentCard);
        }

        cardFile.Cards = cards;
        return cardFile;
    }

    private static void ParsePropertyLine(string line, Card card,
        ref bool inMultiLineString, ref string multiLinePropName,
        ref string multiLineArrayIndex, StringBuilder multiLineValue)
    {
        // Remove inline comments (but not inside strings)
        var commentFreeeLine = RemoveInlineComment(line);

        // Match: global.PropertyName[CardStat] = value
        // or: global.PropertyName[CardStat, index] = value
        var match = Regex.Match(commentFreeeLine,
            @"^global\.(\w+)\[CardStat(?:,\s*(\d+))?\]\s*=\s*(.+)$");
        if (!match.Success) return;

        var propName = match.Groups[1].Value;
        var arrayIndex = match.Groups[2].Value;
        var rawValue = match.Groups[3].Value.TrimEnd();

        // Check if this is a string value that starts but doesn't end on this line
        if (rawValue.StartsWith("'") && !HasClosingSingleQuote(rawValue.Substring(1)))
        {
            inMultiLineString = true;
            multiLinePropName = propName;
            multiLineArrayIndex = arrayIndex;
            multiLineValue.Clear();
            multiLineValue.Append(rawValue);
            return;
        }

        // Check for double-quoted strings
        if (rawValue.StartsWith("\"") && !rawValue.EndsWith("\""))
        {
            inMultiLineString = true;
            multiLinePropName = propName;
            multiLineArrayIndex = arrayIndex;
            multiLineValue.Clear();
            multiLineValue.Append(rawValue);
            return;
        }

        SetCardProperty(card, propName, arrayIndex, rawValue);
    }

    private static bool HasClosingSingleQuote(string text)
    {
        // A closing single quote is a regular ASCII single quote (0x27)
        // The right single quotation mark (U+2019, Windows-1251 0x92) is used as apostrophe
        // and does NOT close the string
        for (int i = text.Length - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c == '\'') return true;
            if (c != ' ' && c != '\t' && c != '\r' && c != '\n') return false;
        }
        return false;
    }

    private static string RemoveInlineComment(string line)
    {
        // Don't remove // inside strings
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        for (int i = 0; i < line.Length - 1; i++)
        {
            char c = line[i];
            if (c == '\'' && !inDoubleQuote) inSingleQuote = !inSingleQuote;
            if (c == '"' && !inSingleQuote) inDoubleQuote = !inDoubleQuote;
            if (!inSingleQuote && !inDoubleQuote && c == '/' && line[i + 1] == '/')
                return line.Substring(0, i).TrimEnd();
        }
        return line;
    }

    private static void SetCardProperty(Card card, string propName, string arrayIndex, string rawValue)
    {
        var strVal = UnquoteString(rawValue);
        var intVal = TryParseInt(rawValue);
        var boolVal = rawValue.Trim() == "1";

        switch (propName)
        {
            // Core
            case "CardName": card.CardName = strVal ?? rawValue; break;
            case "CardText": card.CardText = strVal ?? rawValue; break;
            case "UnitGrade": card.UnitGrade = intVal ?? 0; break;
            case "DCards": card.DCards = intVal ?? 0; break;
            case "DCards2": card.DCards2 = intVal; break;
            case "PowerStat": card.PowerStat = intVal ?? 0; break;
            case "DefensePowerStat": card.DefensePowerStat = intVal ?? 0; break;

            // Clans
            case "CardInClan": card.CardInClan = intVal; break;
            case "CardInClan2": card.CardInClan2 = intVal; break;
            case "CardInClan3": card.CardInClan3 = intVal; break;
            case "CardInClan4": card.CardInClan4 = intVal; break;
            case "CardInClan5": card.CardInClan5 = intVal; break;
            case "CardInClan6": card.CardInClan6 = intVal; break;
            case "CardInClan7": card.CardInClan7 = intVal; break;
            case "CardInClan8": card.CardInClan8 = intVal; break;
            case "CardInClan9": card.CardInClan9 = intVal; break;

            // Triggers
            case "TriggerUnit": card.TriggerUnit = intVal ?? 0; break;

            // Persona Ride
            case "PersonaRide": card.PersonaRide = boolVal; break;
            case "PersonaRideAct": card.PersonaRideAct = boolVal; break;
            case "PersonaRideCardName": card.PersonaRideCardName = strVal ?? rawValue.Trim(); break;
            case "ForbidCrossPersonaRideUpon": card.ForbidCrossPersonaRideUpon = boolVal; break;

            // Imaginary Gifts on Ride
            case "BlueTokenAdd": card.BlueTokenAdd = boolVal; break;
            case "GreenTokenAdd": card.GreenTokenAdd = boolVal; break;
            case "OrangeTokenAdd": card.OrangeTokenAdd = boolVal; break;
            case "BlueTokenBoth": card.BlueTokenBoth = boolVal; break;

            // Gift Generators
            case "ForceAdd": card.ForceAdd = boolVal; break;
            case "ProtectAdd": card.ProtectAdd = boolVal; break;
            case "AccelAdd": card.AccelAdd = boolVal; break;
            case "GiftAdd": card.GiftAdd = boolVal; break;
            case "GiftAddSelect": card.GiftAddSelect = boolVal; break;

            // G Zone
            case "ExtraDeck": card.ExtraDeck = boolVal; break;
            case "QuickShieldAdd": card.QuickShieldAdd = boolVal; break;

            // Copies / Legality
            case "CardCopies": card.CardCopies = intVal; break;
            case "CardLimidet": card.CardLimidet = boolVal; break;
            case "CardBanned": card.CardBanned = boolVal; break;

            // Power Buffs
            case "AttackFromVBuff": card.AttackFromVBuff = intVal; break;
            case "AttackFromRBuff": card.AttackFromRBuff = intVal; break;
            case "AttackFromVRBuff": card.AttackFromVRBuff = intVal; break;

            // Back Row
            case "CanAttackFromBackRow": card.CanAttackFromBackRow = boolVal; break;
            case "EnableAttackFromBackRow": card.EnableAttackFromBackRow = boolVal; break;

            // Misc
            case "RemoveFromDrop": card.RemoveFromDrop = boolVal; break;
            case "UnitGradeIncrementInDeck": card.UnitGradeIncrementInDeck = intVal; break;
            case "TriggerPowerUpEffect":
                if (arrayIndex == "0") card.TriggerPowerUpEffectVCRC = intVal;
                else if (arrayIndex == "1") card.TriggerPowerUpEffectVC = intVal;
                break;

            // Arms
            case "Arms": card.Arms = boolVal; break;
            case "LeftArms": card.LeftArms = boolVal; break;
            case "RightArms": card.RightArms = boolVal; break;
            case "ArmsAsUnit": card.ArmsAsUnit = boolVal; break;
            case "ArmAsUnit": card.ArmsAsUnit = boolVal; break;

            // Special
            case "RegalisPiece": card.RegalisPiece = boolVal; break;
            case "Uranus": card.Uranus = boolVal; break;
            case "MimicNotEvil": card.MimicNotEvil = boolVal; break;
            case "UseCounters": card.UseCounters = boolVal; break;
            case "DeleteAllExtraInEndPhase": card.DeleteAllExtraInEndPhase = boolVal; break;
            case "ExtendedTextBox": card.ExtendedTextBox = boolVal; break;
            case "TokenInHand": card.TokenInHand = boolVal; break;

            // Legacy
            case "OldCardStat": card.OldCardStat = boolVal; break;
            case "NewTriggerStat": card.NewTriggerStat = boolVal; break;

            // Double-face / Reassign
            case "AnotherSide": card.AnotherSide = intVal; break;
            case "CardReassignedId": card.CardReassignedId = intVal; break;

            // Visibility
            case "DontShowInDeckEditor": card.DontShowInDeckEditor = boolVal; break;

            // Token Summoner
            case "TokenSummoner": card.TokenSummoner = intVal; break;
            case "TokenSummoner2": card.TokenSummoner2 = intVal; break;
            case "TokenSummoner3": card.TokenSummoner3 = intVal; break;
            case "TokenSummonerPosition": card.TokenSummonerPosition = strVal; break;
            case "TokenSummonerQuantity": card.TokenSummonerQuantity = intVal; break;
            case "TokenSummonerRodeUponNumber": card.TokenSummonerRodeUponNumber = intVal; break;
            case "TokenSummoner2Text": card.TokenSummoner2Text = UnquoteDoubleString(rawValue) ?? strVal; break;
            case "TokenSummoner2Button1": card.TokenSummoner2Button1 = UnquoteDoubleString(rawValue) ?? strVal; break;
            case "TokenSummoner2Button2": card.TokenSummoner2Button2 = UnquoteDoubleString(rawValue) ?? strVal; break;
            case "TokenSummoner2Button3": card.TokenSummoner2Button3 = UnquoteDoubleString(rawValue) ?? strVal; break;

            // Search Effect
            case "SearchEffect": card.SearchEffect = boolVal; break;
            case "SearchEffectPosition": card.SearchEffectPosition = strVal; break;
            case "SearchEffectLookAtQuantity": card.SearchEffectLookAtQuantity = intVal; break;
            case "SearchEffectMode": card.SearchEffectMode = strVal; break;
            case "SearchEffectArgument1":
                card.SearchEffectArgument1 = strVal ?? rawValue.Trim();
                break;
            case "SearchEffectArgument2":
                card.SearchEffectArgument2 = strVal ?? rawValue.Trim();
                break;
            case "SearchEffectArgument3":
                card.SearchEffectArgument3 = strVal ?? rawValue.Trim();
                break;
            case "ActivateSearchFoundAction": card.ActivateSearchFoundAction = strVal; break;
            case "ActivateSearchRestAction": card.ActivateSearchRestAction = strVal; break;
            case "SearchEffectFindQuantity": card.SearchEffectFindQuantity = intVal; break;

            // Vanguard Requirement
            case "RequiredVan": card.RequiredVan = strVal; break;

            // Move All
            case "MoveAll": card.MoveAll = strVal; break;

            // Reveal
            case "RevealTopX": card.RevealTopX = intVal; break;
            case "Reveal": card.Reveal = boolVal; break;
            case "SceneEffect": card.SceneEffect = boolVal; break;
            case "CocoAdd": card.CocoAdd = boolVal; break;

            // Buddyfight
            case "BuddyWorld": card.BuddyWorld = intVal; break;
            case "GaugeCharge": card.GaugeCharge = intVal; break;
        }
    }

    private static string? UnquoteString(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("'") && trimmed.EndsWith("'") && trimmed.Length >= 2)
            return trimmed.Substring(1, trimmed.Length - 2);
        return null;
    }

    private static string? UnquoteDoubleString(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length >= 2)
            return trimmed.Substring(1, trimmed.Length - 2);
        return null;
    }

    private static int? TryParseInt(string value)
    {
        var trimmed = value.Trim();
        // Remove trailing single quote strings if accidentally included
        if (int.TryParse(trimmed, out int result))
            return result;
        // Handle negative numbers
        if (trimmed.StartsWith("-") && int.TryParse(trimmed, out int negResult))
            return negResult;
        return null;
    }

    /// <summary>
    /// Parses UnitPower.txt which uses literal numeric indices instead of CardStat variable.
    /// Format: global.PowerStat[123] = 5000 / global.DefensePowerStat[123] = 10000
    /// Applies values to cards that have 0 for these stats (i.e. not set in their own file).
    /// </summary>
    public static void ApplyUnitPowerFile(string textFolderPath, List<Card> allCards)
    {
        var filePath = Path.Combine(textFolderPath, "UnitPower.txt");
        if (!File.Exists(filePath)) return;

        // Build lookup by CardStat for fast access
        var cardLookup = new Dictionary<int, Card>();
        foreach (var card in allCards)
            cardLookup[card.CardStat] = card;

        var lines = File.ReadAllLines(filePath, Win1251);
        var regex = new Regex(@"^global\.(PowerStat|DefensePowerStat)\[(\d+)\]\s*=\s*(-?\d+)");

        foreach (var line in lines)
        {
            var match = regex.Match(line.Trim());
            if (!match.Success) continue;

            var prop = match.Groups[1].Value;
            var cardId = int.Parse(match.Groups[2].Value);
            var value = int.Parse(match.Groups[3].Value);

            if (!cardLookup.TryGetValue(cardId, out var card)) continue;

            switch (prop)
            {
                case "PowerStat":
                    if (card.PowerStat == 0)
                        card.PowerStat = value;
                    break;
                case "DefensePowerStat":
                    if (card.DefensePowerStat == 0)
                        card.DefensePowerStat = value;
                    break;
            }
        }
    }

    /// <summary>
    /// Parses the AllCard value from NoUse.txt
    /// </summary>
    public static int ParseAllCard(string textFolderPath)
    {
        var noUsePath = Path.Combine(textFolderPath, "NoUse.txt");
        if (!File.Exists(noUsePath)) return 0;

        var content = File.ReadAllText(noUsePath, Win1251);
        var match = Regex.Match(content, @"global\.AllCard\s*=\s*(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    /// <summary>
    /// Parses Custom Overrides.txt for custom faction definitions and AllCard override.
    /// Returns null if the file doesn't exist.
    /// </summary>
    public static CustomOverridesData? ParseCustomOverrides(string textFolderPath)
    {
        var filePath = Path.Combine(textFolderPath, "Custom Overrides.txt");
        if (!File.Exists(filePath)) return null;

        var lines = File.ReadAllLines(filePath, Win1251);
        var data = new CustomOverridesData();

        // Temporary dictionaries keyed by index
        var clanIds = new Dictionary<int, int>();
        var nationIds = new Dictionary<int, int>();
        var names = new Dictionary<int, string>();
        var fileNames = new Dictionary<int, string>();

        var reClanId = new Regex(@"^global\.CustomFactionClanId\[(\d+)\]\s*=\s*(-?\d+)");
        var reNationId = new Regex(@"^global\.CustomFactionNationId\[(\d+)\]\s*=\s*(-?\d+)");
        var reName = new Regex(@"^global\.CustomFactionName\[(\d+)\]\s*=\s*'(.+)'");
        var reFile = new Regex(@"^global\.CustomFactionFile\[(\d+)\]\s*=\s*'(.+)'");
        var reMaxFaction = new Regex(@"^global\.MaxCustomFaction\s*=\s*(\d+)");
        var reAllCard = new Regex(@"^global\.AllCard\s*=\s*(\d+)");
        var reCustomStartId = new Regex(@"^global\.CustomCardStartId\s*=\s*(\d+)");

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//"))
            {
                data.OtherLines.Add(line);
                continue;
            }

            Match m;
            bool matched = false;

            m = reClanId.Match(trimmed);
            if (m.Success) { clanIds[int.Parse(m.Groups[1].Value)] = int.Parse(m.Groups[2].Value); matched = true; }

            m = reNationId.Match(trimmed);
            if (m.Success) { nationIds[int.Parse(m.Groups[1].Value)] = int.Parse(m.Groups[2].Value); matched = true; }

            m = reName.Match(trimmed);
            if (m.Success) { names[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value; matched = true; }

            m = reFile.Match(trimmed);
            if (m.Success) { fileNames[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value; matched = true; }

            m = reMaxFaction.Match(trimmed);
            if (m.Success) { data.MaxCustomFaction = int.Parse(m.Groups[1].Value); matched = true; }

            m = reAllCard.Match(trimmed);
            if (m.Success) { data.AllCardOverride = int.Parse(m.Groups[1].Value); matched = true; }

            m = reCustomStartId.Match(trimmed);
            if (m.Success) { data.CustomCardStartId = int.Parse(m.Groups[1].Value); matched = true; }

            if (!matched)
                data.OtherLines.Add(line);
        }

        // Build faction list from collected data
        var allIndices = new HashSet<int>();
        foreach (var k in clanIds.Keys) allIndices.Add(k);
        foreach (var k in nationIds.Keys) allIndices.Add(k);
        foreach (var k in names.Keys) allIndices.Add(k);
        foreach (var k in fileNames.Keys) allIndices.Add(k);

        foreach (var idx in allIndices.OrderBy(i => i))
        {
            if (!names.ContainsKey(idx)) continue; // Must have a name at minimum

            data.Factions.Add(new CustomFactionData
            {
                Index = idx,
                ClanId = clanIds.GetValueOrDefault(idx, 0),
                NationId = nationIds.GetValueOrDefault(idx, -1),
                Name = names.GetValueOrDefault(idx, ""),
                FileName = fileNames.GetValueOrDefault(idx, "")
            });
        }

        return data;
    }

    public static Encoding GetEncoding() => Win1251;
}
