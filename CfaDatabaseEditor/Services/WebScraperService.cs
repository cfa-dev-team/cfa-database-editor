using System.Net.Http;
using AngleSharp;
using AngleSharp.Html.Parser;

namespace CfaDatabaseEditor.Services;

public class ScrapedCard
{
    public string Name { get; set; } = string.Empty;
    public string CardNo { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
}

public class WebScraperService
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) CFA-Database-Editor/1.0" }
        }
    };

    private CancellationTokenSource? _cts;

    /// <summary>
    /// Scrapes all card entries from the given expansion URL.
    /// Uses the AJAX pagination endpoint discovered from the site's JavaScript.
    /// </summary>
    public async IAsyncEnumerable<ScrapedCard> ScrapeExpansionAsync(
        string url,
        IProgress<string>? progress = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        // Extract expansion parameter from URL
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var expansion = query["expansion"];
        if (string.IsNullOrEmpty(expansion))
            throw new ArgumentException("URL must contain an 'expansion' parameter");

        var baseUrl = $"{uri.Scheme}://{uri.Host}";
        int page = 1;
        int totalCards = 0;

        while (!token.IsCancellationRequested)
        {
            progress?.Report($"Scraping page {page}...");

            var pageUrl = $"{baseUrl}/cardlist/cardsearch_ex/?expansion={expansion}&view=image&page={page}";
            string html;
            try
            {
                html = await Http.GetStringAsync(pageUrl, token);
            }
            catch (HttpRequestException)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(html) || html.Trim().Length < 10)
                break;

            var parser = new HtmlParser();
            var doc = await parser.ParseDocumentAsync(html, token);
            var items = doc.QuerySelectorAll("li.ex-item");

            if (items.Length == 0)
                break;

            foreach (var item in items)
            {
                token.ThrowIfCancellationRequested();

                var link = item.QuerySelector("a");
                var img = item.QuerySelector("img");
                if (link == null || img == null) continue;

                var href = link.GetAttribute("href") ?? "";
                var imgSrc = img.GetAttribute("src") ?? "";
                var altName = img.GetAttribute("title") ?? img.GetAttribute("alt") ?? "";

                // Extract card number from href
                var hrefQuery = "";
                if (href.Contains('?'))
                    hrefQuery = href.Substring(href.IndexOf('?'));
                var hrefParams = System.Web.HttpUtility.ParseQueryString(hrefQuery);
                var cardNo = hrefParams["cardno"] ?? "";

                // Build absolute image URL
                if (imgSrc.StartsWith("/"))
                    imgSrc = baseUrl + imgSrc;

                var card = new ScrapedCard
                {
                    Name = altName,
                    CardNo = cardNo,
                    ImageUrl = imgSrc
                };

                // Download image
                try
                {
                    card.ImageData = await Http.GetByteArrayAsync(imgSrc, token);
                }
                catch
                {
                    // Skip cards where image download fails
                }

                totalCards++;
                progress?.Report($"Scraped {totalCards} cards (page {page})...");
                yield return card;
            }

            page++;
            // Small delay to be respectful
            await Task.Delay(300, token);
        }

        progress?.Report($"Done. {totalCards} cards scraped.");
    }

    public void Stop()
    {
        _cts?.Cancel();
    }
}
