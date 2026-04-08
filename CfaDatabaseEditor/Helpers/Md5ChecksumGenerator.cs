using System.Security.Cryptography;
using System.Text;

namespace CfaDatabaseEditor.Helpers;

public static class Md5ChecksumGenerator
{
    /// <summary>
    /// Regenerates .md5sums files for CardSprite and CardSpriteMini2 folders.
    /// Output format matches md5deep: "{hash}  ./{filename}"
    /// </summary>
    public static async Task RegenerateChecksumsAsync(string rootPath)
    {
        var tasks = new[]
        {
            GenerateForFolder(Path.Combine(rootPath, "CardSprite"),
                Path.Combine(rootPath, "CardSprite.md5sums")),
            GenerateForFolder(Path.Combine(rootPath, "CardSpriteMini2"),
                Path.Combine(rootPath, "CardSpriteMini2.md5sums"))
        };
        await Task.WhenAll(tasks);
    }

    private static async Task GenerateForFolder(string folderPath, string outputPath)
    {
        if (!Directory.Exists(folderPath)) return;

        var files = Directory.GetFiles(folderPath)
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sb = new StringBuilder();

        foreach (var file in files)
        {
            var hash = await ComputeMd5Async(file);
            var fileName = Path.GetFileName(file);
            sb.AppendLine($"{hash}  .\\{fileName}");
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
