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

    public bool IsLoaded => RootPath != null;

    public async Task LoadDatabaseAsync(string rootPath)
    {
        RootPath = rootPath;
        CardFiles.Clear();
        AllCards.Clear();

        var textPath = Path.Combine(rootPath, "Text");
        if (!Directory.Exists(textPath))
            throw new DirectoryNotFoundException($"Text folder not found at {textPath}");

        AllCardValue = GmlParser.ParseAllCard(textPath);

        var txtFiles = Directory.GetFiles(textPath, "*.txt", SearchOption.AllDirectories);
        var parsedFiles = new List<CardFile>();

        Program.Log?.WriteLine($"[INFO] Parsing {txtFiles.Length} text files...");
        await Task.Run(() =>
        {
            int count = 0;
            foreach (var file in txtFiles)
            {
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

        // Apply power/shield stats from UnitPower.txt for old cards
        // that don't have these values in their own files
        if (textPath != null)
        {
            Program.Log?.WriteLine("[INFO] Applying UnitPower.txt stats...");
            GmlParser.ApplyUnitPowerFile(textPath, AllCards);
        }
    }

    public void SaveModifiedFiles()
    {
        foreach (var cardFile in CardFiles.Where(f => f.IsModified || f.Cards.Any(c => c.IsModified)))
        {
            GmlWriter.WriteCardFile(cardFile);
        }
    }

    public Card CreateNewCard(string targetFileName)
    {
        AllCardValue++;
        var card = new Card
        {
            CardStat = AllCardValue,
            SourceFile = targetFileName,
            SourceLineStart = -1,
            SourceLineEnd = -1,
            IsModified = true
        };

        var targetFile = CardFiles.FirstOrDefault(f =>
            Path.GetFileName(f.FilePath).Equals(targetFileName, StringComparison.OrdinalIgnoreCase));

        if (targetFile != null)
        {
            targetFile.Cards.Add(card);
            targetFile.IsModified = true;
        }

        AllCards.Add(card);
        AllCards.Sort((a, b) => a.CardStat.CompareTo(b.CardStat));

        if (TextPath != null)
            GmlWriter.UpdateAllCard(TextPath, AllCardValue);

        return card;
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
