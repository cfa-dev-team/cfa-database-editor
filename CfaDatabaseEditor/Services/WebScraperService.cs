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

    /// <summary>
    /// Scrapes card images from the JP "Today's Card" archive page.
    /// Uses WordPress pagination: /todays-card/archive/page/{pageNum}/
    /// </summary>
    public async IAsyncEnumerable<ScrapedCard> ScrapeJpArchiveAsync(
        int page = 1,
        IProgress<string>? progress = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        var pageUrl = page <= 1
            ? "https://cf-vanguard.com/todays-card/archive/"
            : $"https://cf-vanguard.com/todays-card/archive/page/{page}/";

        Program.Log?.WriteLine($"[JP-ARCHIVE] Fetching URL: {pageUrl}");
        progress?.Report($"Fetching archive page {page}...");

        string html;
        try
        {
            html = await Http.GetStringAsync(pageUrl, token);
            Program.Log?.WriteLine($"[JP-ARCHIVE] HTML fetched, length={html.Length}");
        }
        catch (HttpRequestException ex)
        {
            Program.Log?.WriteLine($"[JP-ARCHIVE] HTTP error: {ex}");
            progress?.Report($"Error fetching page: {ex.Message}");
            yield break;
        }

        var parser = new HtmlParser();
        var doc = await parser.ParseDocumentAsync(html, token);

        // Log all img tags to understand page structure
        var allImgs = doc.QuerySelectorAll("img");
        Program.Log?.WriteLine($"[JP-ARCHIVE] Total <img> tags on page: {allImgs.Length}");
        foreach (var debugImg in allImgs.Take(20))
        {
            var debugSrc = debugImg.GetAttribute("src") ?? "(no src)";
            var debugParent = debugImg.ParentElement?.TagName ?? "(no parent)";
            var debugGrandparent = debugImg.ParentElement?.ParentElement?.TagName ?? "(no grandparent)";
            Program.Log?.WriteLine($"[JP-ARCHIVE]   img src={debugSrc}  parent={debugParent}  grandparent={debugGrandparent}");
        }

        // The archive page uses <ul> lists with <li> items containing <img> tags
        var images = doc.QuerySelectorAll("ul li img")
            .Where(img =>
            {
                var src = img.GetAttribute("src") ?? "";
                return src.Contains("/todays-card/") && src.EndsWith(".png");
            })
            .ToList();

        Program.Log?.WriteLine($"[JP-ARCHIVE] Matched card images: {images.Count}");

        if (images.Count == 0)
        {
            // Try broader selectors as fallback debug
            var allUlLiImgs = doc.QuerySelectorAll("ul li img").Length;
            var todaysCardImgs = doc.QuerySelectorAll("img")
                .Count(i => (i.GetAttribute("src") ?? "").Contains("todays-card"));
            var pngImgs = doc.QuerySelectorAll("img")
                .Count(i => (i.GetAttribute("src") ?? "").EndsWith(".png"));
            Program.Log?.WriteLine($"[JP-ARCHIVE] Fallback: ul>li>img={allUlLiImgs}, todays-card imgs={todaysCardImgs}, .png imgs={pngImgs}");

            progress?.Report("No card images found on this page.");
            yield break;
        }

        int total = images.Count;
        int downloaded = 0;

        foreach (var img in images)
        {
            token.ThrowIfCancellationRequested();

            var imgSrc = img.GetAttribute("src") ?? "";
            if (string.IsNullOrEmpty(imgSrc)) continue;

            // Ensure absolute URL
            if (imgSrc.StartsWith("/"))
                imgSrc = "https://cf-vanguard.com" + imgSrc;

            Program.Log?.WriteLine($"[JP-ARCHIVE] Downloading image: {imgSrc}");

            var card = new ScrapedCard
            {
                Name = string.Empty,
                CardNo = string.Empty,
                ImageUrl = imgSrc
            };

            try
            {
                card.ImageData = await Http.GetByteArrayAsync(imgSrc, token);
                Program.Log?.WriteLine($"[JP-ARCHIVE]   OK, {card.ImageData.Length} bytes");
            }
            catch (Exception ex)
            {
                Program.Log?.WriteLine($"[JP-ARCHIVE]   FAILED: {ex.Message}");
            }

            downloaded++;
            progress?.Report($"Downloaded {downloaded}/{total} images (page {page})...");
            yield return card;
        }

        progress?.Report($"Done. {downloaded} images from page {page}.");
        Program.Log?.WriteLine($"[JP-ARCHIVE] Scrape complete. {downloaded}/{total} images downloaded.");
    }

    public void Stop()
    {
        _cts?.Cancel();
    }
}
