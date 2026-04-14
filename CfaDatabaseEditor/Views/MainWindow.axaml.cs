using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CfaDatabaseEditor.Models;
using CfaDatabaseEditor.Services;
using CfaDatabaseEditor.ViewModels;

namespace CfaDatabaseEditor.Views;

public partial class MainWindow : Window
{
    private static readonly KeyModifiers PlatformCommandKey =
        OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private bool _suppressCardStatChange;

    public MainWindow()
    {
        InitializeComponent();

        // Register shortcuts in the tunnel phase so they work regardless of
        // which control currently has focus (TextBox, NumericUpDown, etc.).
        AddHandler(KeyDownEvent, OnWindowKeyDownTunnel, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        CardStatEditor.ValueChanged += OnCardStatValueChanged;
    }

    private void OnWindowKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != PlatformCommandKey) return;

        switch (e.Key)
        {
            case Key.O:
                OnOpenDatabaseClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.S:
                if (DataContext is MainWindowViewModel vmS)
                    vmS.SaveCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D:
                if (DataContext is MainWindowViewModel vmD)
                    vmD.DuplicateCardCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.R:
                OnReloadDatabaseClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F:
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                break;
        }
    }

    private async void OnOpenDatabaseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var path = await PickFolderAsync();
        if (path == null) return;
        await vm.LoadFromPathAsync(path);
        RebuildCustomFactionMenuItems(vm);
    }

    private async void OnReloadDatabaseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        await vm.ReloadDatabaseAsync();
        RebuildCustomFactionMenuItems(vm);
    }

    private void OnRecentMenuOpened(object? sender, RoutedEventArgs e)
    {
        RecentMenu.Items.Clear();
        var recent = Services.ConfigService.GetRecentFolders();
        if (recent.Count == 0)
        {
            RecentMenu.Items.Add(new MenuItem { Header = "(empty)", IsEnabled = false });
            return;
        }

        foreach (var folder in recent)
        {
            var item = new MenuItem { Header = folder };
            item.Click += async (_, _) =>
            {
                if (DataContext is not MainWindowViewModel vm) return;
                await vm.LoadFromPathAsync(folder);
                RebuildCustomFactionMenuItems(vm);
            };
            RecentMenu.Items.Add(item);
        }
    }

    private async void OnReplaceImageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.SelectedCard == null) return;

        var path = await PickImageFileAsync();
        if (path == null) return;
        vm.ReplaceImageFromFile(path);
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select CFA Database Folder",
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private async Task<string?> PickImageFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Card Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp" } }
            }
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void RebuildCustomFactionMenuItems(MainWindowViewModel vm)
    {
        // Remove old custom faction menu items (tagged with "custom")
        var toRemove = NewCardMenu.Items.OfType<Control>()
            .Where(c => c.Tag is string s && s == "custom")
            .ToList();
        foreach (var item in toRemove)
            NewCardMenu.Items.Remove(item);

        // Also remove the custom separator if present
        var customSep = NewCardMenu.Items.OfType<Separator>()
            .FirstOrDefault(s => s.Tag is string t && t == "custom-sep");
        if (customSep != null)
            NewCardMenu.Items.Remove(customSep);

        var customFactions = ClanRegistry.CustomFactions;
        if (customFactions.Count == 0) return;

        var sep = new Separator { Tag = "custom-sep" };
        NewCardMenu.Items.Add(sep);

        foreach (var faction in customFactions)
        {
            var suffix = faction.Type == FactionType.Nation ? " [Nation]" : " [Clan]";
            var item = new MenuItem
            {
                Header = faction.Name + suffix,
                Command = vm.AddNewCardCommand,
                CommandParameter = faction,
                Tag = "custom"
            };
            NewCardMenu.Items.Add(item);
        }
    }

    private async void OnCustomFactionsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.Database.IsLoaded) return;

        var dialog = new CustomFactionsWindow();
        dialog.SetDatabase(vm.Database);
        await dialog.ShowDialog(this);

        if (dialog.WasModified)
        {
            vm.RefreshAfterCustomFactionChange();
            RebuildCustomFactionMenuItems(vm);
            vm.StatusText = "Custom factions updated. Save the database to persist.";
        }
    }

    private void OnCardStatValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_suppressCardStatChange) return;
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.SelectedCard == null || !vm.SelectedCard.IsCustomCard) return;

        var newId = (int)(e.NewValue ?? 0);
        var oldId = (int)(e.OldValue ?? 0);
        if (newId == oldId) return;

        // When a different card is selected, the binding pushes Card.CardStat → NumericUpDown.Value.
        // At that point Card.CardStat already equals newId. Skip — this is not a user edit.
        if (vm.SelectedCard.CardStat == newId) return;

        var error = vm.Database.TryChangeCustomCardId(vm.SelectedCard, newId);
        if (error != null)
        {
            // Revert to old value
            _suppressCardStatChange = true;
            CardStatEditor.Value = oldId;
            _suppressCardStatChange = false;
            vm.StatusText = error;
            return;
        }

        vm.MarkCardModified();
        vm.RefreshCardList();
        vm.SelectedCard = vm.Database.AllCards.FirstOrDefault(c => c.CardStat == newId);
        vm.StatusText = $"Card ID changed from {oldId} to {newId}";
    }

    /// <summary>
    /// Shows a dialog to prompt the user for the first custom card ID.
    /// Returns the chosen value, or null if cancelled.
    /// </summary>
    public static async Task<int?> PromptCustomCardStartIdAsync(Window owner)
    {
        int? result = null;

        var dialog = new Window
        {
            Title = "Custom Card Start ID",
            Width = 380,
            Height = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Enter the starting card ID for custom cards.\nCustom cards will be numbered from this value.\nUse 20000+ to avoid conflicts with built-in cards.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 13
        });

        var numUpDown = new NumericUpDown
        {
            Value = 25000,
            Minimum = 100,
            Maximum = 31999,
            FormatString = "0",
            Width = 150,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        panel.Children.Add(numUpDown);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };

        var okButton = new Button { Content = "OK", Width = 80 };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };

        okButton.Click += (_, _) =>
        {
            result = (int)(numUpDown.Value ?? 25000);
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(owner);
        return result;
    }

    // ── Git handlers ──

    private async void OnGitFetchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.StatusText = "Fetching...";
        var result = await vm.Git.FetchAsync();
        if (result.Success)
        {
            await vm.RefreshGitStatusAsync();
            vm.StatusText = "Fetch completed.";
        }
        else
        {
            vm.StatusText = $"Fetch failed: {result.Error.Trim()}";
        }
    }

    private async void OnGitPullClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.StatusText = "Pulling...";
        var result = await vm.Git.PullAsync();
        if (result.Success)
        {
            vm.StatusText = "Pull completed. Reloading database...";
            await vm.ReloadDatabaseAsync();
            await vm.RefreshGitStatusAsync();
        }
        else
        {
            vm.StatusText = $"Pull failed: {result.Error.Trim()}";
        }
    }

    private async void OnGitPushClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.StatusText = "Pushing...";
        var result = await vm.Git.PushAsync();
        if (result.Success)
        {
            await vm.RefreshGitStatusAsync();
            vm.StatusText = "Push completed.";
        }
        else
        {
            vm.StatusText = $"Push failed: {result.Error.Trim()}";
        }
    }

    private async void OnGitCommitClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var commitWindow = new GitCommitWindow(vm.Git);
        await commitWindow.ShowDialog(this);
        if (commitWindow.CommitWasMade)
        {
            await vm.RefreshGitStatusAsync();
            vm.StatusText = "Commit created.";
        }
    }

    private async void OnGitCheckoutClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var branchWindow = new GitBranchWindow(vm.Git);
        await branchWindow.ShowDialog(this);
        if (branchWindow.CheckedOutBranch != null)
        {
            vm.StatusText = $"Switched to {branchWindow.CheckedOutBranch}. Reloading database...";
            await vm.ReloadDatabaseAsync();
            await vm.RefreshGitStatusAsync();
        }
    }

    private async void OnGitRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        await vm.RefreshGitStatusAsync();
        vm.StatusText = "Git status refreshed.";
    }

    private async void OnGitBranchClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Clicking the branch name in the status bar opens the checkout window
        await OnGitCheckoutClickAsync();
    }

    private async Task OnGitCheckoutClickAsync()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var branchWindow = new GitBranchWindow(vm.Git);
        await branchWindow.ShowDialog(this);
        if (branchWindow.CheckedOutBranch != null)
        {
            vm.StatusText = $"Switched to {branchWindow.CheckedOutBranch}. Reloading database...";
            await vm.ReloadDatabaseAsync();
            await vm.RefreshGitStatusAsync();
        }
    }

    private void OnAutoPreformatClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.SelectedCard == null || string.IsNullOrEmpty(vm.SelectedCard.CardText)) return;

        var text = vm.SelectedCard.CardText;

        // Separate Brackets
        text = Regex.Replace(text, @"([\]\)]{1})([\[\(]{1})", "$1 $2");

        // Replace bullet points with Win-1251 middle dot (•) and add space after them
        text = text.Replace("・", "•");
        text = Regex.Replace(text, @"(•)(\S)", "$1 $2");

        // [AUTO] -> AUTO, [ACT] -> ACT, [CONT] -> CONT
        text = text.Replace("[AUTO]", "AUTO");
        text = text.Replace("[ACT]", "ACT");
        text = text.Replace("[CONT]", "CONT");

        // [COST][ -> COST [
        text = text.Replace("[COST]", "COST");

        // [1/Turn] -> 1/Turn, [1/Fight] -> 1/Fight
        text = text.Replace("[1/Turn]", "1/Turn");
        text = text.Replace("[1/Fight]", "1/Fight");

        // [Power] -> power, [Shield] -> shield, [Critical] -> critical
        text = text.Replace("[Power]", "power");
        text = text.Replace("[Shield]", "shield");
        text = text.Replace("[Critical]", "critical");

        // [Stand] -> stand, [Rest] -> rest
        text = text.Replace("[Stand]", "stand");
        text = text.Replace("[Rest]", "rest");

        // :RC: -> RC, :VC: -> VC, :GC: -> GC
        text = text.Replace(":RC:", "RC");
        text = text.Replace(":VC:", "VC");
        text = text.Replace(":GC:", "GC");

        // (RC) -> RC, (VC) -> VC, (GC) -> GC
        text = text.Replace("(RC)", "RC");
        text = text.Replace("(VC)", "VC");
        text = text.Replace("(GC)", "GC");

        // Soul-Blast -> Soul Blast, Counter-Charge -> Counter Charge, etc.
        text = Regex.Replace(text, @"(Soul|Counter|Energy)-(Blast|Charge)", "$1 $2");

        // :\w -> : \w  (colon immediately followed by a word character, add space)
        text = Regex.Replace(text, @":(\S)", ": $1");

        // (\d) -> \d  (single digit in parentheses, remove the parens)
        text = Regex.Replace(text, @"\((\d)\)", "$1");

        vm.SelectedCard.CardText = text;
    }

    private void OnScrollToTopClick(object? sender, RoutedEventArgs e)
    {
        CardListBox.ScrollIntoView(CardListBox.Items.Cast<object>().FirstOrDefault()!);
    }

    private void OnScrollToBottomClick(object? sender, RoutedEventArgs e)
    {
        CardListBox.ScrollIntoView(CardListBox.Items.Cast<object>().LastOrDefault()!);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnEnSyncClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.Database.IsLoaded)
        {
            var syncWindow = new EnSyncWindow();
            syncWindow.SetDatabase(vm.Database, new ImageService(vm.Database));
            syncWindow.Show();
        }
    }

    private void OnJpArchiveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.Database.IsLoaded)
        {
            var archiveWindow = new JpArchiveWindow();
            archiveWindow.SetDatabase(vm.Database, new ImageService(vm.Database));
            archiveWindow.Closed += (_, _) =>
            {
                if (archiveWindow.CardsWereAdded)
                {
                    vm.RefreshCardList();
                    vm.StatusText = "Cards added from JP archive. Save the database to persist.";
                }
            };
            archiveWindow.Show();
        }
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var about = new Window
        {
            Title = "About CFA Database Editor",
            Width = 380,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 8,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = "CFA Database Editor",
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.Bold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Version 1.2.0 beta",
            FontSize = 13,
            Foreground = Avalonia.Media.Brushes.Gray
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Developed by Sieg, 2026",
            FontSize = 13,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Database editor and EN sync tool for Cardfight!! Area.",
            FontSize = 12,
            Foreground = Avalonia.Media.Brushes.Gray,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        });

        about.Content = panel;
        await about.ShowDialog(this);
    }
}
