using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.Views;

public partial class GitCommitWindow : Window
{
    private readonly GitService _git;
    private List<GitFileStatus> _files = new();
    private readonly Dictionary<string, CheckBox> _checkBoxes = new();

    /// <summary>True if a commit was made, so the caller knows to refresh.</summary>
    public bool CommitWasMade { get; private set; }

    /// <summary>True if files were discarded, so the caller knows to reload the database.</summary>
    public bool FilesWereDiscarded { get; private set; }

    public GitCommitWindow()
    {
        InitializeComponent();
        _git = null!; // design-time only
    }

    public GitCommitWindow(GitService git)
    {
        InitializeComponent();
        _git = git;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        try
        {
            await RefreshFileListAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error loading git status: {ex.Message}";
        }
    }

    private async Task RefreshFileListAsync()
    {
        _files = await _git.GetChangedFilesAsync();
        _checkBoxes.Clear();
        FileListPanel.Children.Clear();

        if (_files.Count == 0)
        {
            FileListPanel.Children.Add(new TextBlock
            {
                Text = "No changes detected.",
                Foreground = this.FindResource("SubtleTextBrush") as IBrush ?? Brushes.Gray,
                Margin = new Thickness(8)
            });
            CommitButton.IsEnabled = false;
            DiscardButton.IsEnabled = false;
            DiscardAllButton.IsEnabled = false;
            StageAllButton.IsEnabled = false;
            UnstageAllButton.IsEnabled = false;
            return;
        }

        StageAllButton.IsEnabled = true;
        UnstageAllButton.IsEnabled = true;

        foreach (var file in _files)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Background = Brushes.Transparent, // make hit-testable for double-tap
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = file
            };
            panel.DoubleTapped += OnFileRowDoubleTapped;

            var cb = new CheckBox
            {
                IsChecked = file.IsStaged,
                VerticalAlignment = VerticalAlignment.Center
            };
            cb.IsCheckedChanged += OnFileCheckChanged;
            cb.Tag = file;
            _checkBoxes[file.FilePath] = cb;

            var statusBadge = new Border
            {
                Background = GetStatusBrush(file),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = file.StatusDisplay,
                    FontSize = 11,
                    Foreground = Brushes.White
                }
            };

            var pathText = new TextBlock
            {
                Text = file.FilePath,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            ToolTip.SetTip(pathText, "Double-click to view diff");

            panel.Children.Add(cb);
            panel.Children.Add(statusBadge);
            panel.Children.Add(pathText);
            FileListPanel.Children.Add(panel);
        }

        UpdateButtons();
    }

    private static IBrush GetStatusBrush(GitFileStatus file)
    {
        return file.StatusDisplay switch
        {
            "Untracked" => new SolidColorBrush(Color.Parse("#757575")),
            "Added" => new SolidColorBrush(Color.Parse("#2E7D32")),
            "Modified" => new SolidColorBrush(Color.Parse("#1565C0")),
            "Deleted" => new SolidColorBrush(Color.Parse("#C62828")),
            "Renamed" => new SolidColorBrush(Color.Parse("#6A1B9A")),
            _ => new SolidColorBrush(Color.Parse("#555555")),
        };
    }

    private async void OnFileCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not GitFileStatus file) return;

        var isChecked = cb.IsChecked == true;
        if (isChecked)
            await _git.StageFileAsync(file.RawPath);
        else
            await _git.UnstageFileAsync(file.FilePath);

        file.IsStaged = isChecked;
        UpdateButtons();
    }

    private async void OnStageAllClick(object? sender, RoutedEventArgs e)
    {
        await _git.StageAllAsync();
        await RefreshFileListAsync();
    }

    private async void OnUnstageAllClick(object? sender, RoutedEventArgs e)
    {
        foreach (var file in _files)
        {
            if (file.IsStaged)
                await _git.UnstageFileAsync(file.FilePath);
        }
        await RefreshFileListAsync();
    }

    private async void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        var selected = _checkBoxes
            .Where(kv => kv.Value.IsChecked == true)
            .Select(kv => kv.Value.Tag as GitFileStatus)
            .Where(f => f != null)
            .ToList();

        if (selected.Count == 0)
        {
            StatusLabel.Text = "No files selected to discard.";
            return;
        }

        DiscardButton.IsEnabled = false;
        StatusLabel.Text = $"Discarding {selected.Count} file(s)...";

        var errors = new List<string>();
        foreach (var file in selected)
        {
            var result = await _git.DiscardFileAsync(file!);
            if (!result.Success)
                errors.Add($"{file!.FilePath}: {result.Error.Trim()}");
        }

        FilesWereDiscarded = true;
        await RefreshFileListAsync();

        StatusLabel.Text = errors.Count > 0
            ? $"Discarded with errors: {string.Join("; ", errors)}"
            : $"Discarded {selected.Count} file(s).";
    }

    private async void OnDiscardAllClick(object? sender, RoutedEventArgs e)
    {
        if (_files.Count == 0)
        {
            StatusLabel.Text = "No files to discard.";
            return;
        }

        DiscardAllButton.IsEnabled = false;
        DiscardButton.IsEnabled = false;
        StatusLabel.Text = $"Discarding all {_files.Count} file(s)...";

        var errors = new List<string>();
        foreach (var file in _files)
        {
            var result = await _git.DiscardFileAsync(file);
            if (!result.Success)
                errors.Add($"{file.FilePath}: {result.Error.Trim()}");
        }

        FilesWereDiscarded = true;
        var count = _files.Count;
        await RefreshFileListAsync();

        StatusLabel.Text = errors.Count > 0
            ? $"Discarded with errors: {string.Join("; ", errors)}"
            : $"Discarded {count} file(s).";
    }

    private async void OnCommitClick(object? sender, RoutedEventArgs e)
    {
        var message = CommitMessageBox.Text?.Trim();
        if (string.IsNullOrEmpty(message))
        {
            StatusLabel.Text = "Commit message cannot be empty.";
            return;
        }

        // Ensure at least one file is staged
        var anyStaged = _checkBoxes.Values.Any(cb => cb.IsChecked == true);
        if (!anyStaged)
        {
            StatusLabel.Text = "No files staged for commit.";
            return;
        }

        CommitButton.IsEnabled = false;
        StatusLabel.Text = "Committing...";

        var result = await _git.CommitAsync(message);
        if (result.Success)
        {
            CommitWasMade = true;
            StatusLabel.Text = "Commit successful.";
            CommitMessageBox.Text = "";
            await RefreshFileListAsync();
        }
        else
        {
            StatusLabel.Text = $"Commit failed: {result.Error.Trim()}";
            CommitButton.IsEnabled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
    };

    private async void OnFileRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not StackPanel { Tag: GitFileStatus file }) return;
        if (_git.RepoPath == null) return;

        try
        {
            // For renames, FilePath is the new path; old path is in RawPath ("old -> new").
            string oldRelative = file.FilePath;
            string newRelative = file.FilePath;
            if (file.RawPath.Contains(" -> "))
            {
                var parts = file.RawPath.Split(" -> ");
                oldRelative = parts[0].Trim().Trim('"');
                newRelative = parts[1].Trim().Trim('"');
            }

            bool isUntracked = file.IndexStatus == '?' && file.WorkTreeStatus == '?';
            bool isDeleted = file.IndexStatus == 'D' || file.WorkTreeStatus == 'D';

            byte[]? leftBytes = null;
            byte[]? rightBytes = null;

            if (!isUntracked)
                leftBytes = await _git.GetHeadFileBytesAsync(oldRelative);

            if (!isDeleted)
            {
                var fullPath = Path.Combine(_git.RepoPath, newRelative);
                if (File.Exists(fullPath))
                    rightBytes = await File.ReadAllBytesAsync(fullPath);
            }

            var ext = Path.GetExtension(newRelative);
            GitDiffWindow diff;
            if (ImageExtensions.Contains(ext))
            {
                diff = GitDiffWindow.ForImage(file.FilePath, "HEAD", "Working tree", leftBytes, rightBytes);
            }
            else
            {
                var encoding = GmlParser.GetEncoding();
                var leftText = leftBytes != null ? encoding.GetString(leftBytes) : "";
                var rightText = rightBytes != null ? encoding.GetString(rightBytes) : "";
                diff = new GitDiffWindow(file.FilePath, "HEAD", "Working tree", leftText, rightText);
            }

            await diff.ShowDialog(this);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Diff failed: {ex.Message}";
        }
    }

    private void OnCommitMessageChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var hasMessage = !string.IsNullOrWhiteSpace(CommitMessageBox.Text);
        var hasChecked = _checkBoxes.Values.Any(cb => cb.IsChecked == true);
        var hasStaged = _files.Any(f => f.IsStaged) &&
                        _checkBoxes.Where(kv => kv.Value.IsChecked == true)
                            .Any(kv => kv.Value.Tag is GitFileStatus { IsStaged: true });
        CommitButton.IsEnabled = hasMessage && hasStaged;
        DiscardButton.IsEnabled = hasChecked;
        DiscardAllButton.IsEnabled = _files.Count > 0;
    }
}
