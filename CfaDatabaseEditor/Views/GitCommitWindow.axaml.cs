using Avalonia;
using Avalonia.Controls;
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
        await RefreshFileListAsync();
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
                Foreground = (IBrush)(this.FindResource("SubtleTextBrush") ?? Brushes.Gray),
                Margin = new Thickness(8)
            });
            CommitButton.IsEnabled = false;
            return;
        }

        foreach (var file in _files)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

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
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            panel.Children.Add(cb);
            panel.Children.Add(statusBadge);
            panel.Children.Add(pathText);
            FileListPanel.Children.Add(panel);
        }

        UpdateCommitButton();
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
        UpdateCommitButton();
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

    private void OnCommitMessageChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateCommitButton();
    }

    private void UpdateCommitButton()
    {
        var hasMessage = !string.IsNullOrWhiteSpace(CommitMessageBox.Text);
        var hasStaged = _checkBoxes.Values.Any(cb => cb.IsChecked == true);
        CommitButton.IsEnabled = hasMessage && hasStaged;
    }
}
