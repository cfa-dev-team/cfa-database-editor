using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CfaDatabaseEditor.Models;

public class JpArchiveCard : INotifyPropertyChanged
{
    private string _imageUrl = string.Empty;
    private byte[]? _imageData;
    private string _cardName = string.Empty;
    private ClanDefinition? _selectedNation;
    private ClanDefinition? _selectedClan;
    private bool _isSelected;

    public string ImageUrl
    {
        get => _imageUrl;
        set => SetField(ref _imageUrl, value);
    }

    public byte[]? ImageData
    {
        get => _imageData;
        set => SetField(ref _imageData, value);
    }

    public string CardName
    {
        get => _cardName;
        set => SetField(ref _cardName, value);
    }

    public ClanDefinition? SelectedNation
    {
        get => _selectedNation;
        set => SetField(ref _selectedNation, value);
    }

    public ClanDefinition? SelectedClan
    {
        get => _selectedClan;
        set => SetField(ref _selectedClan, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
