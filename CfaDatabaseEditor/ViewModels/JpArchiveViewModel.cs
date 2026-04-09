using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CfaDatabaseEditor.Models;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.ViewModels;

public partial class JpArchiveViewModel : ViewModelBase
{
    private readonly WebScraperService _scraper = new();
    private DatabaseService? _db;
    private ImageService? _imageService;

    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private bool _isScraping;

    public ObservableCollection<JpArchiveCard> Cards { get; } = new();

    public ObservableCollection<ClanDefinition> NationOptions { get; } = new();
    public ObservableCollection<ClanDefinition> ClanOptions { get; } = new();

    private static readonly ClanDefinition NoNationSentinel = new()
    {
        Id = 0, Name = "(None)", Type = FactionType.Nation,
        DisplayColor = Avalonia.Media.Color.Parse("#808080")
    };

    private static readonly ClanDefinition NoClanSentinel = new()
    {
        Id = 0, Name = "(None)", Type = FactionType.Clan,
        DisplayColor = Avalonia.Media.Color.Parse("#808080")
    };

    public JpArchiveViewModel()
    {
        NationOptions.Add(NoNationSentinel);
        foreach (var n in ClanRegistry.AllNations.Where(n => n.Era != FactionEra.Other))
            NationOptions.Add(n);

        ClanOptions.Add(NoClanSentinel);
        foreach (var c in ClanRegistry.AllClans.Where(c => c.Era != FactionEra.Other))
            ClanOptions.Add(c);
    }

    public void SetDatabase(DatabaseService db, ImageService imageService)
    {
        _db = db;
        _imageService = imageService;
    }

    [RelayCommand]
    private async Task ScrapeAsync()
    {
        if (IsScraping) return;

        IsScraping = true;
        Cards.Clear();
        Program.Log?.WriteLine($"[JP-ARCHIVE-VM] Starting scrape, page={Page}");

        try
        {
            var progress = new Progress<string>(s => ProgressText = s);
            await foreach (var scraped in _scraper.ScrapeJpArchiveAsync(Page, progress))
            {
                Program.Log?.WriteLine($"[JP-ARCHIVE-VM] Got card: url={scraped.ImageUrl}, hasData={scraped.ImageData != null}, dataLen={scraped.ImageData?.Length ?? 0}");
                var card = new JpArchiveCard
                {
                    ImageUrl = scraped.ImageUrl,
                    ImageData = scraped.ImageData,
                    SelectedNation = NoNationSentinel,
                    SelectedClan = NoClanSentinel
                };
                Cards.Add(card);
                Program.Log?.WriteLine($"[JP-ARCHIVE-VM] Card added to collection, total={Cards.Count}");
            }
            Program.Log?.WriteLine($"[JP-ARCHIVE-VM] Scrape loop finished, total cards={Cards.Count}");
        }
        catch (OperationCanceledException)
        {
            Program.Log?.WriteLine("[JP-ARCHIVE-VM] Scraping cancelled");
            ProgressText = "Scraping cancelled.";
        }
        catch (Exception ex)
        {
            Program.Log?.WriteLine($"[JP-ARCHIVE-VM] Scrape error: {ex}");
            ProgressText = $"Error: {ex.Message}";
        }
        finally
        {
            IsScraping = false;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _scraper.Stop();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var card in Cards)
            card.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var card in Cards)
            card.IsSelected = false;
    }

    /// <summary>
    /// Sets the chosen nation/clan on all currently selected cards.
    /// </summary>
    [RelayCommand]
    private void ApplyNationClanToSelected(object? parameter)
    {
        // This is intentionally left as a no-op; each card has its own dropdowns.
    }

    /// <summary>
    /// Creates new database cards for every selected entry with a non-empty name.
    /// Returns true if any cards were added (so the window can close).
    /// </summary>
    public bool Apply()
    {
        if (_db == null || _imageService == null) return false;

        var toProcess = Cards.Where(c => c.IsSelected && !string.IsNullOrWhiteSpace(c.CardName)).ToList();
        if (toProcess.Count == 0)
        {
            ProgressText = "No cards selected (or names are empty).";
            return false;
        }

        int added = 0;
        foreach (var entry in toProcess)
        {
            // Determine target file: prefer clan file, fall back to nation file
            string? targetFile = null;
            if (entry.SelectedClan != null && entry.SelectedClan.Id != 0 && entry.SelectedClan.FileName != null)
                targetFile = entry.SelectedClan.FileName;
            else if (entry.SelectedNation != null && entry.SelectedNation.Id != 0 && entry.SelectedNation.FileName != null)
                targetFile = entry.SelectedNation.FileName;

            if (targetFile == null)
            {
                // Default to Cray Elemental if no nation/clan chosen
                targetFile = ClanRegistry.CrayElemental.FileName!;
            }

            var card = _db.CreateNewCard(targetFile);
            card.CardName = entry.CardName.Trim();

            // Set nation
            if (entry.SelectedNation != null && entry.SelectedNation.Id != 0)
                card.DCards = entry.SelectedNation.Id;
            else if (entry.SelectedClan?.ParentNationId != null)
                card.DCards = entry.SelectedClan.ParentNationId.Value;

            // Set clan
            if (entry.SelectedClan != null && entry.SelectedClan.Id != 0)
                card.CardInClan = entry.SelectedClan.Id;

            // Import image
            if (entry.ImageData != null)
            {
                try
                {
                    _imageService.ImportCardImageFromBytes(card.CardStat, entry.ImageData);
                }
                catch { }
            }

            added++;
        }

        ProgressText = $"Added {added} cards. Save the database to persist.";
        return true;
    }
}
