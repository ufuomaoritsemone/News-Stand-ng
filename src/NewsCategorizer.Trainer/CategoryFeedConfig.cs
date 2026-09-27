namespace NewsCategorizer.Trainer;

public class CategoryFeedEndpoint
{
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
}

public static class CategoryFeedConfig
{
    public static readonly List<CategoryFeedEndpoint> Endpoints = new()
    {
        // 1. Politics (Strictly political news topics)
        new CategoryFeedEndpoint { Category = "Politics", Source = "Punch", FeedUrl = "https://punchng.com/topics/politics/feed/" },
        new CategoryFeedEndpoint { Category = "Politics", Source = "Guardian", FeedUrl = "https://guardian.ng/category/politics/feed/" },
        new CategoryFeedEndpoint { Category = "Politics", Source = "Vanguard", FeedUrl = "https://www.vanguardngr.com/category/politics/feed/" },
        new CategoryFeedEndpoint { Category = "Politics", Source = "DailyTrust", FeedUrl = "https://dailytrust.com/category/politics/feed/" },

        // 2. Business (Financial, commercial, economy, and capital market feeds)
        new CategoryFeedEndpoint { Category = "Business", Source = "Punch", FeedUrl = "https://punchng.com/topics/business/feed/" },
        new CategoryFeedEndpoint { Category = "Business", Source = "Guardian", FeedUrl = "https://guardian.ng/category/business/feed/" },
        new CategoryFeedEndpoint { Category = "Business", Source = "Vanguard", FeedUrl = "https://www.vanguardngr.com/category/business/feed/" },
        new CategoryFeedEndpoint { Category = "Business", Source = "DailyTrust", FeedUrl = "https://dailytrust.com/category/business/feed/" },
        new CategoryFeedEndpoint { Category = "Business", Source = "BusinessDay", FeedUrl = "https://businessday.ng/feed/" },

        // 3. Sports (Football, athletics, tournaments, and global sports)
        new CategoryFeedEndpoint { Category = "Sports", Source = "Punch", FeedUrl = "https://punchng.com/topics/sports/feed/" },
        new CategoryFeedEndpoint { Category = "Sports", Source = "Guardian", FeedUrl = "https://guardian.ng/category/sport/feed/" },
        new CategoryFeedEndpoint { Category = "Sports", Source = "Premium Times", FeedUrl = "https://www.premiumtimesng.com/category/sports/feed" },
        new CategoryFeedEndpoint { Category = "Sports", Source = "Vanguard", FeedUrl = "https://www.vanguardngr.com/category/sports/feed/" },
        new CategoryFeedEndpoint { Category = "Sports", Source = "DailyTrust", FeedUrl = "https://dailytrust.com/category/sports/feed/" },

        // 4. Entertainment (Music, Nollywood, pop culture, reality TV)
        new CategoryFeedEndpoint { Category = "Entertainment", Source = "Punch", FeedUrl = "https://punchng.com/topics/entertainment/feed/" },
        new CategoryFeedEndpoint { Category = "Entertainment", Source = "Premium Times", FeedUrl = "https://www.premiumtimesng.com/category/entertainment/feed" },
        new CategoryFeedEndpoint { Category = "Entertainment", Source = "Vanguard", FeedUrl = "https://www.vanguardngr.com/category/entertainment/feed/" },
        new CategoryFeedEndpoint { Category = "Entertainment", Source = "DailyTrust", FeedUrl = "https://dailytrust.com/category/entertainment/feed/" },
        new CategoryFeedEndpoint { Category = "Entertainment", Source = "LindaIkeji", FeedUrl = "https://www.lindaikejisblog.com/feed" },

        // 5. Technology (Fintech, AI, startups, telecommunications, digital innovations)
        new CategoryFeedEndpoint { Category = "Technology", Source = "Punch", FeedUrl = "https://punchng.com/topics/technology/feed/" },
        new CategoryFeedEndpoint { Category = "Technology", Source = "Guardian", FeedUrl = "https://guardian.ng/category/technology/feed/" },
        new CategoryFeedEndpoint { Category = "Technology", Source = "TechCabal", FeedUrl = "https://techcabal.com/feed/" },
        new CategoryFeedEndpoint { Category = "Technology", Source = "Techpoint", FeedUrl = "https://techpoint.africa/feed/" },

        // 6. Crime & Security (Law enforcement, anti-graft, courts, military operations)
        new CategoryFeedEndpoint { Category = "Crime", Source = "Vanguard", FeedUrl = "https://www.vanguardngr.com/category/crime-courts/feed/" },
        new CategoryFeedEndpoint { Category = "Crime", Source = "Punch", FeedUrl = "https://punchng.com/topics/crime/feed/" },
        new CategoryFeedEndpoint { Category = "Crime", Source = "DailyTrust", FeedUrl = "https://dailytrust.com/category/crime/feed/" },

        // 7. General (Public health, education, transport, social welfare, environment, features)
        new CategoryFeedEndpoint { Category = "General", Source = "Punch Health", FeedUrl = "https://punchng.com/topics/health/feed/" },
        new CategoryFeedEndpoint { Category = "General", Source = "Punch Education", FeedUrl = "https://punchng.com/topics/education/feed/" },
        new CategoryFeedEndpoint { Category = "General", Source = "Guardian Features", FeedUrl = "https://guardian.ng/category/features/feed/" },
        new CategoryFeedEndpoint { Category = "General", Source = "DailyTrust Health", FeedUrl = "https://dailytrust.com/category/health/feed/" },
        new CategoryFeedEndpoint { Category = "General", Source = "DailyTrust Education", FeedUrl = "https://dailytrust.com/category/education/feed/" }
    };
}
