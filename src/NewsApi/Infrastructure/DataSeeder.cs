using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Infrastructure;

/// <summary>
/// Handles initial publisher, article, video channel, social handle, and canonical topic seeding,
/// as well as startup data hygiene and SEO slug backfilling.
/// Extracted from DbInitializer (Priority 2.3) to separate schema management from seed data.
/// </summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

public class DataSeeder : IDataSeeder
{
    private readonly NewsDbContext _db;
    private readonly ILogger _logger;
    private readonly ISlugService _slugService;

    public DataSeeder(
        NewsDbContext db,
        ILogger<DataSeeder> logger,
        ISlugService? slugService = null)
        : this(db, (ILogger)logger, slugService)
    {
    }

    public DataSeeder(
        NewsDbContext db,
        ILogger logger,
        ISlugService? slugService = null)
    {
        _db = db;
        _logger = logger;
        _slugService = slugService ?? new SlugService();
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedSourcesAsync(cancellationToken);
        await EnsureWorldSourcesAsync(cancellationToken);
        await SeedArticlesAsync(cancellationToken);
        await SeedSponsoredArticlesAsync(cancellationToken);
        await SeedVideoChannelsAsync(cancellationToken);
        await SeedSocialHandlesAsync(cancellationToken);
        await SeedSocialPostsAsync(cancellationToken);
        await SeedVideoStoriesAsync(cancellationToken);
        await CleanDummyStoriesAsync(cancellationToken);
        await EnsureArticleSlugsBackfilledAsync(cancellationToken);
    }

    // ── Sources ──────────────────────────────────────────────────────────────

    private async Task SeedSourcesAsync(CancellationToken ct)
    {
        if (await _db.Sources.AnyAsync(ct))
        {
            await EnsureChannelsTvAsync(ct);
            await EnsureNairametricsAsync(ct);
            await EnsureBusinessDayAsync(ct);
            await EnsureLindaIkejiAsync(ct);
            await UpgradeGuardianToHybridAsync(ct);
            await EnsureOpinionSourcesAsync(ct);
            await EnsureCorePublishersAsync(ct);
            await CleanCorruptedArticlesAsync(ct);
            return;
        }

        var defaultSources = new[]
        {
            new Source { Id = "punch",        Name = "Punch Newspaper",       RssUrl = "https://punchng.com/feed/" },
            new Source { Id = "guardian",     Name = "The Guardian Nigeria",  SitemapUrl = "https://guardian.ng/news-sitemap.xml",          RssUrl = "https://news.google.com/rss/search?q=site:guardian.ng&hl=en-NG&gl=NG&ceid=NG:en", ScraperType = "Hybrid" },
            new Source { Id = "premiumtimes", Name = "Premium Times",         RssUrl = "https://www.premiumtimesng.com/feed" },
            new Source { Id = "channelstv",   Name = "Channels Television",   SitemapUrl = "https://www.channelstv.com/news-sitemap.xml",    RssUrl = "https://www.channelstv.com/feed/", ScraperType = "Hybrid" },
            new Source { Id = "nairametrics", Name = "Nairametrics",          SitemapUrl = "https://nairametrics.com/news-sitemap.xml",     RssUrl = "https://nairametrics.com/feed/",      ScraperType = "Hybrid" },
            new Source { Id = "businessday",  Name = "BusinessDay Nigeria",   RssUrl = "https://businessday.ng/feed/",                      ScraperType = "Rss" },
            new Source { Id = "lindaikeji",   Name = "Linda Ikeji's Blog",    RssUrl = "https://www.lindaikejisblog.com/feed",             ScraperType = "Rss" }
        };

        bool anyAdded = false;
        foreach (var source in defaultSources)
        {
            if (!await _db.Sources.AnyAsync(s => s.Id == source.Id, ct))
            {
                _db.Sources.Add(source);
                anyAdded = true;
            }
        }

        if (anyAdded)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Default news sources seeded.");
        }
    }

    private async Task EnsureChannelsTvAsync(CancellationToken ct)
    {
        if (await _db.Sources.AnyAsync(s => s.Id == "channelstv", ct)) return;
        _db.Sources.Add(new Source
        {
            Id = "channelstv", Name = "Channels Television",
            SitemapUrl = "https://www.channelstv.com/news-sitemap.xml",
            RssUrl = "https://www.channelstv.com/feed/", ScraperType = "Hybrid"
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Channels Television source added to existing database.");
    }

    private async Task EnsureNairametricsAsync(CancellationToken ct)
    {
        if (await _db.Sources.AnyAsync(s => s.Id == "nairametrics", ct)) return;
        _db.Sources.Add(new Source
        {
            Id = "nairametrics", Name = "Nairametrics",
            SitemapUrl = "https://nairametrics.com/news-sitemap.xml",
            RssUrl = "https://nairametrics.com/feed/", ScraperType = "Hybrid"
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Nairametrics source added to existing database.");
    }

    private async Task EnsureBusinessDayAsync(CancellationToken ct)
    {
        if (await _db.Sources.AnyAsync(s => s.Id == "businessday", ct)) return;
        _db.Sources.Add(new Source
        {
            Id = "businessday", Name = "BusinessDay Nigeria",
            RssUrl = "https://businessday.ng/feed/", ScraperType = "Rss"
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("BusinessDay source added to existing database.");
    }

    private async Task EnsureLindaIkejiAsync(CancellationToken ct)
    {
        if (await _db.Sources.AnyAsync(s => s.Id == "lindaikeji", ct)) return;
        _db.Sources.Add(new Source
        {
            Id = "lindaikeji", Name = "Linda Ikeji's Blog",
            RssUrl = "https://www.lindaikejisblog.com/feed", ScraperType = "Rss"
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Linda Ikeji source added to existing database.");
    }

    private async Task UpgradeGuardianToHybridAsync(CancellationToken ct)
    {
        var guardian = await _db.Sources.FindAsync(["guardian"], ct);
        if (guardian != null)
        {
            var changed = false;
            if (guardian.SitemapUrl != "https://guardian.ng/news-sitemap.xml")
            {
                guardian.SitemapUrl = "https://guardian.ng/news-sitemap.xml";
                guardian.ScraperType = "Hybrid";
                changed = true;
            }
            if (guardian.RssUrl != "https://news.google.com/rss/search?q=site:guardian.ng&hl=en-NG&gl=NG&ceid=NG:en")
            {
                guardian.RssUrl = "https://news.google.com/rss/search?q=site:guardian.ng&hl=en-NG&gl=NG&ceid=NG:en";
                changed = true;
            }
            if (changed)
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Guardian Nigeria updated to news-sitemap.xml and Google News proxy.");
            }
        }
    }

    private async Task EnsureOpinionSourcesAsync(CancellationToken ct)
    {
        var opinionSources = new (string Id, string Name, string? SitemapUrl, string RssUrl, string ScraperType)[]
        {
            ("punch_opinion", "Punch Opinions", null, "https://punchng.com/opinion/feed/", "Rss"),
            ("vanguard_opinion", "Vanguard Opinions", null, "https://www.vanguardngr.com/category/opinion/feed/", "Rss"),
            ("premiumtimes_opinion", "Premium Times Opinions", null, "https://www.premiumtimesng.com/opinion/feed", "Rss"),
            ("guardian_opinion", "The Guardian Opinions", null, "https://guardian.ng/category/opinion/feed/", "Rss"),
            ("thecable_opinion", "TheCable Opinions", null, "https://www.thecable.ng/category/opinion/feed", "Rss"),
            ("dailytrust_opinion", "Daily Trust Opinions", null, "https://dailytrust.com/opinion/feed/", "Rss"),
            ("businessday_opinion", "BusinessDay Opinions", null, "https://businessday.ng/opinion/feed/", "Rss")
        };

        bool added = false;
        foreach (var (id, name, sitemapUrl, rssUrl, scraperType) in opinionSources)
        {
            if (!await _db.Sources.AnyAsync(s => s.Id == id, ct))
            {
                _db.Sources.Add(new Source
                {
                    Id = id,
                    Name = name,
                    SitemapUrl = sitemapUrl,
                    RssUrl = rssUrl,
                    ScraperType = scraperType
                });
                added = true;
            }
        }

        if (added)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Dedicated editorial and opinion sources seeded successfully.");
        }
    }

    private async Task EnsureWorldSourcesAsync(CancellationToken ct)
    {
        var worldSources = new (string Id, string Name, string? SitemapUrl, string RssUrl, string ScraperType)[]
        {
            ("punch_world", "Punch World", null, "https://punchng.com/topics/world/feed/", "Rss"),
            ("vanguard_world", "Vanguard World", null, "https://www.vanguardngr.com/category/world/feed/", "Rss"),
            ("guardian_world", "The Guardian World", null, "https://guardian.ng/category/world/feed/", "Rss"),
            ("dailytrust_world", "Daily Trust World", null, "https://dailytrust.com/category/world-news/feed/", "Rss")
        };

        bool added = false;
        foreach (var (id, name, sitemapUrl, rssUrl, scraperType) in worldSources)
        {
            if (!await _db.Sources.AnyAsync(s => s.Id == id, ct))
            {
                _db.Sources.Add(new Source
                {
                    Id = id,
                    Name = name,
                    SitemapUrl = sitemapUrl,
                    RssUrl = rssUrl,
                    ScraperType = scraperType
                });
                added = true;
            }
        }

        if (added)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Dedicated international/world sources seeded successfully.");
        }
    }

    private async Task EnsureCorePublishersAsync(CancellationToken ct)
    {
        var coreSources = new (string Id, string Name, string? SitemapUrl, string RssUrl, string ScraperType)[]
        {
            ("thecable", "TheCable", "https://www.thecable.ng/news-sitemap.xml", "https://www.thecable.ng/feed", "Hybrid"),
            ("dailypost", "Daily Post Nigeria", "https://dailypost.ng/news-sitemap.xml", "https://dailypost.ng/feed/", "Hybrid"),
            ("vanguard", "Vanguard News", "https://www.vanguardngr.com/news-sitemap.xml", "https://www.vanguardngr.com/feed/", "Hybrid"),
            ("dailytrust", "Daily Trust", "https://dailytrust.com/news-sitemap.xml", "https://dailytrust.com/feed/", "Hybrid"),
            ("punch", "Punch Newspaper", "https://punchng.com/news-sitemap.xml", "https://punchng.com/feed/", "Hybrid"),
            ("premiumtimes", "Premium Times", "https://www.premiumtimesng.com/news-sitemap.xml", "https://www.premiumtimesng.com/feed", "Hybrid"),
        };

        bool changed = false;
        foreach (var (id, name, sitemapUrl, rssUrl, scraperType) in coreSources)
        {
            var existing = await _db.Sources.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (existing == null)
            {
                var candidateSlug = _slugService.GenerateSlug(name);
                if (await _db.Sources.AnyAsync(s => s.Slug == candidateSlug, ct))
                {
                    candidateSlug = $"{candidateSlug}-{id}";
                }

                _db.Sources.Add(new Source
                {
                    Id = id,
                    Name = name,
                    Slug = candidateSlug,
                    SitemapUrl = sitemapUrl,
                    RssUrl = rssUrl,
                    ScraperType = scraperType
                });
                changed = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(sitemapUrl) && existing.SitemapUrl != sitemapUrl)
                {
                    existing.SitemapUrl = sitemapUrl;
                    existing.ScraperType = "Hybrid";
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Core Nigerian national news publishers verified and sitemaps updated.");
        }
    }

    private async Task CleanCorruptedArticlesAsync(CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Articles\" WHERE \"Title\" = 'Before you continue' OR \"Url\" LIKE '%consent.google.com%' OR \"Title\" IS NULL OR LENGTH(TRIM(\"Title\")) < 5;",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notice during corrupted article cleanup.");
        }
    }

    // ── Articles ─────────────────────────────────────────────────────────────

    private async Task SeedArticlesAsync(CancellationToken ct)
    {
        if (await _db.Articles.AnyAsync(ct)) return;

        var sampleArticles = new[]
        {
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "Federal Government Unveils New Digital Economy Roadmap",   Slug = "fg-unveils-digital-economy-roadmap-seed01",   Excerpt = "The Ministry of Communications and Digital Economy announced a strategic initiative.", Summary = "The Ministry of Communications and Digital Economy announced a strategic initiative.", Url = "https://punchng.com/news/digital-roadmap",               Source = "Punch Newspaper",        Category = "Politics",     PublishedAt = DateTime.UtcNow },
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "Super Eagles Prepare for Upcoming International Friendly", Slug = "super-eagles-prepare-friendly-match-seed02", Excerpt = "Coaching staff confirm full squad training ahead of weekend clash.",               Summary = "Coaching staff confirm full squad training ahead of weekend clash.",               Url = "https://guardian.ng/sports/super-eagles-friendly",         Source = "The Guardian Nigeria",   Category = "Sports",       PublishedAt = DateTime.UtcNow.AddHours(-2) },
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "Central Bank Highlights Monetary Policy Outlook for Q3",  Slug = "cbn-monetary-policy-outlook-q3-seed03",      Excerpt = "Key indicators show steady stabilization across foreign exchange markets.",          Summary = "Key indicators show steady stabilization across foreign exchange markets.",          Url = "https://www.premiumtimesng.com/business/cbn-monetary-policy", Source = "Premium Times",        Category = "Business",     PublishedAt = DateTime.UtcNow.AddHours(-4) },
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "Tech Hub Ecosystem Grows Across Lagos and Abuja",         Slug = "tech-hub-ecosystem-lagos-abuja-seed04",       Excerpt = "Venture investments in Nigerian fintech startups reach record highs this quarter.",  Summary = "Venture investments in Nigerian fintech startups reach record highs this quarter.",  Url = "https://punchng.com/tech/startup-growth",                  Source = "Punch Newspaper",        Category = "Technology",   PublishedAt = DateTime.UtcNow.AddHours(-6) },
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "United Nations Security Council Convenes Summit on Global Diplomatic Treaties", Slug = "un-security-council-global-summit-seed05", Excerpt = "World leaders and international envoys assemble at UN headquarters to negotiate multilateral regional agreements.", Summary = "World leaders and international envoys assemble at UN headquarters to negotiate multilateral regional agreements.", Url = "https://guardian.ng/news/un-global-security-summit", Source = "The Guardian Nigeria", Category = "International", PublishedAt = DateTime.UtcNow.AddHours(-8) }
        };

        bool anyAdded = false;
        foreach (var article in sampleArticles)
        {
            if (!await _db.Articles.AnyAsync(a => a.Slug == article.Slug, ct))
            {
                _db.Articles.Add(article);
                anyAdded = true;
            }
        }

        if (anyAdded)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Default sample articles seeded.");
        }
    }

    // ── Sponsored Articles (Direct Monetization) ──────────────────────────────

    private async Task SeedSponsoredArticlesAsync(CancellationToken ct)
    {
        if (await _db.Articles.AnyAsync(a => a.IsSponsored, ct)) return;

        _db.Articles.Add(new Article
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "Flutterwave Launches ₦10 Billion SME Growth & Expansion Fund",
            Slug = "flutterwave-sme-growth-fund-seed06",
            Excerpt = "Africa's leading payments technology powerhouse unveils a dedicated financing initiative and zero-fee digital settlement tools.",
            Summary = "Africa's leading payments technology powerhouse unveils a dedicated financing initiative and zero-fee digital settlement tools for registered Nigerian merchants.",
            Content = "Flutterwave, Africa's leading payments technology company, today announced a landmark ₦10 Billion SME Growth and Financing Initiative aimed at accelerating digital commerce across Nigeria. The program provides participating merchants with instant working capital advances, zero settlement fees during the promotional period, and integrated omni-channel point-of-sale capabilities.",
            Url = "https://flutterwave.com/ng",
            ImageUrl = "https://images.unsplash.com/photo-1559526324-4b87b5e36e44?w=800&auto=format&fit=crop&q=80",
            Source = "Flutterwave Press Release",
            Category = "Business",
            PublishedAt = DateTime.UtcNow.AddHours(-1),
            IsSponsored = true,
            SponsorName = "Flutterwave",
            SponsorUrl = "https://flutterwave.com/ng",
            IsPinned = true,
            CampaignExpiresAt = DateTime.UtcNow.AddDays(30),
            PlacementZone = "MainPage_InFeed",
            TargetPlatform = "All",
            ImpressionCount = 1420,
            ClickCount = 118
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Default sample sponsored campaign seeded.");
    }

    // ── Video Channels ────────────────────────────────────────────────────────

    private static readonly List<VideoChannel> DefaultChannels =
    [
        new() { Id = "channels_tv",  ChannelName = "Channels Television",  YoutubeChannelId = "UCEXGDNclvmg6RW0vipJYsTQ", ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_kHFuZvRnMoA-7n9WI7m0iNPHQ5qcBfVbz_rXKdJhJfpQ=s176-c-k-c0x00ffffff-no-rj", ChannelUrl = "https://www.youtube.com/@ChannelsTelevision",  Description = "Nigeria's most-watched 24-hour television station." },
        new() { Id = "tvc_news",     ChannelName = "TVC News Nigeria",      YoutubeChannelId = "UCgp4A6I8LCWrhUzn-5SbKvA", ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_mK7KBa_FdQ7BnEP8FcqMPZ5V_eLkUoY8ygTwFN5w=s176-c-k-c0x00ffffff-no-rj", ChannelUrl = "https://www.youtube.com/@TVCNewsNigeria",       Description = "TVC News is a 24-hour news channel from Lagos." },
        new() { Id = "arise_news",   ChannelName = "Arise News",            YoutubeChannelId = "UCyEJX-kSj0kOOCS7Qlq2G7g", ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_l5PqyU3YjZ-b5D5j5GWMKG7LpGLoYtCR5W5Qva5c8=s176-c-k-c0x00ffffff-no-rj", ChannelUrl = "https://www.youtube.com/@AriseNewsChannel",     Description = "Global broadcast news network focused on Nigeria." },
        new() { Id = "the_cable",    ChannelName = "TheCable",              YoutubeChannelId = "UC8jyD9yXYdDFiu3W77JeZlQ", ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_m_mFmJkK7C_VkH5J_5e4xj5HhE5xQ5QQQsT5Q=s176-c-k-c0x00ffffff-no-rj", ChannelUrl = "https://www.youtube.com/@thecableng",           Description = "Independent Nigerian online journalism." },
        new() { Id = "sahara_tv",    ChannelName = "SaharaTV",              YoutubeChannelId = "UCKnyVIW5QvfnsXddsjFKx4A", ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_mK7KBa_FdQ7BnEP8FcqMPZ5V_eLkUoY8ygTwFN5w=s176-c-k-c0x00ffffff-no-rj", ChannelUrl = "https://www.youtube.com/@SaharaTV",             Description = "Exclusive interviews and investigative reports." },
        new() { Id = "nta_network",  ChannelName = "NTA Network",           YoutubeChannelId = "UC6boj-dEymV7fjn6gAIvMLg", ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_m_mFmJkK7C_VkH5J_5e4xj5HhE5xQ5QQQsT5Q=s176-c-k-c0x00ffffff-no-rj", ChannelUrl = "https://www.youtube.com/@NTANetwork",           Description = "Nigerian Television Authority — national broadcaster." },
    ];

    private async Task SeedVideoChannelsAsync(CancellationToken ct)
    {
        if (!await _db.VideoChannels.AnyAsync(ct))
        {
            _db.VideoChannels.AddRange(DefaultChannels);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Default video channels seeded.");
            return;
        }

        var existing = await _db.VideoChannels.ToListAsync(ct);
        bool changed = false;
        foreach (var def in DefaultChannels)
        {
            var match = existing.FirstOrDefault(c => c.Id == def.Id || c.ChannelName == def.ChannelName);
            if (match != null)
            {
                match.YoutubeChannelId = def.YoutubeChannelId;
                match.ChannelUrl       = def.ChannelUrl;
                changed = true;
            }
            else
            {
                _db.VideoChannels.Add(def);
                changed = true;
            }
        }
        if (changed) await _db.SaveChangesAsync(ct);
    }

    // ── Social Handles ────────────────────────────────────────────────────────

    private async Task SeedSocialHandlesAsync(CancellationToken ct)
    {
        if (await _db.SocialHandles.AnyAsync(ct)) return;

        _db.SocialHandles.AddRange(
            new SocialHandle { Id = "channels_tv_x",   Handle = "@channelstv",      DisplayName = "Channels Television",  ProfileUrl = "https://x.com/channelstv",      AvatarUrl = "https://pbs.twimg.com/profile_images/1683847522/channels_tv_400x400.jpg", Bio = "Nigeria's No.1 News Station.",                   Category = "Media" },
            new SocialHandle { Id = "premium_times_x", Handle = "@PremiumTimesng",  DisplayName = "Premium Times",        ProfileUrl = "https://x.com/PremiumTimesng",  AvatarUrl = "https://pbs.twimg.com/profile_images/premium_times_400x400.jpg",          Bio = "Award-winning investigative journalism.",         Category = "Media" },
            new SocialHandle { Id = "punch_x",         Handle = "@MobilePunch",     DisplayName = "Punch Newspapers",     ProfileUrl = "https://x.com/MobilePunch",     AvatarUrl = "https://pbs.twimg.com/profile_images/punch_400x400.jpg",                  Bio = "Most widely read newspaper in Nigeria.",          Category = "Media" },
            new SocialHandle { Id = "guardian_x",      Handle = "@GuardianNigeria", DisplayName = "The Guardian Nigeria", ProfileUrl = "https://x.com/GuardianNigeria", AvatarUrl = "https://pbs.twimg.com/profile_images/guardian_ng_400x400.jpg",            Bio = "Conscience, nurtured by truth.",                  Category = "Media" },
            new SocialHandle { Id = "vanguard_x",      Handle = "@vanguardngrnews", DisplayName = "Vanguard Newspapers",  ProfileUrl = "https://x.com/vanguardngrnews", AvatarUrl = "https://pbs.twimg.com/profile_images/vanguard_400x400.jpg",               Bio = "Towards a better life for the people.",           Category = "Media" },
            new SocialHandle { Id = "arise_x",         Handle = "@ARISETV",         DisplayName = "Arise Television",     ProfileUrl = "https://x.com/ARISETV",         AvatarUrl = "https://pbs.twimg.com/profile_images/arise_tv_400x400.jpg",               Bio = "Global broadcast news network.",                  Category = "Media" },
            new SocialHandle { Id = "omokri_x",        Handle = "@renoomokri",      DisplayName = "Reno Omokri",          ProfileUrl = "https://x.com/renoomokri",      AvatarUrl = "https://pbs.twimg.com/profile_images/reno_omokri_400x400.jpg",            Bio = "Bestselling author and political commentator.",   Category = "Commentator" },
            new SocialHandle { Id = "efcc_x",          Handle = "@officialEFCC",    DisplayName = "EFCC Nigeria",         ProfileUrl = "https://x.com/officialEFCC",    AvatarUrl = "https://pbs.twimg.com/profile_images/efcc_400x400.jpg",                   Bio = "Official Twitter account of EFCC.",               Category = "Government" },
            new SocialHandle { Id = "atiku_x",         Handle = "@atiku",           DisplayName = "Atiku Abubakar",       ProfileUrl = "https://x.com/atiku",           AvatarUrl = "https://pbs.twimg.com/profile_images/atiku_400x400.jpg",                  Bio = "Former Vice President of Nigeria.",               Category = "Commentator" }
        );
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Default social handles seeded.");
    }

    // ── Social Posts ──────────────────────────────────────────────────────────

    private async Task SeedSocialPostsAsync(CancellationToken ct)
    {
        if (await _db.SocialPosts.AnyAsync(ct)) return;

        _db.SocialPosts.AddRange(
            new SocialPost { Id = Guid.NewGuid().ToString("N"), AuthorName = "Premium Times",    AuthorHandle = "@PremiumTimesng", AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/premium_times_400x400.jpg",          Content = "BREAKING: Federal Executive Council approves new national infrastructure development fund.", PostUrl = "https://x.com/PremiumTimesng", MediaUrl = "https://images.unsplash.com/photo-1541888946425-d0fbb18086f6?w=600&auto=format&fit=crop&q=80", Category = "Politics",   LikesCount = 2450, RetweetsCount = 890,  PublishedAt = DateTime.UtcNow.AddMinutes(-35) },
            new SocialPost { Id = Guid.NewGuid().ToString("N"), AuthorName = "Channels TV",      AuthorHandle = "@channelstv",     AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/1683847522/channels_tv_400x400.jpg", Content = "JUST IN: Central Bank reiterates foreign exchange stability guidelines.",               PostUrl = "https://x.com/channelstv",     Category = "Business",   LikesCount = 1820, RetweetsCount = 540,  PublishedAt = DateTime.UtcNow.AddHours(-1) },
            new SocialPost { Id = Guid.NewGuid().ToString("N"), AuthorName = "Punch Newspapers", AuthorHandle = "@MobilePunch",    AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/punch_400x400.jpg",                  Content = "SPORTS: Nigeria Football Federation announces final roster ahead of international tournament.", PostUrl = "https://x.com/MobilePunch",  MediaUrl = "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80", Category = "Sports",     LikesCount = 3890, RetweetsCount = 1220, PublishedAt = DateTime.UtcNow.AddHours(-2) },
            new SocialPost { Id = Guid.NewGuid().ToString("N"), AuthorName = "EFCC Nigeria",     AuthorHandle = "@officialEFCC",   AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/efcc_400x400.jpg",                   Content = "PRESS RELEASE: EFCC recovers over ₦4.2 Billion in fraudulent real estate scheme.",         PostUrl = "https://x.com/officialEFCC",   Category = "Government", LikesCount = 5120, RetweetsCount = 1940, PublishedAt = DateTime.UtcNow.AddHours(-4) }
        );
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Default social posts seeded.");
    }

    // ── Video Stories ─────────────────────────────────────────────────────────

    private async Task SeedVideoStoriesAsync(CancellationToken ct)
    {
        if (await _db.VideoStories.AnyAsync(ct)) return;

        _db.VideoStories.AddRange(
            new VideoStory { Id = Guid.NewGuid().ToString("N"), VideoId = "r8FW2pqYrq4", Title = "WCQ Play-Off: 'It's A Do Or Die Match', South Africa Coach On Nigeria Clash", Summary = "South Africa coach speaks ahead of the crucial world cup qualifying match.",        VideoUrl = "https://www.youtube.com/watch?v=r8FW2pqYrq4", ThumbnailUrl = "https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg", ChannelName = "Channels Television", ChannelId = "channels_tv", Duration = "LIVE / RECENT", Category = "Sports",   PublishedAt = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow },
            new VideoStory { Id = Guid.NewGuid().ToString("N"), VideoId = "2nrElTSJE58", Title = "TVC News Headline News: National Economy, Markets & Policy Review",           Summary = "Comprehensive coverage and analysis of fiscal reforms and governance developments.", VideoUrl = "https://www.youtube.com/watch?v=2nrElTSJE58", ThumbnailUrl = "https://i.ytimg.com/vi/2nrElTSJE58/hqdefault.jpg", ChannelName = "TVC News Nigeria",    ChannelId = "tvc_news",    Duration = "12:10",         Category = "News",     PublishedAt = DateTime.UtcNow.AddHours(-3), CreatedAt = DateTime.UtcNow },
            new VideoStory { Id = Guid.NewGuid().ToString("N"), VideoId = "hmjB9EQ66V8", Title = "TheCable Special Feature: Investigative Spotlight and Governance Insights",  Summary = "In-depth investigative documentary exploring key policy implementation.",           VideoUrl = "https://www.youtube.com/watch?v=hmjB9EQ66V8", ThumbnailUrl = "https://i.ytimg.com/vi/hmjB9EQ66V8/hqdefault.jpg", ChannelName = "TheCable",            ChannelId = "the_cable",   Duration = "09:30",         Category = "Politics", PublishedAt = DateTime.UtcNow.AddHours(-5), CreatedAt = DateTime.UtcNow },
            new VideoStory { Id = Guid.NewGuid().ToString("N"), VideoId = "7LA4EfXNXzc", Title = "SaharaTV Report: National Assembly and Legal Developments",                  Summary = "Coverage of recent legislative proceedings and civic accountability discussions.",    VideoUrl = "https://www.youtube.com/watch?v=7LA4EfXNXzc", ThumbnailUrl = "https://i.ytimg.com/vi/7LA4EfXNXzc/hqdefault.jpg", ChannelName = "SaharaTV",            ChannelId = "sahara_tv",   Duration = "15:20",         Category = "Politics", PublishedAt = DateTime.UtcNow.AddHours(-7), CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Default video stories seeded.");
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private async Task CleanDummyStoriesAsync(CancellationToken ct)
    {
        var dummies = await _db.VideoStories
            .Where(v => v.VideoUrl.Contains("dQw4w9WgXcQ") || v.VideoId.Contains("dQw4w9WgXcQ"))
            .ToListAsync(ct);

        if (dummies.Count > 0)
        {
            _db.VideoStories.RemoveRange(dummies);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Removed {Count} dummy placeholder video stories.", dummies.Count);
        }
    }

    // ── SEO Slugs & Topics Backfill ───────────────────────────────────────────

    private async Task EnsureArticleSlugsBackfilledAsync(CancellationToken ct)
    {
        try
        {
            var sources = await _db.Sources.Where(s => string.IsNullOrEmpty(s.Slug)).ToListAsync(ct);
            if (sources.Count > 0)
            {
                foreach (var s in sources)
                {
                    s.Slug = _slugService.GenerateSlug(string.IsNullOrWhiteSpace(s.Name) ? s.Id : s.Name, 60);
                }
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Backfilled SEO slugs for {Count} sources.", sources.Count);
            }

            await SeedDefaultTopicsAsync(ct);

            var articlesNeedingSeo = await _db.Articles
                .Where(a => string.IsNullOrEmpty(a.Slug) || string.IsNullOrEmpty(a.Excerpt))
                .Take(500)
                .ToListAsync(ct);

            if (articlesNeedingSeo.Count > 0)
            {
                var existingSlugs = await _db.Articles
                    .Where(a => !string.IsNullOrEmpty(a.Slug))
                    .Select(a => a.Slug!)
                    .ToListAsync(ct);
                var usedSlugs = new HashSet<string>(existingSlugs, StringComparer.OrdinalIgnoreCase);

                foreach (var a in articlesNeedingSeo)
                {
                    if (string.IsNullOrWhiteSpace(a.Slug))
                    {
                        var slug = _slugService.GenerateArticleSlug(a.Title, a.Id);
                        int attempts = 0;
                        while (usedSlugs.Contains(slug))
                        {
                            slug = _slugService.GenerateArticleSlug(a.Title, null);
                            attempts++;
                            if (attempts > 5)
                            {
                                slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(80, slug.Length + 8)];
                                break;
                            }
                        }
                        a.Slug = slug;
                        usedSlugs.Add(slug);
                    }

                    if (string.IsNullOrWhiteSpace(a.Excerpt))
                    {
                        a.Excerpt = _slugService.GenerateExcerpt(a.Content, a.Summary, 300);
                    }
                    a.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Backfilled SEO slugs & excerpts for {Count} initial articles.", articlesNeedingSeo.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automatic SEO backfill during initialization encountered a non-fatal warning; backfill service remains available via API.");
        }
    }

    private async Task SeedDefaultTopicsAsync(CancellationToken ct)
    {
        if (await _db.Topics.AnyAsync(ct)) return;

        var defaultTopics = new List<Topic>
        {
            new() { Id = "topic_tinubu",   Slug = "bola-tinubu",         Name = "Bola Ahmed Tinubu",       Type = TopicType.Person,       Description = "President of the Federal Republic of Nigeria." },
            new() { Id = "topic_shettima", Slug = "kashim-shettima",     Name = "Kashim Shettima",         Type = TopicType.Person,       Description = "Vice President of the Federal Republic of Nigeria." },
            new() { Id = "topic_cardoso",  Slug = "yemi-cardoso",        Name = "Olayemi Cardoso",         Type = TopicType.Person,       Description = "Governor of the Central Bank of Nigeria (CBN)." },
            new() { Id = "topic_dangote",  Slug = "aliko-dangote",       Name = "Aliko Dangote",           Type = TopicType.Person,       Description = "Nigerian industrialist and founder of Dangote Group." },

            new() { Id = "topic_cbn",      Slug = "cbn",                 Name = "Central Bank of Nigeria", Type = TopicType.Organization, Description = "The apex monetary authority of Nigeria." },
            new() { Id = "topic_nnpc",     Slug = "nnpc",                Name = "NNPC Limited",            Type = TopicType.Organization, Description = "National oil company and primary energy supplier." },
            new() { Id = "topic_efcc",     Slug = "efcc",                Name = "EFCC",                    Type = TopicType.Organization, Description = "Economic and Financial Crimes Commission." },
            new() { Id = "topic_eagles",   Slug = "super-eagles",        Name = "Super Eagles",            Type = TopicType.Organization, Description = "Nigeria national men's football team." },

            new() { Id = "topic_lagos",    Slug = "lagos",               Name = "Lagos State",             Type = TopicType.Place,        Description = "Commercial nerve centre and economic capital of Nigeria." },
            new() { Id = "topic_abuja",    Slug = "abuja",               Name = "Abuja FCT",               Type = TopicType.Place,        Description = "Federal Capital Territory and political seat of Nigeria." },
            new() { Id = "topic_kano",     Slug = "kano",                Name = "Kano State",              Type = TopicType.Place,        Description = "Major northern commercial and historic metropolis." },
            new() { Id = "topic_rivers",   Slug = "rivers",              Name = "Rivers State",            Type = TopicType.Place,        Description = "Niger Delta industrial and hydrocarbon hub." },

            new() { Id = "topic_fx",       Slug = "naira-exchange-rate", Name = "Naira Exchange Rate",     Type = TopicType.Theme,        Description = "Real-time updates on official and parallel market foreign exchange rates." },
            new() { Id = "topic_fuel",     Slug = "fuel-prices",         Name = "Fuel & Energy Prices",    Type = TopicType.Theme,        Description = "Petroleum pump price monitoring, refinery output, and energy policy." },
            new() { Id = "topic_tech",     Slug = "fintech-startups",    Name = "Fintech & Startups",      Type = TopicType.Theme,        Description = "Venture investments, digital banking, and innovation ecosystem across Africa." }
        };

        _db.Topics.AddRange(defaultTopics);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} canonical topics.", defaultTopics.Count);
    }
}
