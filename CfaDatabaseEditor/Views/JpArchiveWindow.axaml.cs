using Avalonia.Controls;
using Avalonia.Interactivity;
using CfaDatabaseEditor.Services;
using CfaDatabaseEditor.ViewModels;

namespace CfaDatabaseEditor.Views;

public partial class JpArchiveWindow : Window
{
    public bool CardsWereAdded { get; private set; }

    public JpArchiveWindow()
    {
        InitializeComponent();
        DataContext = new JpArchiveViewModel();
    }

    public void SetDatabase(DatabaseService db, ImageService imageService)
    {
        if (DataContext is JpArchiveViewModel vm)
            vm.SetDatabase(db, imageService);
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JpArchiveViewModel vm)
        {
            CardsWereAdded = vm.Apply();
            if (CardsWereAdded)
                Close();
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
