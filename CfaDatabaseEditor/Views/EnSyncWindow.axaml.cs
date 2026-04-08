using Avalonia.Controls;
using Avalonia.Interactivity;
using CfaDatabaseEditor.Models;
using CfaDatabaseEditor.Services;
using CfaDatabaseEditor.ViewModels;

namespace CfaDatabaseEditor.Views;

public partial class EnSyncWindow : Window
{
    public EnSyncWindow()
    {
        InitializeComponent();
        DataContext = new EnSyncViewModel();
    }

    public void SetDatabase(DatabaseService db, ImageService imageService)
    {
        if (DataContext is EnSyncViewModel vm)
            vm.SetDatabase(db, imageService);
    }

    private void OnApproveSelectedClick(object? sender, RoutedEventArgs e)
    {
        var selected = ResultsGrid.SelectedItems;
        if (selected == null || selected.Count == 0) return;

        int count = 0;
        foreach (var item in selected)
        {
            if (item is SyncResult result && !result.IsApproved)
            {
                result.IsApproved = true;
                count++;
            }
        }

        if (DataContext is EnSyncViewModel vm)
            vm.ProgressText = $"Approved {count} selected entries.";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
