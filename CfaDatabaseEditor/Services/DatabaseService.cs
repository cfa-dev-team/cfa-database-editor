using CfaDatabaseEditor;
using CfaDatabaseEditor.Models;

namespace CfaDatabaseEditor.Services;

public class DatabaseService
{
    public string? RootPath { get; private set; }
    public string? TextPath => RootPath != null ? Path.Combine(RootPath, "Text") : null;
    public string? CardSpritePath => RootPath != null ? Path.Combine(RootPath, "CardSprite") : null;
    public string? CardSpriteMini2Path => RootPath != null ? Path.Combine(RootPath, "CardSpriteMini2") : null;

    public List<CardFile> CardFiles { get; } = new();
    public List<Card> AllCards { get; } = new();
    public int AllCardValue { get; set; }
    public int BuiltInAllCardValue { get; private set; }
    public CustomOverridesData? CustomOverrides { get; set; }
    public int? CustomCardStartId { get; set; }

    public bool IsLoaded => RootPath != null;

    public async Task LoadDatabaseAsync(string rootPath)
    {
        RootPath = rootPath;
        CardFiles.Clear();
        AllCards.Clear();
        ClanRegistry.ClearCustomFactions();

        var textPath = Path.Combine(rootPath, "Text");
        if (!Directory.Exists(textPath))
            throw new DirectoryNotFoundException($"Text folder not found at {textPath}");

        AllCardValue = GmlParser.ParseAllCard(textPath);
        BuiltInAllCardValue = AllCardValue;

        // Parse custom overrides for custom factions
        CustomOverrides = GmlParser.ParseCustomOverrides(textPath);
        if (CustomOverrides != null)
        {
            Program.Log?.WriteLine($"[INFO] Found Custom Overrides.txt with {CustomOverrides.Factions.Count} custom factions");
            RegisterCustomFactions(CustomOverrides);

            // Custom Overrides AllCard takes precedence
            if (CustomOverrides.AllCardOverride.HasValue && CustomOverrides.AllCardOverride.Value > AllCardValue)
                AllCardValue = CustomOverrides.AllCardOverride.Value;
        }

        var txtFiles = Directory.GetFiles(textPath, "*.txt", SearchOption.AllDirectories);
        var parsedFiles = new List<CardFile>();

        Program.Log?.WriteLine($"[INFO] Parsing {txtFiles.Length} text files...");
        await Task.Run(() =>
        {
            int count = 0;
            foreach (var file in txtFiles)
            {
                // Skip Custom Overrides.txt itself - it's not a card file
                if (Path.GetFileName(file).Equals("Custom Overrides.txt", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                    continue;
                }

                try
                {
                    var cardFile = GmlParser.ParseFile(file);
                    if (cardFile.Cards.Count > 0)
                        parsedFiles.Add(cardFile);
                    count++;
                    if (count % 10 == 0)
                        Program.Log?.WriteLine($"[INFO] Parsed {count}/{txtFiles.Length} files ({parsedFiles.Sum(f => f.Cards.Count)} cards)");
                }
                catch (Exception ex)
                {
                    Program.Log?.WriteLine($"[ERROR] Parsing {Path.GetFileName(file)}: {ex}");
                }
            }
        });
        Program.Log?.WriteLine($"[INFO] Parsing complete. {parsedFiles.Count} files, {parsedFiles.Sum(f => f.Cards.Count)} cards");

        // Back on UI thread - populate the lists
        foreach (var cf in parsedFiles)
        {
            CardFiles.Add(cf);
            AllCards.AddRange(cf.Cards);
        }

        AllCards.Sort((a, b) => a.CardStat.CompareTo(b.CardStat));

        // Tag custom cards (cards in custom faction files)
        TagCustomCards();

        // Derive CustomCardStartId
        if (CustomOverrides?.CustomCardStartId != null)
        {
            CustomCardStartId = CustomOverrides.CustomCardStartId;
        }
        else
        {
            // Derive from existing custom cards
            var customCards = AllCards.Where(c => c.IsCustomCard).ToList();
            if (customCards.Count > 0)
                CustomCardStartId = customCards.Min(c => c.CardStat);
        }

        // Apply power/shield stats from UnitPower.txt for old cards
        // that don't have these values in their own files
        if (textPath != null)
        {
            Program.Log?.WriteLine("[INFO] Applying UnitPower.txt stats...");
            GmlParser.ApplyUnitPowerFile(textPath, AllCards);
        }
    }

    private void TagCustomCards()
    {
        if (CustomOverrides == null || CustomOverrides.Factions.Count == 0) return;

        var customFileNames = CustomOverrides.Factions
            .Select(f => Path.GetFileName(f.FileName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var card in AllCards)
        {
            if (customFileNames.Contains(card.SourceFile))
                card.IsCustomCard = true;
        }
    }

    private void RegisterCustomFactions(CustomOverridesData overrides)
    {
        var definitions = new List<ClanDefinition>();

        foreach (var f in overrides.Factions)
        {
            bool isNation = f.ClanId == 0;
            definitions.Add(new ClanDefinition
            {
                Id = isNation ? f.NationId : f.ClanId,
                Name = f.Name,
                Type = isNation ? FactionType.Nation : FactionType.Clan,
                Era = FactionEra.Custom,
                ParentNationId = isNation ? null : (f.NationId >= 0 ? f.NationId : null),
                DisplayColor = isNation ? ClanRegistry.GetCustomNationColor() : ClanRegistry.GetCustomClanColor(),
                FileName = f.FileName,
                IsCustom = true,
                CustomIndex = f.Index
            });
        }

        ClanRegistry.RegisterCustomFactions(definitions);
    }

    public void SaveModifiedFiles()
    {
        foreach (var cardFile in CardFiles.Where(f => f.IsModified || f.Cards.Any(c => c.IsModified)))
        {
            GmlWriter.WriteCardFile(cardFile);
        }

        if (TextPath != null)
        {
            GmlWriter.UpdateAllCard(TextPath, AllCardValue);

            // Update Custom Overrides if it exists
            if (CustomOverrides != null)
            {
                CustomOverrides.AllCardOverride = AllCardValue;
                CustomOverrides.CustomCardStartId = CustomCardStartId;
                GmlWriter.WriteCustomOverrides(TextPath, CustomOverrides);
            }
        }
    }

    public Card CreateNewCard(string targetFileName)
    {
        BuiltInAllCardValue++;
        if (BuiltInAllCardValue > AllCardValue)
            AllCardValue = BuiltInAllCardValue;

        var card = new Card
        {
            CardStat = BuiltInAllCardValue,
            SourceFile = Path.GetFileName(targetFileName),
            SourceLineStart = -1,
            SourceLineEnd = -1,
            IsModified = true
        };

        var targetFile = CardFiles.FirstOrDefault(f =>
            Path.GetFileName(f.FilePath).Equals(Path.GetFileName(targetFileName), StringComparison.OrdinalIgnoreCase));

        // Create new CardFile if it doesn't exist yet (e.g., custom faction files)
        if (targetFile == null && TextPath != null)
        {
            var fullPath = Path.Combine(TextPath, targetFileName);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            targetFile = new CardFile
            {
                FilePath = fullPath,
                RawLines = new List<string>(),
                Cards = new List<Card>()
            };
            CardFiles.Add(targetFile);
        }

        if (targetFile != null)
        {
            targetFile.Cards.Add(card);
            targetFile.IsModified = true;
        }

        AllCards.Add(card);
        AllCards.Sort((a, b) => a.CardStat.CompareTo(b.CardStat));

        return card;
    }

    public Card CreateNewCustomCard(string targetFileName)
    {
        int startId = CustomCardStartId ?? 25000;
        int nextId = startId;
        var usedIds = new HashSet<int>(AllCards.Select(c => c.CardStat));

        while (nextId <= 31999 && usedIds.Contains(nextId))
            nextId++;

        if (nextId > 31999)
            throw new InvalidOperationException("No free custom card IDs available (max 31999)");

        if (nextId > AllCardValue)
            AllCardValue = nextId;

        var card = new Card
        {
            CardStat = nextId,
            SourceFile = Path.GetFileName(targetFileName),
            SourceLineStart = -1,
            SourceLineEnd = -1,
            IsModified = true,
            IsCustomCard = true
        };

        var targetFile = CardFiles.FirstOrDefault(f =>
            Path.GetFileName(f.FilePath).Equals(Path.GetFileName(targetFileName), StringComparison.OrdinalIgnoreCase));

        if (targetFile == null && TextPath != null)
        {
            var fullPath = Path.Combine(TextPath, targetFileName);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            targetFile = new CardFile
            {
                FilePath = fullPath,
                RawLines = new List<string>(),
                Cards = new List<Card>()
            };
            CardFiles.Add(targetFile);
        }

        if (targetFile != null)
        {
            targetFile.Cards.Add(card);
            targetFile.IsModified = true;
        }

        AllCards.Add(card);
        AllCards.Sort((a, b) => a.CardStat.CompareTo(b.CardStat));

        return card;
    }

    /// <summary>
    /// Attempts to change a custom card's CardStat ID.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    public string? TryChangeCustomCardId(Card card, int newId)
    {
        if (!card.IsCustomCard)
            return "Only custom cards can have their ID changed.";

        if (newId < 1 || newId > 31999)
            return "Card ID must be between 1 and 31999.";

        if (AllCards.Any(c => c != card && c.CardStat == newId))
            return $"Card ID {newId} is already in use.";

        card.CardStat = newId;
        card.IsModified = true;

        // Mark the source file as modified
        var file = CardFiles.FirstOrDefault(f =>
            Path.GetFileName(f.FilePath).Equals(card.SourceFile, StringComparison.OrdinalIgnoreCase));
        if (file != null)
            file.IsModified = true;

        if (newId > AllCardValue)
            AllCardValue = newId;

        AllCards.Sort((a, b) => a.CardStat.CompareTo(b.CardStat));
        return null;
    }

    public string? GetCardImagePath(int cardStat)
    {
        if (CardSpritePath == null) return null;
        var path = Path.Combine(CardSpritePath, $"n{cardStat}.jpg");
        return File.Exists(path) ? path : null;
    }

    public string? GetCardMiniImagePath(int cardStat)
    {
        if (CardSpriteMini2Path == null) return null;
        var path = Path.Combine(CardSpriteMini2Path, $"n{cardStat}.jpg");
        return File.Exists(path) ? path : null;
    }

    public int GetModifiedFileCount() =>
        CardFiles.Count(f => f.IsModified || f.Cards.Any(c => c.IsModified));
}
