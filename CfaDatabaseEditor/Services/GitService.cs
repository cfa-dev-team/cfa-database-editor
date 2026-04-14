using System.Diagnostics;

namespace CfaDatabaseEditor.Services;

/// <summary>
/// Cross-platform wrapper around the git CLI.
/// All methods are safe to call even when git is not installed or the path is not a repository.
/// </summary>
public class GitService
{
    private string? _repoPath;

    /// <summary>Whether git is installed and accessible on the system PATH.</summary>
    public bool IsGitInstalled { get; private set; }

    /// <summary>Whether the current database path is inside a git repository.</summary>
    public bool IsRepository { get; private set; }

    /// <summary>Current branch name, or null if not a repo.</summary>
    public string? CurrentBranch { get; private set; }

    /// <summary>True when there are uncommitted changes in the working tree.</summary>
    public bool HasChanges { get; private set; }

    /// <summary>Number of commits ahead of the upstream tracking branch.</summary>
    public int AheadCount { get; private set; }

    /// <summary>Number of commits behind the upstream tracking branch.</summary>
    public int BehindCount { get; private set; }

    /// <summary>
    /// Initialise the service for a given database folder.
    /// Call this every time a database is opened or after operations that change branch.
    /// </summary>
    public async Task InitAsync(string databasePath)
    {
        _repoPath = databasePath;
        IsGitInstalled = await CheckGitInstalledAsync();
        if (!IsGitInstalled)
        {
            IsRepository = false;
            CurrentBranch = null;
            HasChanges = false;
            AheadCount = 0;
            BehindCount = 0;
            return;
        }

        IsRepository = await CheckIsRepositoryAsync();
        if (!IsRepository)
        {
            CurrentBranch = null;
            HasChanges = false;
            AheadCount = 0;
            BehindCount = 0;
            return;
        }

        await RefreshStatusAsync();
    }

    /// <summary>Refresh branch name, ahead/behind counts, and dirty state.</summary>
    public async Task RefreshStatusAsync()
    {
        if (!IsGitInstalled || !IsRepository) return;

        CurrentBranch = (await RunAsync("rev-parse", "--abbrev-ref", "HEAD")).Output.Trim();
        HasChanges = !string.IsNullOrWhiteSpace((await RunAsync("status", "--porcelain")).Output);

        // ahead / behind
        AheadCount = 0;
        BehindCount = 0;
        var tracking = await RunAsync("rev-parse", "--abbrev-ref", "@{upstream}");
        if (tracking.Success)
        {
            var counts = await RunAsync("rev-list", "--left-right", "--count", "HEAD...@{upstream}");
            if (counts.Success)
            {
                var parts = counts.Output.Trim().Split('\t');
                if (parts.Length == 2)
                {
                    int.TryParse(parts[0], out var ahead);
                    int.TryParse(parts[1], out var behind);
                    AheadCount = ahead;
                    BehindCount = behind;
                }
            }
        }
    }

    /// <summary>Get a list of changed files (staged and unstaged) with their status codes.</summary>
    public async Task<List<GitFileStatus>> GetChangedFilesAsync()
    {
        var result = await RunAsync("status", "--porcelain");
        if (!result.Success) return new();

        var files = new List<GitFileStatus>();
        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3) continue;
            var indexStatus = line[0];
            var workTreeStatus = line[1];
            var path = line.Substring(3).Trim().Trim('"');

            // Handle renames: "R  old -> new"
            var displayPath = path;
            if (path.Contains(" -> "))
                displayPath = path.Split(" -> ").Last();

            files.Add(new GitFileStatus
            {
                IndexStatus = indexStatus,
                WorkTreeStatus = workTreeStatus,
                FilePath = displayPath,
                RawPath = path,
                IsStaged = indexStatus != ' ' && indexStatus != '?',
            });
        }
        return files;
    }

    /// <summary>Get all local branch names.</summary>
    public async Task<List<string>> GetBranchesAsync()
    {
        var result = await RunAsync("branch", "--format=%(refname:short)");
        if (!result.Success) return new();
        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .ToList();
    }

    /// <summary>Get all remote branch names (without remote prefix where possible).</summary>
    public async Task<List<string>> GetRemoteBranchesAsync()
    {
        var result = await RunAsync("branch", "-r", "--format=%(refname:short)");
        if (!result.Success) return new();
        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => !b.Contains("/HEAD"))
            .ToList();
    }

    /// <summary>Stage a specific file.</summary>
    public Task<GitResult> StageFileAsync(string filePath)
        => RunAsync("add", "--", filePath);

    /// <summary>Unstage a specific file.</summary>
    public Task<GitResult> UnstageFileAsync(string filePath)
        => RunAsync("reset", "HEAD", "--", filePath);

    /// <summary>Stage all changes.</summary>
    public Task<GitResult> StageAllAsync()
        => RunAsync("add", "-A");

    /// <summary>Discard changes for a specific file (restore to HEAD).</summary>
    public async Task<GitResult> DiscardFileAsync(GitFileStatus file)
    {
        // Unstage first if staged
        if (file.IsStaged)
            await RunAsync("reset", "HEAD", "--", file.FilePath);

        if (file.IndexStatus == '?' && file.WorkTreeStatus == '?')
        {
            // Untracked file — delete from disk
            try
            {
                var fullPath = Path.Combine(_repoPath!, file.FilePath);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                return new GitResult { Success = true };
            }
            catch (Exception ex)
            {
                return new GitResult { Success = false, Error = ex.Message };
            }
        }

        // Tracked file — restore to HEAD
        return await RunAsync("checkout", "HEAD", "--", file.FilePath);
    }

    /// <summary>Commit with the given message.</summary>
    public Task<GitResult> CommitAsync(string message)
        => RunAsync("commit", "-m", message);

    /// <summary>Fetch from all remotes.</summary>
    public Task<GitResult> FetchAsync()
        => RunAsync("fetch", "--all");

    /// <summary>Pull (with the default strategy).</summary>
    public Task<GitResult> PullAsync()
        => RunAsync("pull");

    /// <summary>Push the current branch.</summary>
    public Task<GitResult> PushAsync()
        => RunAsync("push");

    /// <summary>Checkout a branch by name.</summary>
    public Task<GitResult> CheckoutAsync(string branchName)
        => RunAsync("checkout", branchName);

    // ── internals ──

    private async Task<bool> CheckGitInstalledAsync()
    {
        try
        {
            var result = await RunGitProcessAsync(null, "version");
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckIsRepositoryAsync()
    {
        var result = await RunAsync("rev-parse", "--is-inside-work-tree");
        return result.Success && result.Output.Trim() == "true";
    }

    private Task<GitResult> RunAsync(params string[] args)
        => RunGitProcessAsync(_repoPath, args);

    private static async Task<GitResult> RunGitProcessAsync(string? workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (workingDir != null)
            psi.WorkingDirectory = workingDir;

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi);
        if (proc == null)
            return new GitResult { Success = false, Output = "", Error = "Failed to start git process" };

        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return new GitResult
        {
            Success = proc.ExitCode == 0,
            Output = stdout,
            Error = stderr
        };
    }
}

public class GitResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
}

public class GitFileStatus
{
    public char IndexStatus { get; init; }
    public char WorkTreeStatus { get; init; }
    public string FilePath { get; init; } = "";
    public string RawPath { get; init; } = "";
    public bool IsStaged { get; set; }

    public string StatusDisplay => (IndexStatus, WorkTreeStatus) switch
    {
        ('?', '?') => "Untracked",
        ('A', _) => "Added",
        ('M', _) or (_, 'M') => "Modified",
        ('D', _) or (_, 'D') => "Deleted",
        ('R', _) => "Renamed",
        ('C', _) => "Copied",
        _ => $"{IndexStatus}{WorkTreeStatus}"
    };
}
