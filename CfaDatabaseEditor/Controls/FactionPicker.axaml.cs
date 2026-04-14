using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using CfaDatabaseEditor.Models;

namespace CfaDatabaseEditor.Controls;

public class FactionPickerOption
{
    public string DisplayName { get; init; } = "";
    public ClanDefinition? Faction { get; init; }
    public bool IsNone { get; init; }
    public bool IsOther { get; init; }

    public override string ToString() => DisplayName;
}

public partial class FactionPicker : UserControl
{
    public static readonly StyledProperty<int?> ValueProperty =
        AvaloniaProperty.Register<FactionPicker, int?>(nameof(Value),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsClanModeProperty =
        AvaloniaProperty.Register<FactionPicker, bool>(nameof(IsClanMode));

    public int? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsClanMode
    {
        get => GetValue(IsClanModeProperty);
        set => SetValue(IsClanModeProperty, value);
    }

    public ObservableCollection<FactionPickerOption> Options { get; } = new();

    private ComboBox? _comboBox;
    private NumericUpDown? _numericUpDown;
    private bool _isUpdating;

    public FactionPicker()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _comboBox = this.FindControl<ComboBox>("FactionComboBox");
        _numericUpDown = this.FindControl<NumericUpDown>("OtherNumeric");

        if (_comboBox != null)
        {
            _comboBox.ItemsSource = Options;
            _comboBox.SelectionChanged += OnSelectionChanged;
        }

        if (_numericUpDown != null)
            _numericUpDown.ValueChanged += OnOtherNumericValueChanged;

        BuildOptions();
        SyncSelectionFromValue();
    }

    private void BuildOptions()
    {
        Options.Clear();

        Options.Add(new FactionPickerOption { DisplayName = "None", IsNone = true });

        var factions = IsClanMode
            ? ClanRegistry.AllClans
            : ClanRegistry.AllNations;

        foreach (var f in factions)
            Options.Add(new FactionPickerOption { DisplayName = $"{f.Name} ({f.Id})", Faction = f });

        Options.Add(new FactionPickerOption { DisplayName = "Other...", IsOther = true });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
            SyncSelectionFromValue();
        else if (change.Property == IsClanModeProperty && _comboBox != null)
        {
            BuildOptions();
            SyncSelectionFromValue();
        }
    }

    private void SyncSelectionFromValue()
    {
        if (_comboBox == null || _isUpdating) return;
        _isUpdating = true;

        var val = Value;

        if (val is null or 0)
        {
            _comboBox.SelectedIndex = 0; // None
            if (_numericUpDown != null)
                _numericUpDown.IsVisible = false;
        }
        else
        {
            var match = FindOptionByValue(val.Value);
            if (match != null)
            {
                _comboBox.SelectedItem = match;
                if (_numericUpDown != null)
                    _numericUpDown.IsVisible = false;
            }
            else
            {
                // Select "Other..."
                _comboBox.SelectedItem = Options[^1];
                if (_numericUpDown != null)
                {
                    _numericUpDown.IsVisible = true;
                    _numericUpDown.Value = val;
                }
            }
        }

        _isUpdating = false;
    }

    private FactionPickerOption? FindOptionByValue(int id)
    {
        foreach (var o in Options)
        {
            if (o.Faction != null && o.Faction.Id == id)
                return o;
        }
        return null;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _comboBox == null) return;

        var option = _comboBox.SelectedItem as FactionPickerOption;
        if (option == null) return;

        _isUpdating = true;

        if (_numericUpDown != null)
            _numericUpDown.IsVisible = option.IsOther;

        if (option.IsNone)
            Value = IsClanMode ? null : 0;
        else if (option.IsOther)
        {
            // When switching to Other, default to 1 if no value yet
            if (_numericUpDown != null && (_numericUpDown.Value is null or 0))
                _numericUpDown.Value = 1;
            Value = (int?)(_numericUpDown?.Value ?? 1);
        }
        else
            Value = option.Faction!.Id;

        _isUpdating = false;
    }

    private void OnOtherNumericValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdating) return;

        var option = _comboBox?.SelectedItem as FactionPickerOption;
        if (option is { IsOther: true })
        {
            _isUpdating = true;
            Value = (int?)e.NewValue;
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Rebuilds the options list (call after custom factions change).
    /// </summary>
    public void RefreshOptions()
    {
        BuildOptions();
        SyncSelectionFromValue();
    }
}
