using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CfaDatabaseEditor.Helpers;

public static class Md5ChecksumGenerator
{
    private static readonly Regex CardFileNameRegex = new(@"^n(\d+)\.", RegexOptions.Compiled);

    /// <summary>
    /// Regenerates .md5sums files for CardSprite and CardSpriteMini2 folders.
    /// Output format matches md5deep: "{hash}  ./{filename}"
    /// Custom card images (IDs above builtInMaxCard) are excluded since they
    /// are never auto-updated and don't need checksums.
    /// </summary>
    public static async Task RegenerateChecksumsAsync(string rootPath, int builtInMaxCard = int.MaxValue)
    {
        var tasks = new[]
        {
            GenerateForFolder(Path.Combine(rootPath, "CardSprite"),
                Path.Combine(rootPath, "CardSprite.md5sums"), builtInMaxCard),
            GenerateForFolder(Path.Combine(rootPath, "CardSpriteMini2"),
                Path.Combine(rootPath, "CardSpriteMini2.md5sums"), builtInMaxCard)
        };
        await Task.WhenAll(tasks);
    }

    private static async Task GenerateForFolder(string folderPath, string outputPath, int builtInMaxCard)
    {
        if (!Directory.Exists(folderPath)) return;

        var folderName = Path.GetFileName(folderPath);
        var files = Directory.GetFiles(folderPath)
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sb = new StringBuilder();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);

            // Skip custom card images (IDs above the built-in AllCard value)
            var match = CardFileNameRegex.Match(fileName);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int cardId) && cardId > builtInMaxCard)
                continue;

            var hash = await ComputeMd5Async(file);
            sb.AppendLine($"{hash}  .\\{folderName}\\{fileName}");
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString());
    }

    private static async Task<string> ComputeMd5Async(string filePath)
    {
        using var md5 = MD5.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await md5.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
