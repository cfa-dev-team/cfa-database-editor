using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CfaDatabaseEditor.Models;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.Views;

public enum FactionsWindowMode
{
    Custom,
    BuiltIn
}

public partial class CustomFactionsWindow : Window
{
    private DatabaseService? _db;
    public bool WasModified { get; private set; }

    private FactionsWindowMode _mode = FactionsWindowMode.Custom;
    private ObservableCollection<FactionListItem> _items = new();
    private int _editingIndex = -1; // -1 = adding new, >= 0 = editing existing

    public CustomFactionsWindow()
    {
        InitializeComponent();
        FactionList.ItemsSource = _items;
    }

    public void SetDatabase(DatabaseService db, FactionsWindowMode mode = FactionsWindowMode.Custom)
    {
        _db = db;
        _mode = mode;
        ApplyModeUi();
        LoadFactions();
    }

    private void ApplyModeUi()
    {
        if (_mode == FactionsWindowMode.BuiltIn)
        {
            Title = "Built-in Factions";
            HeaderTitle.Text = "Built-in Clans & Nations";
            HeaderSubtitle.Text = "Manage built-in factions stored in NoUse.txt's CustomFaction array. " +
                                  "Cards in these factions use normal IDs.";
            FactionIdBox.Minimum = 1;
        }
        else
        {
            Title = "Custom Factions";
            HeaderTitle.Text = "Custom Clans & Nations";
            HeaderSubtitle.Text = "Manage custom factions stored in Custom Overrides.txt. " +
                                  "Use IDs 100+ for factions, 20000+ for cards.";
            FactionIdBox.Minimum = 1;
        }
    }

    private List<CustomFactionData> CurrentFactions => _mode == FactionsWindowMode.BuiltIn
        ? _db!.BuiltInFactions.Factions
        : (_db!.CustomOverrides ??= new CustomOverridesData()).Factions;

    private void LoadFactions()
    {
        _items.Clear();
        if (_db == null) return;

        foreach (var f in CurrentFactions.OrderBy(f => f.Index))
            _items.Add(new FactionListItem(f));
    }

    private void OnFactionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RemoveButton.IsEnabled = FactionList.SelectedItem != null;
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (_db == null) return;

        if (_mode == FactionsWindowMode.Custom)
        {
            // Prompt for CustomCardStartId when adding the very first custom faction
            bool isFirstFaction = (_db.CustomOverrides == null || _db.CustomOverrides.Factions.Count == 0)
                                  && _db.CustomCardStartId == null;
            if (isFirstFaction)
            {
                var startId = await MainWindow.PromptCustomCardStartIdAsync(this);
                if (startId == null) return;
                _db.CustomCardStartId = startId.Value;
                _db.CustomOverrides ??= new CustomOverridesData();
                _db.CustomOverrides.CustomCardStartId = startId.Value;
            }
        }

        _editingIndex = -1;
        FormTitle.Text = _mode == FactionsWindowMode.BuiltIn ? "Add Built-in Faction" : "Add Custom Faction";

        // Suggest defaults
        NameBox.Text = "";
        TypeCombo.SelectedIndex = 0; // Nation
        FactionIdBox.Value = SuggestNextFactionId(isClan: false);
        ParentNationBox.Value = -1;
        FileNameBox.Text = "";

        EditPanel.IsVisible = true;
        NameBox.Focus();
    }

    private int SuggestNextArrayIndex()
    {
        if (_mode == FactionsWindowMode.BuiltIn)
        {
            // First free slot in 0..99
            var used = CurrentFactions.Select(f => f.Index).ToHashSet();
            for (int i = 0; i < 100; i++)
                if (!used.Contains(i)) return i;
            return 99; // Full — caller will surface an error on save
        }
        else
        {
            // First free slot in 100+
            var used = CurrentFactions.Select(f => f.Index).ToHashSet();
            int i = 100;
            while (used.Contains(i)) i++;
            return i;
        }
    }

    private int SuggestNextFactionId(bool isClan)
    {
        // Built-in: first gap in 1..N (skipping IDs already used by hardcoded or dynamic built-ins).
        // Custom: 100+ (existing convention).
        if (_mode == FactionsWindowMode.BuiltIn)
        {
            var used = isClan
                ? ClanRegistry.AllClans.Select(c => c.Id).ToHashSet()
                : ClanRegistry.AllNations.Select(n => n.Id).ToHashSet();
            int i = 1;
            while (used.Contains(i)) i++;
            return i;
        }
        return 100;
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (FactionList.SelectedItem is not FactionListItem selected) return;
        if (_db == null) return;

        var faction = CurrentFactions.FirstOrDefault(f => f.Index == selected.Index);
        if (faction == null) return;

        // Built-in removal: warn if cards reference this faction.
        if (_mode == FactionsWindowMode.BuiltIn)
        {
            bool isNation = faction.ClanId == 0;
            int factionId = isNation ? faction.NationId : faction.ClanId;
            int cardCount = _db.AllCards.Count(c =>
                isNation ? c.DCards == factionId : c.CardInClan == factionId);

            if (cardCount > 0)
            {
                _ = ShowRemoveWarningAsync(faction, cardCount, () => DoRemove(selected, faction));
                return;
            }
        }

        DoRemove(selected, faction);
    }

    private async Task ShowRemoveWarningAsync(CustomFactionData faction, int cardCount, Action onConfirm)
    {
        var confirmed = false;
        var dialog = new Window
        {
            Title = "Remove Faction",
            Width = 420,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Removing \"{faction.Name}\" will break {cardCount} existing card(s) " +
                   $"that reference this faction — they will not be loaded by the CFA engine. " +
                   "Continue anyway?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 13
        });
        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        var ok = new Button { Content = "Remove", Width = 90, IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        ok.Click += (_, _) => { confirmed = true; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        await dialog.ShowDialog(this);
        if (confirmed) onConfirm();
    }

    private void DoRemove(FactionListItem selected, CustomFactionData faction)
    {
        if (_db == null) return;
        CurrentFactions.Remove(faction);
        _items.Remove(selected);
        MarkModified();
        ApplyToRegistry();
    }

    private void OnTypeComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Guard: event fires during InitializeComponent before controls are created
        if (ParentLabel == null || ParentNationBox == null) return;

        bool isClan = TypeCombo.SelectedIndex == 1;
        ParentLabel.IsVisible = isClan;
        ParentNationBox.IsVisible = isClan;

        // Update suggested faction ID based on type when adding a new entry
        if (_editingIndex < 0)
            FactionIdBox.Value = SuggestNextFactionId(isClan);
    }

    private void OnSaveEntryClick(object? sender, RoutedEventArgs e)
    {
        if (_db == null) return;

        var name = NameBox.Text?.Trim() ?? "";
        var fileName = FileNameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(fileName))
            return;

        bool isNation = TypeCombo.SelectedIndex == 0;
        int factionId = (int)(FactionIdBox.Value ?? 1);
        int parentNation = (int)(ParentNationBox.Value ?? -1);

        if (_editingIndex >= 0)
        {
            var existing = CurrentFactions.FirstOrDefault(f => f.Index == _editingIndex);
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
            int newIndex = SuggestNextArrayIndex();

            // Built-in mode caps at 99
            if (_mode == FactionsWindowMode.BuiltIn && newIndex >= 100)
                return; // Silently fail — array is full

            CurrentFactions.Add(new CustomFactionData
            {
                Index = newIndex,
                ClanId = isNation ? 0 : factionId,
                NationId = isNation ? factionId : parentNation,
                Name = name,
                FileName = fileName
            });
        }

        // Update MaxCustomFaction for the custom-overrides container
        if (_mode == FactionsWindowMode.Custom && _db.CustomOverrides != null)
        {
            _db.CustomOverrides.MaxCustomFaction = _db.CustomOverrides.Factions.Count > 0
                ? _db.CustomOverrides.Factions.Max(f => f.Index) + 1
                : 0;
        }

        MarkModified();
        LoadFactions();
        ApplyToRegistry();
        EditPanel.IsVisible = false;
    }

    private void OnCancelEntryClick(object? sender, RoutedEventArgs e)
    {
        EditPanel.IsVisible = false;
    }

    private void MarkModified()
    {
        WasModified = true;
        if (_mode == FactionsWindowMode.BuiltIn)
            _db!.BuiltInFactions.IsModified = true;
    }

    private void ApplyToRegistry()
    {
        if (_db == null) return;

        if (_mode == FactionsWindowMode.BuiltIn)
        {
            ApplyBuiltInsToRegistry();
        }
        else
        {
            ApplyCustomsToRegistry();
        }
    }

    private void ApplyCustomsToRegistry()
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

    private void ApplyBuiltInsToRegistry()
    {
        if (_db == null) { ClanRegistry.ClearDynamicBuiltInFactions(); return; }

        var definitions = new List<ClanDefinition>();
        foreach (var f in _db.BuiltInFactions.Factions)
        {
            bool isNation = f.ClanId == 0;
            var hardcoded = isNation ? ClanRegistry.GetNationById(f.NationId) : ClanRegistry.GetClanById(f.ClanId);
            var color = hardcoded?.DisplayColor
                        ?? (isNation ? ClanRegistry.GetDynamicBuiltInNationColor() : ClanRegistry.GetDynamicBuiltInClanColor());

            definitions.Add(new ClanDefinition
            {
                Id = isNation ? f.NationId : f.ClanId,
                Name = f.Name,
                Type = isNation ? FactionType.Nation : FactionType.Clan,
                Era = hardcoded?.Era ?? FactionEra.Crossover,
                ParentNationId = isNation ? null : (f.NationId >= 0 ? f.NationId : null),
                DisplayColor = color,
                FileName = f.FileName,
                IsCustom = false,
                CustomIndex = f.Index
            });
        }

        ClanRegistry.RegisterDynamicBuiltInFactions(definitions);
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
