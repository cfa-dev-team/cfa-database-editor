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

        OpenMenuItem.HotKey = new KeyGesture(Key.O, PlatformCommandKey);
        SaveMenuItem.HotKey = new KeyGesture(Key.S, PlatformCommandKey);
        DuplicateMenuItem.HotKey = new KeyGesture(Key.D, PlatformCommandKey);

        KeyDown += OnWindowKeyDown;
        CardStatEditor.ValueChanged += OnCardStatValueChanged;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers == PlatformCommandKey)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private async void OnOpenDatabaseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var path = await PickFolderAsync();
        if (path == null) return;
        await vm.LoadFromPathAsync(path);
        RebuildCustomFactionMenuItems(vm);
        UpdateConvertUnicodeMenuState();
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
            UpdateConvertUnicodeMenuState();
            vm.StatusText = "Custom factions updated. Save the database to persist.";
        }
    }

    private void UpdateConvertUnicodeMenuState()
    {
        if (DataContext is not MainWindowViewModel vm || !vm.Database.IsLoaded)
        {
            ConvertUnicodeMenuItem.IsEnabled = false;
            return;
        }

        var overrides = vm.Database.CustomOverrides;
        // Enable only when custom factions exist and UTF-8 is not already enabled
        ConvertUnicodeMenuItem.IsEnabled = overrides != null
            && overrides.Factions.Count > 0
            && overrides.CustomFactionUTF8 != true;
    }

    private async void OnConvertUnicodeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.Database.IsLoaded) return;

        var confirm = new Window
        {
            Title = "Convert to Unicode",
            Width = 440,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        bool confirmed = false;
        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10
        };
        panel.Children.Add(new TextBlock
        {
            Text = "This will convert all custom faction files from Windows-1251\nencoding to UTF-8. This cannot be undone.\n\nContinue?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 13
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        var okButton = new Button { Content = "Convert", Width = 100 };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };
        okButton.Click += (_, _) => { confirmed = true; confirm.Close(); };
        cancelButton.Click += (_, _) => confirm.Close();
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);
        confirm.Content = panel;

        await confirm.ShowDialog(this);

        if (!confirmed) return;

        try
        {
            int count = vm.Database.ConvertCustomFactionsToUnicode();
            vm.StatusText = $"Converted {count} custom faction file(s) to UTF-8.";
            UpdateConvertUnicodeMenuState();
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Conversion error: {ex.Message}";
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
            syncWindow.Show(this);
        }
    }

    private async void OnJpArchiveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.Database.IsLoaded)
        {
            var archiveWindow = new JpArchiveWindow();
            archiveWindow.SetDatabase(vm.Database, new ImageService(vm.Database));
            await archiveWindow.ShowDialog(this);

            if (archiveWindow.CardsWereAdded)
            {
                vm.RefreshCardList();
                vm.StatusText = "Cards added from JP archive. Save the database to persist.";
            }
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
            Text = "Version 1.1.1 beta",
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
