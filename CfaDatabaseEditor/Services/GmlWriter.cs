using System.Text;
using CfaDatabaseEditor.Models;

namespace CfaDatabaseEditor.Services;

public static class GmlWriter
{
    /// <summary>
    /// Generates the GML text for a single card, following CFA conventions.
    /// Core properties (Name, Text, Grade, DCards, and some mechanics) go inside braces.
    /// PowerStat, DefensePowerStat, and other mechanical properties go outside.
    /// </summary>
    public static string GenerateCardGml(Card card)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CardStat = {card.CardStat}");
        sb.AppendLine("{");

        // Core properties inside braces
        sb.AppendLine($"global.CardName[CardStat] = '{EscapeGmlString(card.CardName)}'");
        sb.AppendLine($"global.CardText[CardStat] = '{EscapeGmlString(card.CardText)}'");
        sb.AppendLine($"global.UnitGrade[CardStat] = {card.UnitGrade}");
        if (card.DCards != 0)
            sb.AppendLine($"global.DCards[CardStat] = {card.DCards}");
        if (card.DCards2.HasValue)
            sb.AppendLine($"global.DCards2[CardStat] = {card.DCards2}");

        // Clans inside braces
        WriteOptionalInt(sb, "CardInClan", card.CardInClan);
        WriteOptionalInt(sb, "CardInClan2", card.CardInClan2);
        WriteOptionalInt(sb, "CardInClan3", card.CardInClan3);
        WriteOptionalInt(sb, "CardInClan4", card.CardInClan4);
        WriteOptionalInt(sb, "CardInClan5", card.CardInClan5);
        WriteOptionalInt(sb, "CardInClan6", card.CardInClan6);
        WriteOptionalInt(sb, "CardInClan7", card.CardInClan7);
        WriteOptionalInt(sb, "CardInClan8", card.CardInClan8);
        WriteOptionalInt(sb, "CardInClan9", card.CardInClan9);

        // Persona Ride (often inside braces)
        if (card.PersonaRide) sb.AppendLine("global.PersonaRide[CardStat] = 1");
        if (card.PersonaRideAct) sb.AppendLine("global.PersonaRideAct[CardStat] = 1");
        if (!string.IsNullOrEmpty(card.PersonaRideCardName))
            sb.AppendLine($"global.PersonaRideCardName[CardStat] = '{EscapeGmlString(card.PersonaRideCardName)}'");
        if (card.ForbidCrossPersonaRideUpon) sb.AppendLine("global.ForbidCrossPersonaRideUpon[CardStat] = 1");

        // Triggers (0 = None, skip)
        if (card.TriggerUnit > 0)
            sb.AppendLine($"global.TriggerUnit[CardStat] = {card.TriggerUnit}");

        // Extra Deck
        if (card.ExtraDeck) sb.AppendLine("global.ExtraDeck[CardStat] = 1");
        if (card.QuickShieldAdd) sb.AppendLine("global.QuickShieldAdd[CardStat] = 1");

        // Imaginary Gifts
        if (card.BlueTokenAdd) sb.AppendLine("global.BlueTokenAdd[CardStat] = 1");
        if (card.GreenTokenAdd) sb.AppendLine("global.GreenTokenAdd[CardStat] = 1");
        if (card.OrangeTokenAdd) sb.AppendLine("global.OrangeTokenAdd[CardStat] = 1");
        if (card.BlueTokenBoth) sb.AppendLine("global.BlueTokenBoth[CardStat] = 1");

        // Gift generators
        if (card.ForceAdd) sb.AppendLine("global.ForceAdd[CardStat] = 1");
        if (card.ProtectAdd) sb.AppendLine("global.ProtectAdd[CardStat] = 1");
        if (card.AccelAdd) sb.AppendLine("global.AccelAdd[CardStat] = 1");
        if (card.GiftAdd) sb.AppendLine("global.GiftAdd[CardStat] = 1");
        if (card.GiftAddSelect) sb.AppendLine("global.GiftAddSelect[CardStat] = 1");

        // Legality
        if (card.OldCardStat) sb.AppendLine("global.OldCardStat[CardStat] = 1");
        if (card.NewTriggerStat) sb.AppendLine("global.NewTriggerStat[CardStat] = 1");
        WriteOptionalInt(sb, "CardCopies", card.CardCopies);
        if (card.CardLimidet) sb.AppendLine("global.CardLimidet[CardStat] = 1");
        if (card.CardBanned) sb.AppendLine("global.CardBanned[CardStat] = 1");

        // Visibility
        if (card.DontShowInDeckEditor) sb.AppendLine("global.DontShowInDeckEditor[CardStat] = 1");

        // Double face / Reassign
        WriteOptionalInt(sb, "AnotherSide", card.AnotherSide);
        WriteOptionalInt(sb, "CardReassignedId", card.CardReassignedId);

        // Search effect (inside braces in some files)
        if (card.SearchEffect)
        {
            sb.AppendLine();
            sb.AppendLine("global.SearchEffect[CardStat] = 1");
            if (!string.IsNullOrEmpty(card.SearchEffectPosition))
                sb.AppendLine($"global.SearchEffectPosition[CardStat] = '{card.SearchEffectPosition}'");
            WriteOptionalInt(sb, "SearchEffectLookAtQuantity", card.SearchEffectLookAtQuantity);
            if (!string.IsNullOrEmpty(card.SearchEffectMode))
                sb.AppendLine($"global.SearchEffectMode[CardStat] = '{card.SearchEffectMode}'");
            if (!string.IsNullOrEmpty(card.SearchEffectArgument1))
            {
                if (int.TryParse(card.SearchEffectArgument1, out _))
                    sb.AppendLine($"global.SearchEffectArgument1[CardStat] = {card.SearchEffectArgument1}");
                else
                    sb.AppendLine($"global.SearchEffectArgument1[CardStat] = '{EscapeGmlString(card.SearchEffectArgument1)}'");
            }
            if (!string.IsNullOrEmpty(card.SearchEffectArgument2))
            {
                if (int.TryParse(card.SearchEffectArgument2, out _))
                    sb.AppendLine($"global.SearchEffectArgument2[CardStat] = {card.SearchEffectArgument2}");
                else
                    sb.AppendLine($"global.SearchEffectArgument2[CardStat] = '{EscapeGmlString(card.SearchEffectArgument2)}'");
            }
            if (!string.IsNullOrEmpty(card.SearchEffectArgument3))
            {
                if (int.TryParse(card.SearchEffectArgument3, out _))
                    sb.AppendLine($"global.SearchEffectArgument3[CardStat] = {card.SearchEffectArgument3}");
                else
                    sb.AppendLine($"global.SearchEffectArgument3[CardStat] = '{EscapeGmlString(card.SearchEffectArgument3)}'");
            }
            if (!string.IsNullOrEmpty(card.ActivateSearchFoundAction))
                sb.AppendLine($"global.ActivateSearchFoundAction[CardStat] = '{card.ActivateSearchFoundAction}'");
            if (!string.IsNullOrEmpty(card.ActivateSearchRestAction))
                sb.AppendLine($"global.ActivateSearchRestAction[CardStat] = '{card.ActivateSearchRestAction}'");
            WriteOptionalInt(sb, "SearchEffectFindQuantity", card.SearchEffectFindQuantity);
        }

        sb.AppendLine("}");

        // Properties outside braces
        sb.AppendLine($"global.PowerStat[CardStat] = {card.PowerStat}");
        sb.AppendLine($"global.DefensePowerStat[CardStat] = {card.DefensePowerStat}");

        // Power buffs
        WriteOptionalInt(sb, "AttackFromVBuff", card.AttackFromVBuff);
        WriteOptionalInt(sb, "AttackFromRBuff", card.AttackFromRBuff);
        WriteOptionalInt(sb, "AttackFromVRBuff", card.AttackFromVRBuff);

        // Back row
        if (card.CanAttackFromBackRow) sb.AppendLine("global.CanAttackFromBackRow[CardStat] = 1");
        if (card.EnableAttackFromBackRow) sb.AppendLine("global.EnableAttackFromBackRow[CardStat] = 1");

        // Misc mechanics
        if (card.RemoveFromDrop) sb.AppendLine("global.RemoveFromDrop[CardStat] = 1");
        WriteOptionalInt(sb, "UnitGradeIncrementInDeck", card.UnitGradeIncrementInDeck);
        if (card.TriggerPowerUpEffectVCRC.HasValue)
            sb.AppendLine($"global.TriggerPowerUpEffect[CardStat, 0] = {card.TriggerPowerUpEffectVCRC}");
        if (card.TriggerPowerUpEffectVC.HasValue)
            sb.AppendLine($"global.TriggerPowerUpEffect[CardStat, 1] = {card.TriggerPowerUpEffectVC}");

        // Arms
        if (card.Arms) sb.AppendLine("global.Arms[CardStat] = 1");
        if (card.LeftArms) sb.AppendLine("global.LeftArms[CardStat] = 1");
        if (card.RightArms) sb.AppendLine("global.RightArms[CardStat] = 1");
        if (card.ArmsAsUnit) sb.AppendLine("global.ArmAsUnit[CardStat] = 1");

        // Special
        if (card.RegalisPiece) sb.AppendLine("global.RegalisPiece[CardStat] = 1");
        if (card.Uranus) sb.AppendLine("global.Uranus[CardStat] = 1");
        if (card.MimicNotEvil) sb.AppendLine("global.MimicNotEvil[CardStat] = 1");
        if (card.UseCounters) sb.AppendLine("global.UseCounters[CardStat] = 1");
        if (card.DeleteAllExtraInEndPhase) sb.AppendLine("global.DeleteAllExtraInEndPhase[CardStat] = 1");
        if (card.ExtendedTextBox) sb.AppendLine("global.ExtendedTextBox = 1");
        if (card.TokenInHand) sb.AppendLine("global.TokenInHand[CardStat] = 1");

        // Token summoner (outside braces)
        if (card.TokenSummoner.HasValue)
        {
            sb.AppendLine($"global.TokenSummoner[CardStat] = {card.TokenSummoner}");
            if (card.TokenSummoner2.HasValue)
                sb.AppendLine($"global.TokenSummoner2[CardStat] = {card.TokenSummoner2}");
            if (card.TokenSummoner3.HasValue)
                sb.AppendLine($"global.TokenSummoner3[CardStat] = {card.TokenSummoner3}");
            if (!string.IsNullOrEmpty(card.TokenSummonerPosition))
                sb.AppendLine($"global.TokenSummonerPosition[CardStat] = '{card.TokenSummonerPosition}'");
            WriteOptionalInt(sb, "TokenSummonerQuantity", card.TokenSummonerQuantity);
            WriteOptionalInt(sb, "TokenSummonerRodeUponNumber", card.TokenSummonerRodeUponNumber);
            if (!string.IsNullOrEmpty(card.TokenSummoner2Text))
                sb.AppendLine($"global.TokenSummoner2Text[CardStat] = \"{card.TokenSummoner2Text}\"");
            if (!string.IsNullOrEmpty(card.TokenSummoner2Button1))
                sb.AppendLine($"global.TokenSummoner2Button1[CardStat] = \"{card.TokenSummoner2Button1}\"");
            if (!string.IsNullOrEmpty(card.TokenSummoner2Button2))
                sb.AppendLine($"global.TokenSummoner2Button2[CardStat] = \"{card.TokenSummoner2Button2}\"");
            if (!string.IsNullOrEmpty(card.TokenSummoner2Button3))
                sb.AppendLine($"global.TokenSummoner2Button3[CardStat] = \"{card.TokenSummoner2Button3}\"");
        }

        // Required Vanguard
        if (!string.IsNullOrEmpty(card.RequiredVan))
            sb.AppendLine($"global.RequiredVan[CardStat] = '{EscapeGmlString(card.RequiredVan)}'");

        // Move All
        if (!string.IsNullOrEmpty(card.MoveAll))
            sb.AppendLine($"global.MoveAll[CardStat] = '{card.MoveAll}'");

        // Reveal Top X
        WriteOptionalInt(sb, "RevealTopX", card.RevealTopX);

        // Card-specific
        if (card.Reveal) sb.AppendLine("global.Reveal[CardStat] = 1");
        if (card.SceneEffect) sb.AppendLine("global.SceneEffect[CardStat] = 1");
        if (card.CocoAdd) sb.AppendLine("global.CocoAdd[CardStat] = 1");

        // Buddyfight
        WriteOptionalInt(sb, "BuddyWorld", card.BuddyWorld);
        WriteOptionalInt(sb, "GaugeCharge", card.GaugeCharge);

        return sb.ToString();
    }

    private static void WriteOptionalInt(StringBuilder sb, string propName, int? value)
    {
        if (value.HasValue)
            sb.AppendLine($"global.{propName}[CardStat] = {value.Value}");
    }

    private static string EscapeGmlString(string value)
    {
        // Replace ASCII apostrophe/single quote (') with right single quotation mark (\u2019)
        // to avoid breaking GML strings which are delimited by single quotes.
        // The right single quotation mark is the standard apostrophe in CFA database.
        return value.Replace('\'', '\u2019');
    }

    /// <summary>
    /// Writes modified cards back to their source files.
    /// For existing cards, replaces the original line range.
    /// For new cards, appends to the file.
    /// </summary>
    public static void WriteCardFile(CardFile cardFile)
    {
        var encoding = GmlParser.GetEncoding();
        var lines = new List<string>(cardFile.RawLines);

        // Process cards in reverse order of SourceLineStart to avoid line number shifts
        var modifiedCards = cardFile.Cards
            .Where(c => c.IsModified)
            .OrderByDescending(c => c.SourceLineStart)
            .ToList();

        foreach (var card in modifiedCards)
        {
            if (card.SourceLineStart >= 0 && card.SourceLineEnd >= card.SourceLineStart)
            {
                // Replace existing card lines
                var newGml = GenerateCardGml(card);
                var newLines = newGml.Split('\n')
                    .Select(l => l.TrimEnd('\r'))
                    .ToList();
                // Remove trailing empty line from generation
                while (newLines.Count > 0 && string.IsNullOrWhiteSpace(newLines[^1]))
                    newLines.RemoveAt(newLines.Count - 1);

                lines.RemoveRange(card.SourceLineStart, card.SourceLineEnd - card.SourceLineStart + 1);
                lines.InsertRange(card.SourceLineStart, newLines);
            }
            card.IsModified = false;
        }

        // Append new cards (SourceLineStart == -1)
        var newCards = cardFile.Cards
            .Where(c => c.SourceLineStart < 0)
            .ToList();

        foreach (var card in newCards)
        {
            lines.Add("");
            lines.Add("");
            var newGml = GenerateCardGml(card);
            var newLines = newGml.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .ToList();
            while (newLines.Count > 0 && string.IsNullOrWhiteSpace(newLines[^1]))
                newLines.RemoveAt(newLines.Count - 1);
            lines.AddRange(newLines);

            card.SourceLineStart = lines.Count - newLines.Count;
            card.SourceLineEnd = lines.Count - 1;
            card.IsModified = false;
        }

        cardFile.RawLines = lines;
        File.WriteAllLines(cardFile.FilePath, lines, encoding);
        cardFile.IsModified = false;
    }

    /// <summary>
    /// Updates global.AllCard value in NoUse.txt
    /// </summary>
    public static void UpdateAllCard(string textFolderPath, int newValue)
    {
        var encoding = GmlParser.GetEncoding();
        var noUsePath = Path.Combine(textFolderPath, "NoUse.txt");
        if (!File.Exists(noUsePath)) return;

        var content = File.ReadAllText(noUsePath, encoding);
        content = System.Text.RegularExpressions.Regex.Replace(
            content,
            @"global\.AllCard\s*=\s*\d+",
            $"global.AllCard = {newValue}");
        File.WriteAllText(noUsePath, content, encoding);
    }

    /// <summary>
    /// Replaces the built-in CustomFaction* block (indices 0-99) and the
    /// MaxCustomFaction line in NoUse.txt with new content. Non-faction lines
    /// and any custom-faction entries (index >= 100) are preserved.
    /// </summary>
    public static void WriteBuiltInFactions(string textFolderPath, BuiltInFactionsData data)
    {
        var encoding = GmlParser.GetEncoding();
        var noUsePath = Path.Combine(textFolderPath, "NoUse.txt");
        if (!File.Exists(noUsePath)) return;

        var lines = File.ReadAllLines(noUsePath, encoding).ToList();

        var reBuiltInFaction = new System.Text.RegularExpressions.Regex(
            @"^\s*global\.CustomFaction(ClanId|NationId|Name|File)\[(\d+)\]");
        var reMaxFaction = new System.Text.RegularExpressions.Regex(
            @"^\s*global\.MaxCustomFaction\s*=");

        // Determine which existing CustomFaction lines belong to built-in indices (0-99).
        // Custom Overrides.txt-managed entries (index >= 100) must be preserved.
        var builtInLineIndices = new SortedSet<int>();
        for (int i = 0; i < lines.Count; i++)
        {
            var m = reBuiltInFaction.Match(lines[i]);
            if (!m.Success) continue;
            if (int.Parse(m.Groups[2].Value) < 100)
                builtInLineIndices.Add(i);
        }

        // Find the MaxCustomFaction line — replace it together with the block,
        // since NoUse.txt is the canonical place for it.
        int maxFactionLine = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (reMaxFaction.IsMatch(lines[i])) { maxFactionLine = i; break; }
        }

        // Remove existing built-in faction lines and the MaxCustomFaction line
        // (in reverse order to keep indices valid).
        var toRemove = new SortedSet<int>(builtInLineIndices);
        if (maxFactionLine >= 0) toRemove.Add(maxFactionLine);

        // Also remove blank lines that were inside the built-in block, so we
        // don't leave stray gaps. A blank line counts as "inside" if it has at
        // least one removed line both immediately before and immediately after
        // (allowing for further blanks in between).
        var removeSet = new HashSet<int>(toRemove);
        if (toRemove.Count > 0)
        {
            int blockStart = toRemove.Min();
            int blockEnd = toRemove.Max();
            for (int i = blockStart; i <= blockEnd; i++)
            {
                if (removeSet.Contains(i)) continue;
                if (string.IsNullOrWhiteSpace(lines[i]))
                    removeSet.Add(i);
            }
        }

        int insertAt = removeSet.Count > 0 ? removeSet.Min() : lines.Count;
        var sortedRemove = removeSet.OrderByDescending(i => i).ToList();
        foreach (var idx in sortedRemove) lines.RemoveAt(idx);

        // Build the new block.
        var block = new List<string>();
        foreach (var f in data.Factions.Where(f => f.Index < 100).OrderBy(f => f.Index))
        {
            block.Add($"global.CustomFactionClanId[{f.Index}] = {f.ClanId}");
            block.Add($"global.CustomFactionNationId[{f.Index}] = {f.NationId}");
            block.Add($"global.CustomFactionName[{f.Index}] = '{f.Name}'");
            block.Add($"global.CustomFactionFile[{f.Index}] = '{f.FileName}'");
            block.Add("");
        }

        if (data.Factions.Count > 0)
        {
            var maxIndex = data.Factions.Where(f => f.Index < 100).Max(f => f.Index);
            block.Add("//Increase left number for every new clan/nation");
            block.Add($"global.MaxCustomFaction = {maxIndex} + 1");
        }
        else
        {
            block.Add("global.MaxCustomFaction = 0");
        }

        if (insertAt > lines.Count) insertAt = lines.Count;
        lines.InsertRange(insertAt, block);

        File.WriteAllLines(noUsePath, lines, encoding);
    }

    /// <summary>
    /// Writes custom faction definitions to Custom Overrides.txt.
    /// Preserves non-faction lines from the original file.
    /// </summary>
    public static void WriteCustomOverrides(string textFolderPath, CustomOverridesData data)
    {
        var encoding = GmlParser.GetEncoding();
        var filePath = Path.Combine(textFolderPath, "Custom Overrides.txt");

        var sb = new StringBuilder();

        // Write preserved non-faction lines first
        foreach (var line in data.OtherLines)
            sb.AppendLine(line);

        // Add a blank line separator if there were other lines
        if (data.OtherLines.Count > 0 && data.Factions.Count > 0)
            sb.AppendLine();

        // Write faction definitions
        foreach (var f in data.Factions.OrderBy(f => f.Index))
        {
            sb.AppendLine($"global.CustomFactionClanId[{f.Index}] = {f.ClanId}");
            sb.AppendLine($"global.CustomFactionNationId[{f.Index}] = {f.NationId}");
            sb.AppendLine($"global.CustomFactionName[{f.Index}] = '{f.Name}'");
            sb.AppendLine($"global.CustomFactionFile[{f.Index}] = '{f.FileName}'");
            sb.AppendLine();
        }

        // Write MaxCustomFaction
        if (data.Factions.Count > 0)
        {
            var maxIndex = data.Factions.Max(f => f.Index);
            sb.AppendLine($"global.MaxCustomFaction = {maxIndex + 1}");
        }

        // Write CustomCardStartId (editor-managed, ignored by CFA engine)
        if (data.CustomCardStartId.HasValue)
            sb.AppendLine($"global.CustomCardStartId = {data.CustomCardStartId.Value}");

        // Write AllCard override if present
        if (data.AllCardOverride.HasValue)
            sb.AppendLine($"global.AllCard = {data.AllCardOverride.Value}");

        File.WriteAllText(filePath, sb.ToString(), encoding);
    }
}
