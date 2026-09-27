using System.Text.RegularExpressions;
using System.Xml.Linq;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Services;

public static class RssScraperHelper
{
    private static DateTime _lastFreshnessCheck = DateTime.MinValue;
    private static readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

    public static async Task<int> EnsureFreshnessAsync(NewsDbContext db, HttpClient client, CancellationToken cancellationToken = default)
    {
        if ((DateTime.UtcNow - _lastFreshnessCheck).TotalMinutes < 5)
        {
            return 0;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if ((DateTime.UtcNow - _lastFreshnessCheck).TotalMinutes < 5)
            {
                return 0;
            }

            var today = DateTime.UtcNow.Date;
            var hasTodayArticles = db.Articles.Any(a => a.PublishedAt != null && a.PublishedAt >= today);

            if (hasTodayArticles)
            {
                _lastFreshnessCheck = DateTime.UtcNow;
                return 0;
            }

            int totalScraped = 0;
            var sources = db.Sources.Where(s => !string.IsNullOrWhiteSpace(s.RssUrl)).ToList();
            if (!sources.Any())
            {
                sources = new List<Source>
                {
                    new Source { Id = "punch", Name = "Punch Newspaper", RssUrl = "https://punchng.com/feed/" },
                    new Source { Id = "guardian", Name = "The Guardian Nigeria", RssUrl = "https://news.google.com/rss/search?q=site:guardian.ng&hl=en-NG&gl=NG&ceid=NG:en" },
                    new Source { Id = "premiumtimes", Name = "Premium Times", RssUrl = "https://www.premiumtimesng.com/feed" },
                    new Source { Id = "vanguard", Name = "Vanguard News", RssUrl = "https://www.vanguardngr.com/feed/" },
                    new Source { Id = "arise", Name = "Arise Tv", RssUrl = "https://arise.tv/feed/" },
                    new Source { Id = "businessday", Name = "BusinessDay Nigeria", RssUrl = "https://businessday.ng/feed/" },
                    new Source { Id = "lindaikeji", Name = "Linda Ikeji's Blog", RssUrl = "https://www.lindaikejisblog.com/feed" }
                };
            }

            foreach (var src in sources)
            {
                if (!string.IsNullOrWhiteSpace(src.RssUrl))
                {
                    totalScraped += await ScrapeAndSaveFeedAsync(db, client, src.Name, src.RssUrl, cancellationToken);
                }
            }

            _lastFreshnessCheck = DateTime.UtcNow;
            return totalScraped;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public static async Task<int> ScrapeAndSaveFeedAsync(NewsDbContext db, HttpClient client, string sourceName, string rssUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rssUrl)) return 0;

        var items = new List<Article>();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, rssUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var doc = XDocument.Load(stream);

            // Handle standard RSS <item> elements
            var rssItems = doc.Descendants("item");
            foreach (var item in rssItems)
            {
                var title = item.Element("title")?.Value ?? string.Empty;
                var link = item.Element("link")?.Value;
                var pub = item.Element("pubDate")?.Value;
                DateTime? published = null;
                if (DateTimeOffset.TryParse(pub, out var dto)) published = dto.UtcDateTime;
                var description = item.Element("description")?.Value;
                var content = item.Element(XName.Get("encoded", "http://purl.org/rss/1.0/modules/content/"))?.Value;

                var imageUrl = ExtractImageUrl(item, description, content);

                items.Add(CreateArticleEntity(title, link, description ?? content, sourceName, published, imageUrl));
            }

            // Handle Atom <entry> elements if not RSS
            if (!items.Any())
            {
                XNamespace atomNs = "http://www.w3.org/2005/Atom";
                var atomEntries = doc.Descendants(atomNs + "entry");
                foreach (var entry in atomEntries)
                {
                    var title = entry.Element(atomNs + "title")?.Value ?? string.Empty;
                    var link = entry.Element(atomNs + "link")?.Attribute("href")?.Value;
                    var pub = entry.Element(atomNs + "published")?.Value ?? entry.Element(atomNs + "updated")?.Value;
                    DateTime? published = null;
                    if (DateTimeOffset.TryParse(pub, out var dto)) published = dto.UtcDateTime;
                    var summary = entry.Element(atomNs + "summary")?.Value ?? entry.Element(atomNs + "content")?.Value;

                    var imageUrl = ExtractImageUrl(entry, summary, null);
                    items.Add(CreateArticleEntity(title, link, summary, sourceName, published, imageUrl));
                }
            }
        }
        catch
        {
            // Logging or graceful fallthrough for invalid/blocked feed
            return 0;
        }

        if (!items.Any()) return 0;

        var existingUrls = db.Articles.Where(a => a.Url != null).Select(a => a.Url!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingTitles = db.Articles.Select(a => a.Title).ToList().Select(NormalizeTitle).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newArticles = new List<Article>();
        foreach (var art in items)
        {
            if (string.IsNullOrWhiteSpace(art.Title)) continue;

            var normTitle = NormalizeTitle(art.Title);
            if ((!string.IsNullOrEmpty(art.Url) && existingUrls.Contains(art.Url)) || existingTitles.Contains(normTitle))
            {
                continue;
            }

            newArticles.Add(art);
            if (!string.IsNullOrEmpty(art.Url)) existingUrls.Add(art.Url);
            existingTitles.Add(normTitle);
        }

        if (newArticles.Any())
        {
            db.Articles.AddRange(newArticles);
            await db.SaveChangesAsync(cancellationToken);
            SyncArticlesJson(db);
        }

        return newArticles.Count;
    }

    private static Article CreateArticleEntity(string title, string? url, string? summary, string sourceName, DateTime? publishedAt, string? imageUrl)
    {
        title = NormalizeTitle(title);
        var cleanSummary = StripHtml(summary);
        var cat = Categorize(title, cleanSummary);
        if (cat == "General")
        {
            if (sourceName.Contains("Linda Ikeji", StringComparison.OrdinalIgnoreCase))
                cat = "Entertainment";
            else if (sourceName.Contains("BusinessDay", StringComparison.OrdinalIgnoreCase))
                cat = "Business";
        }

        return new Article
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Url = url,
            Summary = cleanSummary,
            Source = sourceName,
            Category = cat,
            PublishedAt = publishedAt ?? DateTime.UtcNow,
            ImageUrl = imageUrl
        };
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        return Regex.Replace(title, "\"|'|\\s+", " ").Trim();
    }

    private static readonly Regex SportsRegex = new(
        @"\b(super eagles|super falcons|afcon|fifa|caf|epl|premier league|champions league|europa league|la liga|serie a|bundesliga|npfl|fa cup|ballon d'or|olympic|osimhen|lookman|boniface|arsenal|chelsea|manchester united|man united|man city|liverpool|real madrid|barcelona|galatasaray|atalanta|nwabali|amusan|joshua|boxer|boxing|bout|knockout|heavyweight|goalscorer|hat-trick|striker|midfielder|winger|goalkeeper|referee|head coach|transfer window|transfer fee|clean sheet|d'tigress|d'tigers|table tennis|aruna|quadri)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PoliticsRegex = new(
        @"\b(tinubu|bola tinubu|shettima|atiku|peter obi|kwankwaso|wike|sanwo-olu|abiodun|makinde|adeleke|soludo|alex otti|peter mbah|zulum|akpabio|godswill akpabio|barau jibrin|tajudeen abbas|gbajabiamila|apc|pdp|labour party|\blp\b|nnpp|apga|inec|senate|national assembly|house of representatives|house of reps|presidency|aso rock|federal executive council|\bfec\b|governor|governors|governorship|gubernatorial|deputy governor|commissioner|commissioners|local government autonomy|electoral act|electoral bill|election appeal|tribunal|by-election|primary election|peace accord|defected|defection|impeachment|cabinet reshuffle|minister of|special adviser|state budget|appropriation bill)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BusinessRegex = new(
        @"\b(cbn|central bank of nigeria|olayemi cardoso|monetary policy|all-share index|\bngx\b|market capitalization|naira|forex|foreign exchange|nafem|us dollar|fx market|headline inflation|food inflation|consumer price index|\bnbs\b|gross domestic product|\bgdp\b|dmo|debt management|treasury bills|fgn bonds|dangote|refinery|nnpc|nuprc|nmdpra|crude oil production|barrels per day|\bbpd\b|opec|pms price|pms petrol|fuel subsidy|afcfta|firs|company income tax|tier-1 banks|zenith bank|gtbank|access holdings|first bank|fidelity bank|dividend payout|shareholders|recapitalization|rights issue|profit after tax|world bank|financing package|\bimf\b|customs revenue)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CrimeRegex = new(
        @"\b(efcc|economic and financial crimes|icpc|ndlea|police command|commissioner of police|\bigp\b|kayode egbetokun|dss|department of state services|civil defence|nscdc|kidnapping|kidnappers|kidnapper|abducted|abduction|hostage|ransom|bandits|banditry|armed robbery|robbers|burglary|cult clash|cultism|cyber crime|cybercrime|yahoo boys|advance fee fraud|wire fraud|money laundering|embezzlement|arms trafficking|illegal firearms|ammunition seized|drug trafficking|cocaine seizure|cannabis plantation|human trafficking|ritual killing|oil bunkering|illegal refinery|pirates|piracy|arraigned|docked|remanded in custody|convicted|sentenced to imprisonment|prosecution witness|terrorists neutralized|iswap|boko haram|counter-terrorism)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EntertainmentRegex = new(
        @"\b(burna boy|wizkid|davido|asake|rema|tems|ayra starr|tiwa savage|olamide|fireboy|kizz daniel|shallipopi|odumodublvck|phyno|flavour|simi|adekunle gold|amvca|africa magic viewers|grammy awards|grammy nomination|headies|billboard|spotify streams|apple music chart|o2 arena|sold-out concert|album release|nollywood|funke akindele|kunle afolayan|genevieve nnaji|richard mofe-damijo|\brmd\b|femi adebayo|sola sobowale|box office record|highest grossing movie|cinema release|bbnaija|big brother naija|housemates|eviction night|reality tv show|fashion week)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TechnologyRegex = new(
        @"\b(fintech|startup|series [a-d]|seed funding|venture capital|\bvc\b|flutterwave|paystack|moniepoint|opay|piggyvest|kuda bank|andela|interswitch|chowdeck|moove|\bai\b|artificial intelligence|machine learning|large language model|generative ai|cloud computing|azure|google cloud|\baws\b|data center|starlink|satellite internet|\b5g\b|broadband|telecoms|\bnitda\b|\bncc\b|cybersecurity|software engineering|developer ecosystem|app store|play store|mobile app|web3|blockchain|crypto exchange)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GeneralRegex = new(
        @"\b(frsc|road safety|road crash|fatal accident|highway collision|cholera outbreak|lassa fever|ncdc|yellow fever|immunization|public health|teaching hospital|ministry of education|\bwaec\b|\bjamb\b|\butme\b|\bneco\b|\basuu\b|nelfund|student loan|nema|flood disaster|earth tremor|seismic|weather forecast|nimet|rainstorm|nlc|tuc|warning strike|minimum wage|salary arrears|christian association of nigeria|catholic bishops|nscia|sultan of sokoto|ooni of ife|traditional ruler|public holiday|united nations|\bun\b|visas? category|visa categories|visa application|embassy)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string StripHtml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var clean = Regex.Replace(input, "<.*?>", " ");
        clean = System.Net.WebUtility.HtmlDecode(clean);
        clean = Regex.Replace(clean, @"https?://\S+", " ");
        clean = Regex.Replace(clean, @"\b(utm_source|utm_medium|rss|web)\b", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\s+", " ");
        return clean.Trim();
    }

    private static string Categorize(string title, string summary)
    {
        var scores = new Dictionary<string, int>
        {
            { "Sports", SportsRegex.Matches(title).Count * 3 + SportsRegex.Matches(summary).Count },
            { "Politics", PoliticsRegex.Matches(title).Count * 3 + PoliticsRegex.Matches(summary).Count },
            { "Business", BusinessRegex.Matches(title).Count * 3 + BusinessRegex.Matches(summary).Count },
            { "Crime", CrimeRegex.Matches(title).Count * 3 + CrimeRegex.Matches(summary).Count },
            { "Entertainment", EntertainmentRegex.Matches(title).Count * 3 + EntertainmentRegex.Matches(summary).Count },
            { "Technology", TechnologyRegex.Matches(title).Count * 3 + TechnologyRegex.Matches(summary).Count },
            { "General", GeneralRegex.Matches(title).Count * 3 + GeneralRegex.Matches(summary).Count }
        };

        var best = scores.OrderByDescending(kv => kv.Value).First();
        return best.Value > 0 ? best.Key : "General";
    }

    private static string? ExtractImageUrl(XElement item, string? description, string? content)
    {
        // Try <enclosure url="..." />
        var enc = item.Element("enclosure")?.Attribute("url")?.Value;
        if (!string.IsNullOrWhiteSpace(enc) && enc.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return enc;

        // Try <media:content url="..." />
        var mediaNs = XNamespace.Get("http://search.yahoo.com/mrss/");
        var media = item.Element(mediaNs + "content")?.Attribute("url")?.Value;
        if (!string.IsNullOrWhiteSpace(media) && media.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return media;

        // Try img src in HTML description/content
        var text = (description ?? string.Empty) + " " + (content ?? string.Empty);
        var match = Regex.Match(text, @"<img\s+[^>]*src=[""'](https?://[^""']+)[""']", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    private static void SyncArticlesJson(NewsDbContext db)
    {
        try
        {
            var articles = db.Articles
                .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
                .Take(1000)
                .ToList();

            var dataPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "data", "articles.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "data", "articles.json")
            };

            var json = System.Text.Json.JsonSerializer.Serialize(articles);
            foreach (var path in dataPaths)
            {
                var dir = Path.GetDirectoryName(path);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
            }
        }
        catch { }
    }
}
