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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasIndex(a => a.PublishedAt);
            entity.HasIndex(a => a.Category);
            entity.HasIndex(a => a.Source);
            entity.HasIndex(a => a.ContentType);
            entity.HasIndex(a => a.Url);
            entity.HasIndex(a => a.Title);
            entity.HasIndex(a => a.IsSponsored);
            entity.HasIndex(a => a.IsPinned);
            entity.HasIndex(a => a.CampaignExpiresAt);
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
        });
    }
}
