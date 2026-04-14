using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CfaDatabaseEditor;
using CfaDatabaseEditor.Models;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.ViewModels;

public partial class EnSyncViewModel : ViewModelBase
{
    private readonly WebScraperService _scraper = new();
    private readonly ImageMatcherService _matcher = new();
    private DatabaseService? _db;
    private ImageService? _imageService;

    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private bool _isScraping;
    [ObservableProperty] private SyncResult? _selectedResult;

    // Image previews for the selected result
    [ObservableProperty] private Bitmap? _enPreviewImage;
    [ObservableProperty] private Bitmap? _cfaPreviewImage;

    public ObservableCollection<SyncResult> Results { get; } = new();

    public void SetDatabase(DatabaseService db, ImageService imageService)
    {
        _db = db;
        _imageService = imageService;
    }

    partial void OnSelectedResultChanged(SyncResult? value)
    {
        EnPreviewImage = null;
        CfaPreviewImage = null;

        if (value == null) return;

        // Load EN image from scraped bytes
        if (value.EnImageData != null)
        {
            try
            {
                using var stream = new MemoryStream(value.EnImageData);
                EnPreviewImage = new Bitmap(stream);
            }
            catch { }
        }

        LoadCfaPreview(value);
    }

    private void LoadCfaPreview(SyncResult result)
    {
        if (_db == null) return;

        var cardStat = result.MatchedCardStat;
        if (cardStat == null) { CfaPreviewImage = null; return; }

        var path = _db.GetCardImagePath(cardStat.Value);
        if (path != null && File.Exists(path))
        {
            try { CfaPreviewImage = new Bitmap(path); }
            catch { CfaPreviewImage = null; }
        }
        else
        {
            CfaPreviewImage = null;
        }
    }

    private void OnResultPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SyncResult.MatchedCardStat) || sender is not SyncResult result || _db == null)
            return;

        // Look up the card by the manually entered ID
        var card = result.MatchedCardStat.HasValue
            ? _db.AllCards.FirstOrDefault(c => c.CardStat == result.MatchedCardStat.Value)
            : null;
        result.MatchedCard = card;
        result.Confidence = 0;

        // Refresh CFA preview if this is the currently selected result
        if (result == SelectedResult)
            LoadCfaPreview(result);
    }

    [RelayCommand]
    private async Task ScrapeAsync()
    {
        if (string.IsNullOrWhiteSpace(Url) || IsScraping) return;

        IsScraping = true;
        foreach (var r in Results) r.PropertyChanged -= OnResultPropertyChanged;
        Results.Clear();

        try
        {
            if (_db?.CardSpritePath != null)
            {
                var indexProgress = new Progress<string>(s => ProgressText = s);
                await _matcher.BuildIndexAsync(_db.CardSpritePath, indexProgress);
            }

            var progress = new Progress<string>(s => ProgressText = s);
            await foreach (var scraped in _scraper.ScrapeExpansionAsync(Url, progress))
            {
                var imageData = scraped.ImageData != null
                    ? ImageService.FixRotationIfNeeded(scraped.ImageData)
                    : null;

                var result = new SyncResult
                {
                    EnName = scraped.Name,
                    EnCardNo = scraped.CardNo,
                    EnImageUrl = scraped.ImageUrl,
                    EnImageData = imageData
                };

                if (imageData != null && _db != null)
                {
                    var match = _matcher.FindBestMatch(imageData);
                    if (match.HasValue)
                    {
                        result.MatchedCard = _db.AllCards.FirstOrDefault(c => c.CardStat == match.Value.CardStat);
                        result.Confidence = match.Value.Confidence;
                    }
                }

                result.PropertyChanged += OnResultPropertyChanged;
                Results.Add(result);
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Scraping cancelled.";
        }
        catch (Exception ex)
        {
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
    private void ApproveAboveThreshold()
    {
        int count = 0;
        foreach (var result in Results)
        {
            if (result.Confidence >= 75 && result.MatchedCard != null && !result.IsApproved)
            {
                result.IsApproved = true;
                count++;
            }
        }
        ProgressText = $"Approved {count} entries above 75% confidence.";
    }

    [RelayCommand]
    private void ApproveSelected()
    {
        if (SelectedResult == null) return;
        SelectedResult.IsApproved = true;
        ProgressText = $"Approved: {SelectedResult.EnName}";
    }

    [RelayCommand]
    private void ApplySelected()
    {
        if (_db == null || _imageService == null) return;

        int applied = 0;
        int skippedEncoding = 0;
        foreach (var result in Results.Where(r => r.IsApproved && !r.IsApplied && r.MatchedCard != null))
        {
            if (result.HasEncodingIssue)
            {
                skippedEncoding++;
                continue;
            }

            result.MatchedCard!.CardName = result.EnName;
            result.MatchedCard.IsModified = true;

            if (result.EnImageData != null)
            {
                try
                {
                    _imageService.ImportCardImageFromBytes(result.MatchedCard.CardStat, result.EnImageData);
                }
                catch { }
            }

            result.IsApplied = true;
            applied++;
        }

        var msg = $"Applied {applied} changes.";
        if (skippedEncoding > 0)
            msg += $" Skipped {skippedEncoding} with encoding issues (fix the name and retry).";
        msg += " Save the database to persist.";
        ProgressText = msg;
    }
}
