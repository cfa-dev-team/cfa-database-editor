using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CfaDatabaseEditor.Models;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.Views;

public partial class CustomFactionsWindow : Window
{
    private DatabaseService? _db;
    public bool WasModified { get; private set; }

    private ObservableCollection<FactionListItem> _items = new();
    private int _editingIndex = -1; // -1 = adding new, >= 0 = editing existing

    public CustomFactionsWindow()
    {
        InitializeComponent();
        FactionList.ItemsSource = _items;
    }

    public void SetDatabase(DatabaseService db)
    {
        _db = db;
        LoadFactions();
    }

    private void LoadFactions()
    {
        _items.Clear();
        if (_db?.CustomOverrides == null) return;

        foreach (var f in _db.CustomOverrides.Factions.OrderBy(f => f.Index))
        {
            _items.Add(new FactionListItem(f));
        }
    }

    private void OnFactionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RemoveButton.IsEnabled = FactionList.SelectedItem != null;
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (_db == null) return;

        // Prompt for CustomCardStartId when adding the very first custom faction
        bool isFirstFaction = (_db.CustomOverrides == null || _db.CustomOverrides.Factions.Count == 0)
                              && _db.CustomCardStartId == null;
        if (isFirstFaction)
        {
            var startId = await MainWindow.PromptCustomCardStartIdAsync(this);
            if (startId == null) return; // User cancelled
            _db.CustomCardStartId = startId.Value;
            _db.CustomOverrides ??= new CustomOverridesData();
            _db.CustomOverrides.CustomCardStartId = startId.Value;
        }

        _editingIndex = -1;
        FormTitle.Text = "Add Custom Faction";

        // Suggest next available index
        int nextIndex = 100;
        if (_db.CustomOverrides?.Factions.Count > 0)
            nextIndex = _db.CustomOverrides.Factions.Max(f => f.Index) + 1;

        NameBox.Text = "";
        TypeCombo.SelectedIndex = 0; // Nation
        FactionIdBox.Value = nextIndex;
        ParentNationBox.Value = -1;
        FileNameBox.Text = "";

        EditPanel.IsVisible = true;
        NameBox.Focus();
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (FactionList.SelectedItem is not FactionListItem selected) return;
        if (_db?.CustomOverrides == null) return;

        var faction = _db.CustomOverrides.Factions.FirstOrDefault(f => f.Index == selected.Index);
        if (faction != null)
        {
            _db.CustomOverrides.Factions.Remove(faction);
            _items.Remove(selected);
            ApplyToRegistry();
            WasModified = true;
        }
    }

    private void OnTypeComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Guard: event fires during InitializeComponent before controls are created
        if (ParentLabel == null || ParentNationBox == null) return;

        bool isClan = TypeCombo.SelectedIndex == 1;
        ParentLabel.IsVisible = isClan;
        ParentNationBox.IsVisible = isClan;
    }

    private void OnSaveEntryClick(object? sender, RoutedEventArgs e)
    {
        if (_db == null) return;

        var name = NameBox.Text?.Trim() ?? "";
        var fileName = FileNameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(fileName))
            return;

        _db.CustomOverrides ??= new CustomOverridesData();

        bool isNation = TypeCombo.SelectedIndex == 0;
        int factionId = (int)(FactionIdBox.Value ?? 100);
        int parentNation = (int)(ParentNationBox.Value ?? -1);

        if (_editingIndex >= 0)
        {
            // Update existing
            var existing = _db.CustomOverrides.Factions.FirstOrDefault(f => f.Index == _editingIndex);
            if (existing != null)
            {
                existing.Name = name;
                existing.ClanId = isNation ? 0 : factionId;
                existing.NationId = isNation ? factionId : parentNation;
                existing.FileName = fileName;
            }
        }
        else
        {
            // Determine the next available index
            int newIndex = 100;
            if (_db.CustomOverrides.Factions.Count > 0)
                newIndex = _db.CustomOverrides.Factions.Max(f => f.Index) + 1;

            _db.CustomOverrides.Factions.Add(new CustomFactionData
            {
                Index = newIndex,
                ClanId = isNation ? 0 : factionId,
                NationId = isNation ? factionId : parentNation,
                Name = name,
                FileName = fileName
            });
        }

        // Update MaxCustomFaction
        if (_db.CustomOverrides.Factions.Count > 0)
            _db.CustomOverrides.MaxCustomFaction = _db.CustomOverrides.Factions.Max(f => f.Index) + 1;

        LoadFactions();
        ApplyToRegistry();
        EditPanel.IsVisible = false;
        WasModified = true;
    }

    private void OnCancelEntryClick(object? sender, RoutedEventArgs e)
    {
        EditPanel.IsVisible = false;
    }

    private void ApplyToRegistry()
    {
        if (_db?.CustomOverrides == null)
        {
            ClanRegistry.ClearCustomFactions();
            return;
        }

        var definitions = new List<ClanDefinition>();
        foreach (var f in _db.CustomOverrides.Factions)
        {
            bool isNation = f.ClanId == 0;
            definitions.Add(new ClanDefinition
            {
                Id = isNation ? f.NationId : f.ClanId,
                Name = f.Name,
                Type = isNation ? FactionType.Nation : FactionType.Clan,
                Era = FactionEra.Custom,
                ParentNationId = isNation ? null : (f.NationId >= 0 ? f.NationId : null),
                DisplayColor = isNation ? ClanRegistry.GetCustomNationColor() : ClanRegistry.GetCustomClanColor(),
                FileName = f.FileName,
                IsCustom = true,
                CustomIndex = f.Index
            });
        }

        ClanRegistry.RegisterCustomFactions(definitions);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// Display item for the faction list.
/// </summary>
public class FactionListItem
{
    public int Index { get; }
    public string Name { get; }
    public string TypeLabel { get; }
    public string DisplayId { get; }
    public string FileName { get; }

    public FactionListItem(CustomFactionData data)
    {
        Index = data.Index;
        Name = data.Name;
        TypeLabel = data.ClanId == 0 ? "Nation" : "Clan";
        DisplayId = data.ClanId == 0 ? data.NationId.ToString() : data.ClanId.ToString();
        FileName = data.FileName;
    }
}
