using Microsoft.EntityFrameworkCore;
using NewsApi.Models;

namespace NewsApi.Data;

public class NewsDbContext : DbContext
{
    public NewsDbContext(DbContextOptions<NewsDbContext> options) : base(options)
    {
    }

    public DbSet<Article> Articles { get; set; } = null!;
    public DbSet<Source> Sources { get; set; } = null!;
    public DbSet<AudioAsset> AudioAssets { get; set; } = null!;
    public DbSet<Briefing> Briefings { get; set; } = null!;
    public DbSet<VideoChannel> VideoChannels { get; set; } = null!;
    public DbSet<SocialHandle> SocialHandles { get; set; } = null!;
    public DbSet<VideoStory> VideoStories { get; set; } = null!;
    public DbSet<SocialPost> SocialPosts { get; set; } = null!;
    public DbSet<FeedbackItem> Feedbacks { get; set; } = null!;
    public DbSet<CategoryCorrection> CategoryCorrections { get; set; } = null!;
    public DbSet<UserEvent> UserEvents { get; set; } = null!;
    public DbSet<StoryComment> StoryComments { get; set; } = null!;
    public DbSet<CommentVote> CommentVotes { get; set; } = null!;
    public DbSet<CommentReport> CommentReports { get; set; } = null!;
    public DbSet<UserVerification> UserVerifications { get; set; } = null!;
    public DbSet<Topic> Topics { get; set; } = null!;
    public DbSet<ArticleTopic> ArticleTopics { get; set; } = null!;
    public DbSet<ArticleEmbedding> ArticleEmbeddings { get; set; } = null!;
    public DbSet<StoryCluster> StoryClusters { get; set; } = null!;
    public DbSet<StoryClusterArticle> StoryClusterArticles { get; set; } = null!;
    public DbSet<ArticleEnrichment> ArticleEnrichments { get; set; } = null!;
    public DbSet<MarketDataRecord> MarketDataRecords { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasIndex(a => a.Slug).IsUnique();
            entity.HasIndex(a => a.PublishedAt);
            entity.HasIndex(a => a.Category);
            entity.HasIndex(a => a.Source);
            entity.HasIndex(a => a.ContentType);
            entity.HasIndex(a => a.Url);
            entity.HasIndex(a => a.Title);
            entity.HasIndex(a => a.IsSponsored);
            entity.HasIndex(a => a.IsPinned);
            entity.HasIndex(a => a.CampaignExpiresAt);
            entity.HasIndex(a => a.UpdatedAt);
        });

        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasIndex(s => s.Slug).IsUnique();
        });

        modelBuilder.Entity<Topic>(entity =>
        {
            entity.HasIndex(t => t.Slug).IsUnique();
            entity.HasIndex(t => t.Type);
            entity.HasIndex(t => t.CreatedAt);
        });

        modelBuilder.Entity<ArticleTopic>(entity =>
        {
            entity.HasKey(at => new { at.ArticleId, at.TopicId });
            entity.HasIndex(at => at.TopicId);

            entity.HasOne(at => at.Article)
                .WithMany(a => a.ArticleTopics)
                .HasForeignKey(at => at.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(at => at.Topic)
                .WithMany(t => t.ArticleTopics)
                .HasForeignKey(at => at.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArticleEmbedding>(entity =>
        {
            entity.HasKey(ae => ae.ArticleId);
            entity.HasOne(ae => ae.Article)
                .WithOne(a => a.Embedding)
                .HasForeignKey<ArticleEmbedding>(ae => ae.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArticleEnrichment>(entity =>
        {
            entity.HasKey(ae => ae.ArticleId);
            entity.HasOne(ae => ae.Article)
                .WithOne(a => a.Enrichment)
                .HasForeignKey<ArticleEnrichment>(ae => ae.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(ae => ae.CreatedAt);
        });

        modelBuilder.Entity<MarketDataRecord>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.MetricKey).IsUnique();
            entity.HasIndex(m => m.LastUpdated);
        });

        modelBuilder.Entity<StoryCluster>(entity =>
        {
            entity.HasIndex(sc => sc.Slug).IsUnique();
            entity.HasIndex(sc => sc.UpdatedAt);
            entity.HasIndex(sc => sc.CreatedAt);
        });

        modelBuilder.Entity<StoryClusterArticle>(entity =>
        {
            entity.HasKey(sca => new { sca.ClusterId, sca.ArticleId });
            entity.HasIndex(sca => sca.ArticleId);

            entity.HasOne(sca => sca.Cluster)
                .WithMany(sc => sc.ClusterArticles)
                .HasForeignKey(sca => sca.ClusterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sca => sca.Article)
                .WithMany(a => a.StoryClusterArticles)
                .HasForeignKey(sca => sca.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AudioAsset>(entity =>
        {
            entity.HasIndex(a => a.ArticleId);
        });

        modelBuilder.Entity<VideoStory>(entity =>
        {
            entity.HasIndex(v => v.PublishedAt);
            entity.HasIndex(v => v.ChannelId);
            entity.HasIndex(v => v.VideoId);
            entity.HasIndex(v => v.IsTrending);
            entity.HasIndex(v => v.TrendingRank);
        });

        modelBuilder.Entity<SocialPost>(entity =>
        {
            entity.HasIndex(s => s.PublishedAt);
            entity.HasIndex(s => s.AuthorHandle);
        });

        modelBuilder.Entity<FeedbackItem>(entity =>
        {
            entity.HasIndex(f => f.CreatedAt);
            entity.HasIndex(f => f.Category);
        });

        modelBuilder.Entity<CategoryCorrection>(entity =>
        {
            entity.HasIndex(c => c.CreatedAt);
            entity.HasIndex(c => c.ArticleId);
        });

        modelBuilder.Entity<UserEvent>(entity =>
        {
            entity.HasIndex(e => e.OccurredAt);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.ArticleId);
        });

        modelBuilder.Entity<StoryComment>(entity =>
        {
            entity.HasIndex(c => c.ArticleId);
            entity.HasIndex(c => c.ParentCommentId);
            entity.HasIndex(c => c.CreatedAt);
            entity.HasIndex(c => c.Status);
        });

        modelBuilder.Entity<CommentVote>(entity =>
        {
            entity.HasIndex(v => new { v.CommentId, v.UserId }).IsUnique();
        });

        modelBuilder.Entity<CommentReport>(entity =>
        {
            entity.HasIndex(r => r.CommentId);
            entity.HasIndex(r => r.CreatedAt);
        });

        modelBuilder.Entity<UserVerification>(entity =>
        {
            entity.HasIndex(u => u.Identifier);
            entity.HasIndex(u => u.ExpiresAt);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(s => s.Key);
            entity.Property(s => s.Key).HasMaxLength(150);
            entity.Property(s => s.Category).HasMaxLength(50);
        });
    }

    /// <summary>
    /// When true (default), automatically assigns collision-resistant slugs and excerpts to new or modified articles on save.
    /// Can be disabled during tests or bulk imports to simulate legacy data.
    /// </summary>
    public bool AutoGenerateSlugsOnSave { get; set; } = true;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (AutoGenerateSlugsOnSave)
        {
            EnsureArticleSlugs();
        }
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (AutoGenerateSlugsOnSave)
        {
            EnsureArticleSlugs();
        }
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureArticleSlugs()
    {
        var entries = ChangeTracker.Entries<Article>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        var usedSlugsInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var article = entry.Entity;
            if (string.IsNullOrWhiteSpace(article.Slug))
            {
                var titleText = !string.IsNullOrWhiteSpace(article.Title) ? article.Title : "article";
                var clean = System.Text.RegularExpressions.Regex.Replace(titleText.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
                if (clean.Length > 60) clean = clean[..60].TrimEnd('-');
                if (string.IsNullOrWhiteSpace(clean)) clean = "story";

                var suffix = Guid.NewGuid().ToString("N")[..6].ToLowerInvariant();
                var candidate = $"{clean}-{suffix}";
                while (usedSlugsInBatch.Contains(candidate))
                {
                    suffix = Guid.NewGuid().ToString("N")[..6].ToLowerInvariant();
                    candidate = $"{clean}-{suffix}";
                }

                article.Slug = candidate;
                usedSlugsInBatch.Add(candidate);
            }

            if (string.IsNullOrWhiteSpace(article.Excerpt) && (!string.IsNullOrWhiteSpace(article.Summary) || !string.IsNullOrWhiteSpace(article.Content)))
            {
                var raw = !string.IsNullOrWhiteSpace(article.Summary) ? article.Summary : article.Content;
                var clean = System.Text.RegularExpressions.Regex.Replace(raw!, "<.*?>", " ");
                clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();
                article.Excerpt = clean.Length > 300 ? clean[..297] + "..." : clean;
            }
        }
    }
}
