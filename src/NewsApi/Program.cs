using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Health;
using NewsApi.Middleware;
using NewsApi.Models;

using NewsApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddScoped<YouTubeFeedService>();
builder.Services.AddScoped<SocialFeedService>();
builder.Services.AddScoped<RelatedContentService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddHostedService<BackgroundMediaSyncService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("db_health_check");

// CORS policy for Admin Dashboard & Client apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure DbContext (Support PostgreSQL and SQLite)
var connectionString = builder.Configuration.GetConnectionString("NewsDb") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=news.db";

builder.Services.AddDbContext<NewsDbContext>(options =>
{
    if (connectionString.Contains("Host=") || connectionString.Contains("Server=") || connectionString.Contains("Username=") || connectionString.Contains("Postgres"))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

// Production Health Check Endpoints
app.MapHealthChecks("/healthz/liveness", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/healthz/readiness", new HealthCheckOptions
{
    Predicate = check => check.Name == "db_health_check"
});

app.MapHealthChecks("/health");

// Ensure DB schema and default seed data safely
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NewsDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        db.Database.EnsureCreated();

        // Ensure all tables exist in SQLite if DB file was previously created with partial schema
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""VideoChannels"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_VideoChannels"" PRIMARY KEY,
                    ""ChannelName"" TEXT NOT NULL,
                    ""YoutubeChannelId"" TEXT NOT NULL,
                    ""ThumbnailUrl"" TEXT NOT NULL,
                    ""ChannelUrl"" TEXT NOT NULL,
                    ""Description"" TEXT NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL
                );
            ");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""VideoStories"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_VideoStories"" PRIMARY KEY,
                    ""VideoId"" TEXT NOT NULL,
                    ""Title"" TEXT NOT NULL,
                    ""Summary"" TEXT NOT NULL,
                    ""VideoUrl"" TEXT NOT NULL,
                    ""ThumbnailUrl"" TEXT NOT NULL,
                    ""ChannelName"" TEXT NOT NULL,
                    ""ChannelId"" TEXT NOT NULL,
                    ""Duration"" TEXT NOT NULL,
                    ""Category"" TEXT NOT NULL,
                    ""PublishedAt"" TEXT NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""IsTrending"" INTEGER NOT NULL DEFAULT 0,
                    ""TrendingRank"" INTEGER NULL,
                    ""ViewCount"" INTEGER NOT NULL DEFAULT 0,
                    ""LikeCount"" INTEGER NOT NULL DEFAULT 0
                );
            ");
        }
        catch { }

        // Schema migrations for pre-existing SQLite database files
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""VideoStories"" ADD COLUMN ""IsTrending"" INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""VideoStories"" ADD COLUMN ""TrendingRank"" INTEGER NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""VideoStories"" ADD COLUMN ""ViewCount"" INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""VideoStories"" ADD COLUMN ""LikeCount"" INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_VideoStories_PublishedAt"" ON ""VideoStories"" (""PublishedAt"");"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_VideoStories_VideoId"" ON ""VideoStories"" (""VideoId"");"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_VideoStories_ChannelId"" ON ""VideoStories"" (""ChannelId"");"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_VideoStories_IsTrending"" ON ""VideoStories"" (""IsTrending"");"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_VideoStories_TrendingRank"" ON ""VideoStories"" (""TrendingRank"");"); } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""SocialHandles"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_SocialHandles"" PRIMARY KEY,
                    ""Handle"" TEXT NOT NULL,
                    ""DisplayName"" TEXT NOT NULL,
                    ""ProfileUrl"" TEXT NOT NULL,
                    ""AvatarUrl"" TEXT NOT NULL,
                    ""Bio"" TEXT NOT NULL,
                    ""Category"" TEXT NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL
                );
            ");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""SocialPosts"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_SocialPosts"" PRIMARY KEY,
                    ""AuthorName"" TEXT NOT NULL,
                    ""AuthorHandle"" TEXT NOT NULL,
                    ""AuthorAvatarUrl"" TEXT NOT NULL,
                    ""Content"" TEXT NOT NULL,
                    ""PostUrl"" TEXT NOT NULL,
                    ""MediaUrl"" TEXT NULL,
                    ""Category"" TEXT NOT NULL,
                    ""LikesCount"" INTEGER NOT NULL,
                    ""RetweetsCount"" INTEGER NOT NULL,
                    ""PublishedAt"" TEXT NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL
                );
            ");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_SocialPosts_PublishedAt"" ON ""SocialPosts"" (""PublishedAt"");");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_SocialPosts_AuthorHandle"" ON ""SocialPosts"" (""AuthorHandle"");");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""Feedbacks"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Feedbacks"" PRIMARY KEY,
                    ""Rating"" INTEGER NOT NULL,
                    ""Category"" TEXT NOT NULL,
                    ""Message"" TEXT NOT NULL,
                    ""UserEmail"" TEXT NULL,
                    ""UserName"" TEXT NULL,
                    ""AppVersion"" TEXT NULL,
                    ""Platform"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""IsEmailSent"" INTEGER NOT NULL DEFAULT 0
                );
            ");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Feedbacks_CreatedAt"" ON ""Feedbacks"" (""CreatedAt"");");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Feedbacks_Category"" ON ""Feedbacks"" (""Category"");");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""CategoryCorrections"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_CategoryCorrections"" PRIMARY KEY,
                    ""ArticleId"" TEXT NOT NULL,
                    ""Title"" TEXT NOT NULL,
                    ""Summary"" TEXT NULL,
                    ""OldCategory"" TEXT NOT NULL,
                    ""NewCategory"" TEXT NOT NULL,
                    ""Source"" TEXT NULL,
                    ""Url"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL
                );
            ");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_CategoryCorrections_CreatedAt"" ON ""CategoryCorrections"" (""CreatedAt"");");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_CategoryCorrections_ArticleId"" ON ""CategoryCorrections"" (""ArticleId"");");
        }
        catch { }

        // Analytics: User behaviour events (app_open, article_read, article_share, article_bookmark)
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""UserEvents"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_UserEvents"" PRIMARY KEY,
                    ""DeviceId"" TEXT NOT NULL,
                    ""EventType"" TEXT NOT NULL,
                    ""ArticleId"" TEXT NULL,
                    ""ArticleTitle"" TEXT NULL,
                    ""Category"" TEXT NULL,
                    ""Platform"" TEXT NULL,
                    ""AppVersion"" TEXT NULL,
                    ""OccurredAt"" TEXT NOT NULL,
                    ""ReceivedAt"" TEXT NOT NULL
                );
            ");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_UserEvents_OccurredAt"" ON ""UserEvents"" (""OccurredAt"");");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_UserEvents_EventType"" ON ""UserEvents"" (""EventType"");");
            db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_UserEvents_DeviceId"" ON ""UserEvents"" (""DeviceId"");");
        }
        catch { }

        logger.LogInformation("Database verified and schema initialized.");

        // Seed initial sources if empty
        if (!db.Sources.Any())
        {
            db.Sources.AddRange(
                new Source { Id = "punch", Name = "Punch Newspaper", RssUrl = "https://punchng.com/feed/" },
                new Source { Id = "guardian", Name = "The Guardian Nigeria", SitemapUrl = "https://guardian.ng/sitemap.xml", RssUrl = "https://guardian.ng/feed/", ScraperType = "Hybrid" },
                new Source { Id = "premiumtimes", Name = "Premium Times", RssUrl = "https://www.premiumtimesng.com/feed" },
                new Source { Id = "nairametrics", Name = "Nairametrics", SitemapUrl = "https://nairametrics.com/news-sitemap.xml", RssUrl = "https://nairametrics.com/feed/", ScraperType = "Hybrid" }
            );
            db.SaveChanges();
            logger.LogInformation("Default news sources seeded.");
        }
        else if (!db.Sources.Any(s => s.Id == "nairametrics"))
        {
            // Upsert Nairametrics into existing databases
            db.Sources.Add(new Source
            {
                Id = "nairametrics",
                Name = "Nairametrics",
                SitemapUrl = "https://nairametrics.com/news-sitemap.xml",
                RssUrl = "https://nairametrics.com/feed/",
                ScraperType = "Hybrid"
            });
            db.SaveChanges();
            logger.LogInformation("Nairametrics source added to existing database.");
        }

        // Upgrade guardian from RSS-only to hybrid sitemap scraper for existing databases
        var guardianSource = db.Sources.Find("guardian");
        if (guardianSource != null && string.IsNullOrEmpty(guardianSource.SitemapUrl))
        {
            guardianSource.SitemapUrl = "https://guardian.ng/sitemap.xml";
            guardianSource.ScraperType = "Hybrid";
            db.SaveChanges();
            logger.LogInformation("Guardian Nigeria source upgraded to hybrid sitemap scraper.");
        }

        // Seed initial articles if empty
        if (!db.Articles.Any())
        {
            db.Articles.AddRange(
                new Article
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = "Federal Government Unveils New Digital Economy Roadmap",
                    Summary = "The Ministry of Communications and Digital Economy announced a strategic initiative targeting infrastructure expansion and youth tech skills.",
                    Content = "Full article content covering national digital infrastructure investments.",
                    Url = "https://punchng.com/news/digital-roadmap",
                    Source = "Punch Newspaper",
                    Category = "Politics",
                    PublishedAt = DateTime.UtcNow
                },
                new Article
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = "Super Eagles Prepare for Upcoming International Friendly",
                    Summary = "Coaching staff confirm full squad training ahead of weekend clash with key tactical adjustments.",
                    Content = "Details on player fitness, team morale, and match preparations.",
                    Url = "https://guardian.ng/sports/super-eagles-friendly",
                    Source = "The Guardian Nigeria",
                    Category = "Sports",
                    PublishedAt = DateTime.UtcNow.AddHours(-2)
                },
                new Article
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = "Central Bank Highlights Monetary Policy Outlook for Q3",
                    Summary = "Key indicators show steady stabilization across foreign exchange markets and trade balances.",
                    Content = "Comprehensive report on inflation targets and market liquidity.",
                    Url = "https://www.premiumtimesng.com/business/cbn-monetary-policy",
                    Source = "Premium Times",
                    Category = "Business",
                    PublishedAt = DateTime.UtcNow.AddHours(-4)
                },
                new Article
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = "Tech Hub Ecosystem Grows Across Lagos and Abuja",
                    Summary = "Venture investments in Nigerian fintech startups reach record highs this quarter.",
                    Content = "In-depth analysis of innovation hubs and venture funding trends.",
                    Url = "https://punchng.com/tech/startup-growth",
                    Source = "Punch Newspaper",
                    Category = "Technology",
                    PublishedAt = DateTime.UtcNow.AddHours(-6)
                }
            );
            db.SaveChanges();
            logger.LogInformation("Default sample articles seeded.");
        }

        // Seed or update initial video channels
        var defaultChannels = new List<VideoChannel>
        {
            new VideoChannel
            {
                Id = "channels_tv",
                ChannelName = "Channels Television",
                YoutubeChannelId = "UCEXGDNclvmg6RW0vipJYsTQ",
                ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_kHFuZvRnMoA-7n9WI7m0iNPHQ5qcBfVbz_rXKdJhJfpQ=s176-c-k-c0x00ffffff-no-rj",
                ChannelUrl = "https://www.youtube.com/@ChannelsTelevision",
                Description = "Nigeria's most-watched 24-hour television station delivering breaking news, politics, business, and entertainment."
            },
            new VideoChannel
            {
                Id = "tvc_news",
                ChannelName = "TVC News Nigeria",
                YoutubeChannelId = "UCgp4A6I8LCWrhUzn-5SbKvA",
                ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_mK7KBa_FdQ7BnEP8FcqMPZ5V_eLkUoY8ygTwFN5w=s176-c-k-c0x00ffffff-no-rj",
                ChannelUrl = "https://www.youtube.com/@TVCNewsNigeria",
                Description = "TVC News is a 24-hour news channel broadcasting live from Lagos, covering Nigerian and African news."
            },
            new VideoChannel
            {
                Id = "arise_news",
                ChannelName = "Arise News",
                YoutubeChannelId = "UCyEJX-kSj0kOOCS7Qlq2G7g",
                ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_l5PqyU3YjZ-b5D5j5GWMKG7LpGLoYtCR5W5Qva5c8=s176-c-k-c0x00ffffff-no-rj",
                ChannelUrl = "https://www.youtube.com/@AriseNewsChannel",
                Description = "Arise News is a global broadcast news network focused on Nigeria, Africa, and the diaspora."
            },
            new VideoChannel
            {
                Id = "the_cable",
                ChannelName = "TheCable",
                YoutubeChannelId = "UC8jyD9yXYdDFiu3W77JeZlQ",
                ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_m_mFmJkK7C_VkH5J_5e4xj5HhE5xQ5QQQsT5Q=s176-c-k-c0x00ffffff-no-rj",
                ChannelUrl = "https://www.youtube.com/@thecableng",
                Description = "TheCable delivers independent Nigerian online journalism and video broadcasts."
            },
            new VideoChannel
            {
                Id = "sahara_tv",
                ChannelName = "SaharaTV",
                YoutubeChannelId = "UCKnyVIW5QvfnsXddsjFKx4A",
                ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_mK7KBa_FdQ7BnEP8FcqMPZ5V_eLkUoY8ygTwFN5w=s176-c-k-c0x00ffffff-no-rj",
                ChannelUrl = "https://www.youtube.com/@SaharaTV",
                Description = "SaharaTV brings you exclusive interviews, investigative reports, and in-depth political discourse."
            },
            new VideoChannel
            {
                Id = "nta_network",
                ChannelName = "NTA Network",
                YoutubeChannelId = "UC6boj-dEymV7fjn6gAIvMLg",
                ThumbnailUrl = "https://yt3.googleusercontent.com/ytc/AIdro_m_mFmJkK7C_VkH5J_5e4xj5HhE5xQ5QQQsT5Q=s176-c-k-c0x00ffffff-no-rj",
                ChannelUrl = "https://www.youtube.com/@NTANetwork",
                Description = "Nigerian Television Authority — the national public broadcaster delivering news and current affairs."
            }
        };

        if (!db.VideoChannels.Any())
        {
            db.VideoChannels.AddRange(defaultChannels);
            db.SaveChanges();
            logger.LogInformation("Default video channels seeded.");
        }
        else
        {
            // Sync/update any existing channels that had legacy invalid IDs
            var existingChannels = db.VideoChannels.ToList();
            foreach (var def in defaultChannels)
            {
                var match = existingChannels.FirstOrDefault(c => c.Id == def.Id || c.ChannelName == def.ChannelName);
                if (match != null)
                {
                    match.YoutubeChannelId = def.YoutubeChannelId;
                    match.ChannelUrl = def.ChannelUrl;
                }
                else
                {
                    db.VideoChannels.Add(def);
                }
            }
            db.SaveChanges();
        }

        // Clean up any legacy dummy placeholder video stories (e.g. Rickroll dQw4w9WgXcQ)
        var dummyStories = db.VideoStories
            .Where(v => v.VideoUrl.Contains("dQw4w9WgXcQ") || v.VideoId.Contains("dQw4w9WgXcQ"))
            .ToList();
        if (dummyStories.Count > 0)
        {
            db.VideoStories.RemoveRange(dummyStories);
            db.SaveChanges();
            logger.LogInformation("Cleaned up {Count} dummy placeholder video stories.", dummyStories.Count);
        }

        // Seed initial social handles if empty
        if (!db.SocialHandles.Any())
        {
            db.SocialHandles.AddRange(
                // Media outlets
                new SocialHandle
                {
                    Id = "channels_tv_x",
                    Handle = "@channelstv",
                    DisplayName = "Channels Television",
                    ProfileUrl = "https://x.com/channelstv",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/1683847522/channels_tv_400x400.jpg",
                    Bio = "Nigeria's No.1 News Station. Breaking news, live updates, and in-depth coverage.",
                    Category = "Media"
                },
                new SocialHandle
                {
                    Id = "premium_times_x",
                    Handle = "@PremiumTimesng",
                    DisplayName = "Premium Times",
                    ProfileUrl = "https://x.com/PremiumTimesng",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/premium_times_400x400.jpg",
                    Bio = "Award-winning investigative journalism. Delivering credible Nigerian news.",
                    Category = "Media"
                },
                new SocialHandle
                {
                    Id = "punch_x",
                    Handle = "@MobilePunch",
                    DisplayName = "Punch Newspapers",
                    ProfileUrl = "https://x.com/MobilePunch",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/punch_400x400.jpg",
                    Bio = "Most widely read newspaper in Nigeria. News, analysis, and features.",
                    Category = "Media"
                },
                new SocialHandle
                {
                    Id = "guardian_x",
                    Handle = "@GuardianNigeria",
                    DisplayName = "The Guardian Nigeria",
                    ProfileUrl = "https://x.com/GuardianNigeria",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/guardian_ng_400x400.jpg",
                    Bio = "Flagship of the Nigerian press. Conscience, nurtured by truth.",
                    Category = "Media"
                },
                new SocialHandle
                {
                    Id = "vanguard_x",
                    Handle = "@vanguardngrnews",
                    DisplayName = "Vanguard Newspapers",
                    ProfileUrl = "https://x.com/vanguardngrnews",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/vanguard_400x400.jpg",
                    Bio = "Towards a better life for the people. Breaking Nigerian news daily.",
                    Category = "Media"
                },
                new SocialHandle
                {
                    Id = "arise_x",
                    Handle = "@ARISETV",
                    DisplayName = "Arise Television",
                    ProfileUrl = "https://x.com/ARISETV",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/arise_tv_400x400.jpg",
                    Bio = "Global broadcast news network — Nigerian news, African stories, world perspective.",
                    Category = "Media"
                },
                // Commentators
                new SocialHandle
                {
                    Id = "omokri_x",
                    Handle = "@renoomokri",
                    DisplayName = "Reno Omokri",
                    ProfileUrl = "https://x.com/renoomokri",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/reno_omokri_400x400.jpg",
                    Bio = "Bestselling author, political commentator, and social media influencer.",
                    Category = "Commentator"
                },
                new SocialHandle
                {
                    Id = "efcc_x",
                    Handle = "@officialEFCC",
                    DisplayName = "EFCC Nigeria",
                    ProfileUrl = "https://x.com/officialEFCC",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/efcc_400x400.jpg",
                    Bio = "Official Twitter account of the Economic and Financial Crimes Commission (EFCC).",
                    Category = "Government"
                },
                new SocialHandle
                {
                    Id = "atiku_x",
                    Handle = "@atiku",
                    DisplayName = "Atiku Abubakar",
                    ProfileUrl = "https://x.com/atiku",
                    AvatarUrl = "https://pbs.twimg.com/profile_images/atiku_400x400.jpg",
                    Bio = "Former Vice President of Nigeria. Committed to national development and unity.",
                    Category = "Commentator"
                }
            );
            db.SaveChanges();
            logger.LogInformation("Default social handles seeded.");
        }

        // Seed initial social posts if empty
        if (!db.SocialPosts.Any())
        {
            db.SocialPosts.AddRange(
                new SocialPost
                {
                    Id = Guid.NewGuid().ToString("N"),
                    AuthorName = "Premium Times",
                    AuthorHandle = "@PremiumTimesng",
                    AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/premium_times_400x400.jpg",
                    Content = "BREAKING: Federal Executive Council approves new national infrastructure development fund aimed at boosting transport and power grids across 36 states.",
                    PostUrl = "https://x.com/PremiumTimesng",
                    MediaUrl = "https://images.unsplash.com/photo-1541888946425-d0fbb18086f6?w=600&auto=format&fit=crop&q=80",
                    Category = "Politics",
                    LikesCount = 2450,
                    RetweetsCount = 890,
                    PublishedAt = DateTime.UtcNow.AddMinutes(-35)
                },
                new SocialPost
                {
                    Id = Guid.NewGuid().ToString("N"),
                    AuthorName = "Channels Television",
                    AuthorHandle = "@channelstv",
                    AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/1683847522/channels_tv_400x400.jpg",
                    Content = "JUST IN: Central Bank reiterates foreign exchange stability guidelines as trading volumes climb 14% on the official window.",
                    PostUrl = "https://x.com/channelstv",
                    Category = "Business",
                    LikesCount = 1820,
                    RetweetsCount = 540,
                    PublishedAt = DateTime.UtcNow.AddHours(-1)
                },
                new SocialPost
                {
                    Id = Guid.NewGuid().ToString("N"),
                    AuthorName = "Punch Newspapers",
                    AuthorHandle = "@MobilePunch",
                    AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/punch_400x400.jpg",
                    Content = "SPORTS: Nigeria Football Federation announces final roster and tactical schedule ahead of next month's international tournament.",
                    PostUrl = "https://x.com/MobilePunch",
                    MediaUrl = "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80",
                    Category = "Sports",
                    LikesCount = 3890,
                    RetweetsCount = 1220,
                    PublishedAt = DateTime.UtcNow.AddHours(-2)
                },
                new SocialPost
                {
                    Id = Guid.NewGuid().ToString("N"),
                    AuthorName = "EFCC Nigeria",
                    AuthorHandle = "@officialEFCC",
                    AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/efcc_400x400.jpg",
                    Content = "PRESS RELEASE: EFCC recovers over ₦4.2 Billion in fraudulent real estate scheme, returns assets to legitimate institutional investors.",
                    PostUrl = "https://x.com/officialEFCC",
                    Category = "Government",
                    LikesCount = 5120,
                    RetweetsCount = 1940,
                    PublishedAt = DateTime.UtcNow.AddHours(-4)
                }
            );
            db.SaveChanges();
            logger.LogInformation("Default social posts seeded.");
        }
        // Seed initial video stories if empty (using verified real news broadcast clips)
        if (!db.VideoStories.Any())
        {
            db.VideoStories.AddRange(
                new VideoStory
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VideoId = "r8FW2pqYrq4",
                    Title = "WCQ Play-Off: 'It’s A Do Or Die Match', South Africa Coach On Nigeria Clash",
                    Summary = "South Africa national team coach speaks ahead of the crucial world cup qualifying match against Super Eagles.",
                    VideoUrl = "https://www.youtube.com/watch?v=r8FW2pqYrq4",
                    ThumbnailUrl = "https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg",
                    ChannelName = "Channels Television",
                    ChannelId = "channels_tv",
                    Duration = "LIVE / RECENT",
                    Category = "Sports",
                    PublishedAt = DateTime.UtcNow.AddHours(-1),
                    CreatedAt = DateTime.UtcNow
                },
                new VideoStory
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VideoId = "2nrElTSJE58",
                    Title = "TVC News Headline News: National Economy, Markets & Policy Review",
                    Summary = "Comprehensive news coverage and analysis of fiscal reforms and key governance developments across the federation.",
                    VideoUrl = "https://www.youtube.com/watch?v=2nrElTSJE58",
                    ThumbnailUrl = "https://i.ytimg.com/vi/2nrElTSJE58/hqdefault.jpg",
                    ChannelName = "TVC News Nigeria",
                    ChannelId = "tvc_news",
                    Duration = "12:10",
                    Category = "News",
                    PublishedAt = DateTime.UtcNow.AddHours(-3),
                    CreatedAt = DateTime.UtcNow
                },
                new VideoStory
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VideoId = "hmjB9EQ66V8",
                    Title = "TheCable Special Feature: Investigative Spotlight and Governance Insights",
                    Summary = "In-depth investigative documentary exploring key policy implementation and socio-economic milestones.",
                    VideoUrl = "https://www.youtube.com/watch?v=hmjB9EQ66V8",
                    ThumbnailUrl = "https://i.ytimg.com/vi/hmjB9EQ66V8/hqdefault.jpg",
                    ChannelName = "TheCable",
                    ChannelId = "the_cable",
                    Duration = "09:30",
                    Category = "Politics",
                    PublishedAt = DateTime.UtcNow.AddHours(-5),
                    CreatedAt = DateTime.UtcNow
                },
                new VideoStory
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VideoId = "7LA4EfXNXzc",
                    Title = "SaharaTV Report: National Assembly and Legal Developments",
                    Summary = "Coverage of recent legislative proceedings, public hearings, and civic accountability discussions.",
                    VideoUrl = "https://www.youtube.com/watch?v=7LA4EfXNXzc",
                    ThumbnailUrl = "https://i.ytimg.com/vi/7LA4EfXNXzc/hqdefault.jpg",
                    ChannelName = "SaharaTV",
                    ChannelId = "sahara_tv",
                    Duration = "15:20",
                    Category = "Politics",
                    PublishedAt = DateTime.UtcNow.AddHours(-7),
                    CreatedAt = DateTime.UtcNow
                }
            );
            db.SaveChanges();
            logger.LogInformation("Default video stories seeded.");
        }

        // Trigger initial background sync of live YouTube video feeds
        _ = Task.Run(async () =>
        {
            try
            {
                using var syncScope = app.Services.CreateScope();
                var ytService = syncScope.ServiceProvider.GetRequiredService<YouTubeFeedService>();
                var newCount = await ytService.SyncAllChannelsAsync();
                logger.LogInformation("Startup YouTube feeds sync completed: {Count} stories processed.", newCount);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background startup YouTube sync encountered an issue (non-fatal).");
            }
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing database during startup.");
    }
}

app.Run();
