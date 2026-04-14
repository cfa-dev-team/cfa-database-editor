using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.Views;

public partial class GitBranchWindow : Window
{
    private readonly GitService _git;
    private List<BranchEntry> _allBranches = new();

    /// <summary>The branch that was successfully checked out, or null if cancelled.</summary>
    public string? CheckedOutBranch { get; private set; }

    public GitBranchWindow()
    {
        InitializeComponent();
        _git = null!;
    }

    public GitBranchWindow(GitService git)
    {
        InitializeComponent();
        _git = git;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await LoadBranchesAsync();
    }

    private async Task LoadBranchesAsync()
    {
        var localBranches = await _git.GetBranchesAsync();
        var remoteBranches = await _git.GetRemoteBranchesAsync();

        _allBranches.Clear();

        foreach (var b in localBranches)
        {
            var isCurrent = b == _git.CurrentBranch;
            _allBranches.Add(new BranchEntry(b, false, isCurrent));
        }

        // Add remote branches that don't have a local counterpart
        foreach (var rb in remoteBranches)
        {
            // remote branch format: "origin/branch-name"
            var shortName = rb.Contains('/') ? rb.Substring(rb.IndexOf('/') + 1) : rb;
            if (!localBranches.Contains(shortName))
                _allBranches.Add(new BranchEntry(rb, true, false));
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(filter)
            ? _allBranches
            : _allBranches.Where(b => b.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        BranchList.ItemsSource = filtered.Select(b =>
        {
            var item = new ListBoxItem();
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };

            if (b.IsCurrent)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "\u2713",
                    Foreground = new SolidColorBrush(Color.Parse("#2E7D32")),
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });
            }

            panel.Children.Add(new TextBlock
            {
                Text = b.Name,
                FontWeight = b.IsCurrent ? FontWeight.Bold : FontWeight.Normal
            });

            if (b.IsRemote)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "(remote)",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });
            }

            item.Content = panel;
            item.Tag = b;
            return item;
        }).ToList();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnBranchSelected(object? sender, SelectionChangedEventArgs e)
    {
        CheckoutButton.IsEnabled = BranchList.SelectedItem is ListBoxItem { Tag: BranchEntry entry } && !entry.IsCurrent;
    }

    private async void OnBranchDoubleClick(object? sender, TappedEventArgs e)
    {
        await TryCheckoutAsync();
    }

    private async void OnCheckoutClick(object? sender, RoutedEventArgs e)
    {
        await TryCheckoutAsync();
    }

    private async Task TryCheckoutAsync()
    {
        if (BranchList.SelectedItem is not ListBoxItem { Tag: BranchEntry entry }) return;
        if (entry.IsCurrent) return;

        CheckoutButton.IsEnabled = false;
        StatusLabel.Text = $"Checking out {entry.Name}...";

        // For remote branches, checkout the short name (creates a local tracking branch)
        var targetName = entry.IsRemote && entry.Name.Contains('/')
            ? entry.Name.Substring(entry.Name.IndexOf('/') + 1)
            : entry.Name;

        var result = await _git.CheckoutAsync(targetName);
        if (result.Success)
        {
            CheckedOutBranch = targetName;
            Close();
        }
        else
        {
            StatusLabel.Text = $"Checkout failed: {result.Error.Trim()}";
            CheckoutButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private record BranchEntry(string Name, bool IsRemote, bool IsCurrent);
}
