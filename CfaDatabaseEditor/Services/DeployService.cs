namespace CfaDatabaseEditor.Services;

public record DeployStats(int TextFiles, int NewSprites, int UpdatedSprites, int SkippedSprites);

public static class DeployService
{
    private static readonly string[] SpriteFolders = { "CardSprite", "CardSpriteMini2" };

    public static async Task<DeployStats> DeployAsync(
        string sourceRoot,
        string cfaPath,
        bool updateOldSprites,
        IProgress<string>? progress = null)
    {
        var textFiles = await CopyTextAsync(sourceRoot, cfaPath, progress);

        int newSprites = 0;
        int updatedSprites = 0;
        int skippedSprites = 0;

        foreach (var folder in SpriteFolders)
        {
            progress?.Report($"Copying {folder}...");
            var (n, u, s) = await Task.Run(() => DeploySpriteFolder(
                Path.Combine(sourceRoot, folder),
                Path.Combine(cfaPath, folder),
                updateOldSprites));
            newSprites += n;
            updatedSprites += u;
            skippedSprites += s;
        }

        return new DeployStats(textFiles, newSprites, updatedSprites, skippedSprites);
    }

    private static async Task<int> CopyTextAsync(string sourceRoot, string cfaPath, IProgress<string>? progress)
    {
        var srcText = Path.Combine(sourceRoot, "Text");
        var dstText = Path.Combine(cfaPath, "Text");
        if (!Directory.Exists(srcText)) return 0;

        progress?.Report("Copying Text...");
        return await Task.Run(() => CopyDirectoryRecursive(srcText, dstText));
    }

    private static int CopyDirectoryRecursive(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        int count = 0;
        foreach (var file in Directory.EnumerateFiles(src))
        {
            var dstFile = Path.Combine(dst, Path.GetFileName(file));
            File.Copy(file, dstFile, overwrite: true);
            count++;
        }
        foreach (var sub in Directory.EnumerateDirectories(src))
        {
            count += CopyDirectoryRecursive(sub, Path.Combine(dst, Path.GetFileName(sub)));
        }
        return count;
    }

    private static (int newCount, int updated, int skipped) DeploySpriteFolder(
        string srcDir, string dstDir, bool updateOldSprites)
    {
        if (!Directory.Exists(srcDir)) return (0, 0, 0);
        Directory.CreateDirectory(dstDir);

        int newCount = 0, updated = 0, skipped = 0;

        foreach (var srcFile in Directory.EnumerateFiles(srcDir))
        {
            var dstFile = Path.Combine(dstDir, Path.GetFileName(srcFile));

            if (!File.Exists(dstFile))
            {
                File.Copy(srcFile, dstFile, overwrite: true);
                newCount++;
                continue;
            }

            if (!updateOldSprites)
            {
                skipped++;
                continue;
            }

            if (File.GetLastWriteTimeUtc(srcFile) > File.GetLastWriteTimeUtc(dstFile))
            {
                File.Copy(srcFile, dstFile, overwrite: true);
                updated++;
            }
            else
            {
                skipped++;
            }
        }

        return (newCount, updated, skipped);
    }
}
