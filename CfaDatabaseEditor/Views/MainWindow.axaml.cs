using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CfaDatabaseEditor.Services;
using CfaDatabaseEditor.ViewModels;

namespace CfaDatabaseEditor.Views;

public partial class MainWindow : Window
{
    private static readonly KeyModifiers PlatformCommandKey =
        OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    public MainWindow()
    {
        InitializeComponent();

        OpenMenuItem.HotKey = new KeyGesture(Key.O, PlatformCommandKey);
        SaveMenuItem.HotKey = new KeyGesture(Key.S, PlatformCommandKey);
        DuplicateMenuItem.HotKey = new KeyGesture(Key.D, PlatformCommandKey);

        KeyDown += OnWindowKeyDown;
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
            Text = "Version 1.1.0 beta",
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
