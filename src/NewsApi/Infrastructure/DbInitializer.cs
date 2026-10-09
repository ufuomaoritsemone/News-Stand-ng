using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Infrastructure;

/// <summary>
/// Handles EF Core schema migration and initial data seeding at application startup.
/// Extracted from Program.cs to satisfy Single Responsibility Principle.
/// </summary>
public interface IDbInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public class DbInitializer(
    NewsDbContext db,
    ILogger<DbInitializer> logger,
    ISlugService? slugService = null) : IDbInitializer
{
    private readonly ISlugService _slugService = slugService ?? new SlugService();
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await BaselineLegacyDatabaseIfNeededAsync(cancellationToken);

        try
        {
            // Run EF Core migrations (creates schema if missing, applies pending migrations)
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database schema migrated successfully via EF Core Migrations.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MigrateAsync failed. Falling back to EnsureCreatedAsync.");
            try
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Database initialization failed completely.");
                throw;
            }
        }

        await EnsureTablesExistAsync(cancellationToken);
        await EnsureSchemaColumnsUpdatedAsync(cancellationToken);
        await EnsureFullTextSearchCreatedAsync(db, logger, cancellationToken);
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

    // ── Legacy Database Baselining ───────────────────────────────────────────

    /// <summary>
    /// If the database was created before EF Core migrations existed, records the initial
    /// migration in __EFMigrationsHistory so MigrateAsync does not attempt duplicate table creation.
    /// </summary>
    private async Task BaselineLegacyDatabaseIfNeededAsync(CancellationToken ct)
    {
        try
        {
            var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
            var isPostgres = db.Database.ProviderName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;

            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await db.Database.OpenConnectionAsync(ct);
            }

            bool hasLegacyTables = false;
            if (isSqlite)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Articles';";
                var result = await cmd.ExecuteScalarAsync(ct);
                hasLegacyTables = Convert.ToInt32(result) > 0;
            }
            else if (isPostgres)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Articles';";
                var result = await cmd.ExecuteScalarAsync(ct);
                hasLegacyTables = Convert.ToInt32(result) > 0;
            }

            if (hasLegacyTables)
            {
                if (isSqlite)
                {
                    using var histCmd = conn.CreateCommand();
                    histCmd.CommandText = """
                        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                            "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                            "ProductVersion" TEXT NOT NULL
                        );
                        INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                        VALUES ('20260911133927_InitialCreate', '10.0.11');
                        INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                        VALUES ('20260913220753_AddAuthorAndContentType', '10.0.11');
                        """;
                    await histCmd.ExecuteNonQueryAsync(ct);
                }
                else if (isPostgres)
                {
                    using var histCmd = conn.CreateCommand();
                    histCmd.CommandText = """
                        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                            "MigrationId" varchar(150) NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                            "ProductVersion" varchar(32) NOT NULL
                        );
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                        VALUES ('20260911133927_InitialCreate', '10.0.11')
                        ON CONFLICT DO NOTHING;
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                        VALUES ('20260913220753_AddAuthorAndContentType', '10.0.11')
                        ON CONFLICT DO NOTHING;
                        """;
                    await histCmd.ExecuteNonQueryAsync(ct);
                }
                logger.LogInformation("Legacy database schema baselined with InitialCreate and AddAuthorAndContentType migrations.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not check/baseline legacy migration history. Continuing with standard migration.");
        }
    }

    // ── Table & Schema Upgrades (SQLite & PostgreSQL) ────────────────────────

    private async Task EnsureTablesExistAsync(CancellationToken ct)
    {
        var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var isPostgres = db.Database.ProviderName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;

        if (!isSqlite && !isPostgres) return;

        try
        {
            if (isPostgres)
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "VideoChannels" (
                        "Id" text NOT NULL,
                        "ChannelName" text NOT NULL,
                        "YoutubeChannelId" text NOT NULL,
                        "ThumbnailUrl" text NOT NULL,
                        "ChannelUrl" text NOT NULL,
                        "Description" text NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_VideoChannels" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "VideoStories" (
                        "Id" text NOT NULL,
                        "VideoId" text NOT NULL,
                        "Title" text NOT NULL,
                        "Summary" text NOT NULL,
                        "VideoUrl" text NOT NULL,
                        "ThumbnailUrl" text NOT NULL,
                        "ChannelName" text NOT NULL,
                        "ChannelId" text NOT NULL,
                        "Duration" text NOT NULL,
                        "Category" text NOT NULL,
                        "PublishedAt" timestamp with time zone NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        "IsTrending" boolean NOT NULL DEFAULT false,
                        "TrendingRank" integer NULL,
                        "ViewCount" bigint NOT NULL DEFAULT 0,
                        "LikeCount" bigint NOT NULL DEFAULT 0,
                        CONSTRAINT "PK_VideoStories" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "CategoryCorrections" (
                        "Id" text NOT NULL,
                        "ArticleId" text NOT NULL,
                        "Title" text NOT NULL,
                        "Summary" text NULL,
                        "OldCategory" text NOT NULL,
                        "NewCategory" text NOT NULL,
                        "Source" text NULL,
                        "Url" text NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_CategoryCorrections" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "Feedbacks" (
                        "Id" text NOT NULL,
                        "Rating" integer NOT NULL,
                        "Category" character varying(100) NOT NULL,
                        "Message" text NOT NULL,
                        "UserEmail" character varying(200) NULL,
                        "UserName" character varying(100) NULL,
                        "AppVersion" character varying(50) NULL,
                        "Platform" character varying(50) NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        "IsEmailSent" boolean NOT NULL DEFAULT false,
                        CONSTRAINT "PK_Feedbacks" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "SocialHandles" (
                        "Id" text NOT NULL,
                        "Handle" text NOT NULL,
                        "DisplayName" text NOT NULL,
                        "ProfileUrl" text NOT NULL,
                        "AvatarUrl" text NOT NULL,
                        "Bio" text NOT NULL,
                        "Category" text NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_SocialHandles" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "SocialPosts" (
                        "Id" text NOT NULL,
                        "AuthorName" text NOT NULL,
                        "AuthorHandle" text NOT NULL,
                        "AuthorAvatarUrl" text NOT NULL,
                        "Content" text NOT NULL,
                        "PostUrl" text NOT NULL,
                        "MediaUrl" text NULL,
                        "Category" text NOT NULL,
                        "LikesCount" integer NOT NULL DEFAULT 0,
                        "RetweetsCount" integer NOT NULL DEFAULT 0,
                        "PublishedAt" timestamp with time zone NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_SocialPosts" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "UserEvents" (
                        "Id" text NOT NULL,
                        "DeviceId" character varying(64) NOT NULL,
                        "EventType" character varying(50) NOT NULL,
                        "ArticleId" character varying(64) NULL,
                        "ArticleTitle" character varying(300) NULL,
                        "Category" character varying(100) NULL,
                        "Platform" character varying(50) NULL,
                        "AppVersion" character varying(30) NULL,
                        "OccurredAt" timestamp with time zone NOT NULL,
                        "ReceivedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_UserEvents" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "AudioAssets" (
                        "Id" uuid NOT NULL,
                        "ArticleId" text NULL,
                        "Url" text NOT NULL,
                        "Duration" interval NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_AudioAssets" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "Briefings" (
                        "Id" uuid NOT NULL,
                        "Category" text NOT NULL,
                        "GeneratedAt" timestamp with time zone NOT NULL,
                        "PayloadJson" text NOT NULL,
                        CONSTRAINT "PK_Briefings" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "StoryComments" (
                        "Id" text NOT NULL,
                        "ArticleId" text NOT NULL,
                        "ArticleTitle" text NULL,
                        "ParentCommentId" text NULL,
                        "AuthorName" text NOT NULL,
                        "AuthorAvatar" text NULL,
                        "AuthorId" text NOT NULL,
                        "Content" text NOT NULL,
                        "Upvotes" integer NOT NULL DEFAULT 0,
                        "Downvotes" integer NOT NULL DEFAULT 0,
                        "ReportCount" integer NOT NULL DEFAULT 0,
                        "IsPinned" boolean NOT NULL DEFAULT false,
                        "Status" text NOT NULL DEFAULT 'Published',
                        "CreatedAt" timestamp with time zone NOT NULL,
                        "UpdatedAt" timestamp with time zone NULL,
                        CONSTRAINT "PK_StoryComments" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "CommentVotes" (
                        "Id" text NOT NULL,
                        "CommentId" text NOT NULL,
                        "UserId" text NOT NULL,
                        "VoteType" integer NOT NULL DEFAULT 1,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_CommentVotes" PRIMARY KEY ("Id")
                    );

                    CREATE TABLE IF NOT EXISTS "CommentReports" (
                        "Id" text NOT NULL,
                        "CommentId" text NOT NULL,
                        "ReportedBy" text NOT NULL,
                        "Reason" text NOT NULL,
                        "IsResolved" boolean NOT NULL DEFAULT false,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_CommentReports" PRIMARY KEY ("Id")
                    );

                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_ChannelId" ON "VideoStories" ("ChannelId");
                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_IsTrending" ON "VideoStories" ("IsTrending");
                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_PublishedAt" ON "VideoStories" ("PublishedAt");
                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_TrendingRank" ON "VideoStories" ("TrendingRank");
                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_VideoId" ON "VideoStories" ("VideoId");
                    CREATE INDEX IF NOT EXISTS "IX_SocialPosts_PublishedAt" ON "SocialPosts" ("PublishedAt");
                    CREATE INDEX IF NOT EXISTS "IX_SocialPosts_AuthorHandle" ON "SocialPosts" ("AuthorHandle");
                    CREATE INDEX IF NOT EXISTS "IX_Feedbacks_CreatedAt" ON "Feedbacks" ("CreatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_Feedbacks_Category" ON "Feedbacks" ("Category");
                    CREATE INDEX IF NOT EXISTS "IX_CategoryCorrections_CreatedAt" ON "CategoryCorrections" ("CreatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_CategoryCorrections_ArticleId" ON "CategoryCorrections" ("ArticleId");
                    CREATE INDEX IF NOT EXISTS "IX_UserEvents_OccurredAt" ON "UserEvents" ("OccurredAt");
                    CREATE INDEX IF NOT EXISTS "IX_UserEvents_EventType" ON "UserEvents" ("EventType");
                    CREATE INDEX IF NOT EXISTS "IX_UserEvents_DeviceId" ON "UserEvents" ("DeviceId");
                    CREATE INDEX IF NOT EXISTS "IX_StoryComments_ArticleId" ON "StoryComments" ("ArticleId");
                    CREATE INDEX IF NOT EXISTS "IX_StoryComments_ParentCommentId" ON "StoryComments" ("ParentCommentId");
                    CREATE INDEX IF NOT EXISTS "IX_StoryComments_CreatedAt" ON "StoryComments" ("CreatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_CommentVotes_CommentId_UserId" ON "CommentVotes" ("CommentId", "UserId");
                    CREATE INDEX IF NOT EXISTS "IX_CommentReports_CommentId" ON "CommentReports" ("CommentId");

                    CREATE TABLE IF NOT EXISTS "UserVerifications" (
                        "Id" text NOT NULL,
                        "Identifier" text NOT NULL,
                        "DisplayName" text NOT NULL,
                        "AvatarUrl" text NULL,
                        "Channel" text NOT NULL,
                        "OtpHash" text NULL,
                        "AttemptCount" integer NOT NULL DEFAULT 0,
                        "IsVerified" boolean NOT NULL DEFAULT false,
                        "ExpiresAt" timestamp with time zone NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_UserVerifications" PRIMARY KEY ("Id")
                    );
                    CREATE INDEX IF NOT EXISTS "IX_UserVerifications_Identifier" ON "UserVerifications" ("Identifier");
                    CREATE INDEX IF NOT EXISTS "IX_UserVerifications_ExpiresAt" ON "UserVerifications" ("ExpiresAt");

                    CREATE TABLE IF NOT EXISTS "Topics" (
                        "Id" text NOT NULL,
                        "Slug" character varying(100) NOT NULL,
                        "Name" character varying(150) NOT NULL,
                        "Type" integer NOT NULL DEFAULT 3,
                        "Description" character varying(500) NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_Topics" PRIMARY KEY ("Id")
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Topics_Slug" ON "Topics" ("Slug");
                    CREATE INDEX IF NOT EXISTS "IX_Topics_Type" ON "Topics" ("Type");
                    CREATE INDEX IF NOT EXISTS "IX_Topics_CreatedAt" ON "Topics" ("CreatedAt");

                    CREATE TABLE IF NOT EXISTS "ArticleTopics" (
                        "ArticleId" text NOT NULL,
                        "TopicId" text NOT NULL,
                        "Confidence" real NOT NULL DEFAULT 1.0,
                        CONSTRAINT "PK_ArticleTopics" PRIMARY KEY ("ArticleId", "TopicId"),
                        CONSTRAINT "FK_ArticleTopics_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_ArticleTopics_Topics_TopicId" FOREIGN KEY ("TopicId") REFERENCES "Topics" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_ArticleTopics_TopicId" ON "ArticleTopics" ("TopicId");

                    CREATE TABLE IF NOT EXISTS "ArticleEmbeddings" (
                        "ArticleId" text NOT NULL,
                        "Dimensions" integer NOT NULL,
                        "VectorBlob" bytea NOT NULL,
                        "Model" character varying(50) NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_ArticleEmbeddings" PRIMARY KEY ("ArticleId"),
                        CONSTRAINT "FK_ArticleEmbeddings_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS "StoryClusters" (
                        "Id" text NOT NULL,
                        "Slug" character varying(160) NOT NULL,
                        "Title" character varying(500) NOT NULL,
                        "Summary" text NULL,
                        "ImageUrl" text NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        "UpdatedAt" timestamp with time zone NOT NULL,
                        "ArticleCount" integer NOT NULL DEFAULT 0,
                        "SourceCount" integer NOT NULL DEFAULT 0,
                        CONSTRAINT "PK_StoryClusters" PRIMARY KEY ("Id")
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_StoryClusters_Slug" ON "StoryClusters" ("Slug");
                    CREATE INDEX IF NOT EXISTS "IX_StoryClusters_UpdatedAt" ON "StoryClusters" ("UpdatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_StoryClusters_CreatedAt" ON "StoryClusters" ("CreatedAt");

                    CREATE TABLE IF NOT EXISTS "StoryClusterArticles" (
                        "ClusterId" text NOT NULL,
                        "ArticleId" text NOT NULL,
                        "SimilarityScore" real NOT NULL DEFAULT 1.0,
                        "AddedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_StoryClusterArticles" PRIMARY KEY ("ClusterId", "ArticleId"),
                        CONSTRAINT "FK_StoryClusterArticles_StoryClusters_ClusterId" FOREIGN KEY ("ClusterId") REFERENCES "StoryClusters" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_StoryClusterArticles_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_StoryClusterArticles_ArticleId" ON "StoryClusterArticles" ("ArticleId");

                    CREATE TABLE IF NOT EXISTS "ArticleEnrichments" (
                        "ArticleId" text NOT NULL,
                        "KeyTakeawaysJson" text NOT NULL,
                        "TimelineFactsJson" text NOT NULL,
                        "FaqsJson" text NOT NULL,
                        "FullSummaryJson" text NULL,
                        "Engine" character varying(50) NOT NULL DEFAULT 'TextRank-Extractive',
                        "CreatedAt" timestamp with time zone NOT NULL,
                        "UpdatedAt" timestamp with time zone NULL,
                        CONSTRAINT "PK_ArticleEnrichments" PRIMARY KEY ("ArticleId"),
                        CONSTRAINT "FK_ArticleEnrichments_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_ArticleEnrichments_CreatedAt" ON "ArticleEnrichments" ("CreatedAt");

                    CREATE TABLE IF NOT EXISTS "MarketDataRecords" (
                        "Id" serial NOT NULL,
                        "MetricKey" character varying(100) NOT NULL,
                        "Title" character varying(255) NOT NULL,
                        "PrimaryValue" character varying(100) NOT NULL,
                        "SecondaryValue" character varying(100) NULL,
                        "Change24h" character varying(50) NULL,
                        "Unit" character varying(50) NOT NULL,
                        "SourceAttribution" character varying(255) NOT NULL,
                        "HistoricalDataJson" text NOT NULL,
                        "FaqsJson" text NOT NULL,
                        "SummaryMarkdown" text NOT NULL,
                        "LastUpdated" timestamp with time zone NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_MarketDataRecords" PRIMARY KEY ("Id")
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketDataRecords_MetricKey" ON "MarketDataRecords" ("MetricKey");
                    CREATE INDEX IF NOT EXISTS "IX_MarketDataRecords_LastUpdated" ON "MarketDataRecords" ("LastUpdated");
                    """, ct);
            }
            else if (isSqlite)
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "VideoChannels" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_VideoChannels" PRIMARY KEY,
                        "ChannelName" TEXT NOT NULL,
                        "YoutubeChannelId" TEXT NOT NULL,
                        "ThumbnailUrl" TEXT NOT NULL,
                        "ChannelUrl" TEXT NOT NULL,
                        "Description" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS "VideoStories" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_VideoStories" PRIMARY KEY,
                        "VideoId" TEXT NOT NULL,
                        "Title" TEXT NOT NULL,
                        "Summary" TEXT NOT NULL,
                        "VideoUrl" TEXT NOT NULL,
                        "ThumbnailUrl" TEXT NOT NULL,
                        "ChannelName" TEXT NOT NULL,
                        "ChannelId" TEXT NOT NULL,
                        "Duration" TEXT NOT NULL,
                        "Category" TEXT NOT NULL,
                        "PublishedAt" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "IsTrending" INTEGER NOT NULL DEFAULT 0,
                        "TrendingRank" INTEGER NULL,
                        "ViewCount" INTEGER NOT NULL DEFAULT 0,
                        "LikeCount" INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS "CategoryCorrections" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_CategoryCorrections" PRIMARY KEY,
                        "ArticleId" TEXT NOT NULL,
                        "Title" TEXT NOT NULL,
                        "Summary" TEXT NULL,
                        "OldCategory" TEXT NOT NULL,
                        "NewCategory" TEXT NOT NULL,
                        "Source" TEXT NULL,
                        "Url" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS "Feedbacks" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_Feedbacks" PRIMARY KEY,
                        "Rating" INTEGER NOT NULL,
                        "Category" TEXT NOT NULL,
                        "Message" TEXT NOT NULL,
                        "UserEmail" TEXT NULL,
                        "UserName" TEXT NULL,
                        "AppVersion" TEXT NULL,
                        "Platform" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "IsEmailSent" INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS "SocialHandles" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_SocialHandles" PRIMARY KEY,
                        "Handle" TEXT NOT NULL,
                        "DisplayName" TEXT NOT NULL,
                        "ProfileUrl" TEXT NOT NULL,
                        "AvatarUrl" TEXT NOT NULL,
                        "Bio" TEXT NOT NULL,
                        "Category" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS "SocialPosts" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_SocialPosts" PRIMARY KEY,
                        "AuthorName" TEXT NOT NULL,
                        "AuthorHandle" TEXT NOT NULL,
                        "AuthorAvatarUrl" TEXT NOT NULL,
                        "Content" TEXT NOT NULL,
                        "PostUrl" TEXT NOT NULL,
                        "MediaUrl" TEXT NULL,
                        "Category" TEXT NOT NULL,
                        "LikesCount" INTEGER NOT NULL DEFAULT 0,
                        "RetweetsCount" INTEGER NOT NULL DEFAULT 0,
                        "PublishedAt" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS "UserEvents" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_UserEvents" PRIMARY KEY,
                        "DeviceId" TEXT NOT NULL,
                        "EventType" TEXT NOT NULL,
                        "ArticleId" TEXT NULL,
                        "ArticleTitle" TEXT NULL,
                        "Category" TEXT NULL,
                        "Platform" TEXT NULL,
                        "AppVersion" TEXT NULL,
                        "OccurredAt" TEXT NOT NULL,
                        "ReceivedAt" TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS "StoryComments" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_StoryComments" PRIMARY KEY,
                        "ArticleId" TEXT NOT NULL,
                        "ArticleTitle" TEXT NULL,
                        "ParentCommentId" TEXT NULL,
                        "AuthorName" TEXT NOT NULL,
                        "AuthorAvatar" TEXT NULL,
                        "AuthorId" TEXT NOT NULL,
                        "Content" TEXT NOT NULL,
                        "Upvotes" INTEGER NOT NULL DEFAULT 0,
                        "Downvotes" INTEGER NOT NULL DEFAULT 0,
                        "ReportCount" INTEGER NOT NULL DEFAULT 0,
                        "IsPinned" INTEGER NOT NULL DEFAULT 0,
                        "Status" TEXT NOT NULL DEFAULT 'Published',
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL
                    );

                    CREATE TABLE IF NOT EXISTS "CommentVotes" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_CommentVotes" PRIMARY KEY,
                        "CommentId" TEXT NOT NULL,
                        "UserId" TEXT NOT NULL,
                        "VoteType" INTEGER NOT NULL DEFAULT 1,
                        "CreatedAt" TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS "CommentReports" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_CommentReports" PRIMARY KEY,
                        "CommentId" TEXT NOT NULL,
                        "ReportedBy" TEXT NOT NULL,
                        "Reason" TEXT NOT NULL,
                        "IsResolved" INTEGER NOT NULL DEFAULT 0,
                        "CreatedAt" TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_ChannelId" ON "VideoStories" ("ChannelId");
                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_IsTrending" ON "VideoStories" ("IsTrending");
                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_PublishedAt" ON "VideoStories" ("PublishedAt");
                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_TrendingRank" ON "VideoStories" ("TrendingRank");
                    CREATE INDEX IF NOT EXISTS "IX_VideoStories_VideoId" ON "VideoStories" ("VideoId");
                    CREATE INDEX IF NOT EXISTS "IX_SocialPosts_PublishedAt" ON "SocialPosts" ("PublishedAt");
                    CREATE INDEX IF NOT EXISTS "IX_SocialPosts_AuthorHandle" ON "SocialPosts" ("AuthorHandle");
                    CREATE INDEX IF NOT EXISTS "IX_Feedbacks_CreatedAt" ON "Feedbacks" ("CreatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_Feedbacks_Category" ON "Feedbacks" ("Category");
                    CREATE INDEX IF NOT EXISTS "IX_CategoryCorrections_CreatedAt" ON "CategoryCorrections" ("CreatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_CategoryCorrections_ArticleId" ON "CategoryCorrections" ("ArticleId");
                    CREATE INDEX IF NOT EXISTS "IX_UserEvents_OccurredAt" ON "UserEvents" ("OccurredAt");
                    CREATE INDEX IF NOT EXISTS "IX_UserEvents_EventType" ON "UserEvents" ("EventType");
                    CREATE INDEX IF NOT EXISTS "IX_UserEvents_DeviceId" ON "UserEvents" ("DeviceId");
                    CREATE INDEX IF NOT EXISTS "IX_StoryComments_ArticleId" ON "StoryComments" ("ArticleId");
                    CREATE INDEX IF NOT EXISTS "IX_StoryComments_ParentCommentId" ON "StoryComments" ("ParentCommentId");
                    CREATE INDEX IF NOT EXISTS "IX_StoryComments_CreatedAt" ON "StoryComments" ("CreatedAt");
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_CommentVotes_CommentId_UserId" ON "CommentVotes" ("CommentId", "UserId");
                    CREATE INDEX IF NOT EXISTS "IX_CommentReports_CommentId" ON "CommentReports" ("CommentId");

                    CREATE TABLE IF NOT EXISTS "UserVerifications" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_UserVerifications" PRIMARY KEY,
                        "Identifier" TEXT NOT NULL,
                        "DisplayName" TEXT NOT NULL,
                        "AvatarUrl" TEXT NULL,
                        "Channel" TEXT NOT NULL,
                        "OtpHash" TEXT NULL,
                        "AttemptCount" INTEGER NOT NULL DEFAULT 0,
                        "IsVerified" INTEGER NOT NULL DEFAULT 0,
                        "ExpiresAt" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_UserVerifications_Identifier" ON "UserVerifications" ("Identifier");
                    CREATE INDEX IF NOT EXISTS "IX_UserVerifications_ExpiresAt" ON "UserVerifications" ("ExpiresAt");

                    CREATE TABLE IF NOT EXISTS "Topics" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_Topics" PRIMARY KEY,
                        "Slug" TEXT NOT NULL,
                        "Name" TEXT NOT NULL,
                        "Type" INTEGER NOT NULL DEFAULT 3,
                        "Description" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Topics_Slug" ON "Topics" ("Slug");
                    CREATE INDEX IF NOT EXISTS "IX_Topics_Type" ON "Topics" ("Type");
                    CREATE INDEX IF NOT EXISTS "IX_Topics_CreatedAt" ON "Topics" ("CreatedAt");

                    CREATE TABLE IF NOT EXISTS "ArticleTopics" (
                        "ArticleId" TEXT NOT NULL,
                        "TopicId" TEXT NOT NULL,
                        "Confidence" REAL NOT NULL DEFAULT 1.0,
                        CONSTRAINT "PK_ArticleTopics" PRIMARY KEY ("ArticleId", "TopicId"),
                        CONSTRAINT "FK_ArticleTopics_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_ArticleTopics_Topics_TopicId" FOREIGN KEY ("TopicId") REFERENCES "Topics" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_ArticleTopics_TopicId" ON "ArticleTopics" ("TopicId");

                    CREATE TABLE IF NOT EXISTS "ArticleEmbeddings" (
                        "ArticleId" TEXT NOT NULL CONSTRAINT "PK_ArticleEmbeddings" PRIMARY KEY,
                        "Dimensions" INTEGER NOT NULL,
                        "VectorBlob" BLOB NOT NULL,
                        "Model" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        CONSTRAINT "FK_ArticleEmbeddings_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS "StoryClusters" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_StoryClusters" PRIMARY KEY,
                        "Slug" TEXT NOT NULL,
                        "Title" TEXT NOT NULL,
                        "Summary" TEXT NULL,
                        "ImageUrl" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NOT NULL,
                        "ArticleCount" INTEGER NOT NULL DEFAULT 0,
                        "SourceCount" INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_StoryClusters_Slug" ON "StoryClusters" ("Slug");
                    CREATE INDEX IF NOT EXISTS "IX_StoryClusters_UpdatedAt" ON "StoryClusters" ("UpdatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_StoryClusters_CreatedAt" ON "StoryClusters" ("CreatedAt");

                    CREATE TABLE IF NOT EXISTS "StoryClusterArticles" (
                        "ClusterId" TEXT NOT NULL,
                        "ArticleId" TEXT NOT NULL,
                        "SimilarityScore" REAL NOT NULL DEFAULT 1.0,
                        "AddedAt" TEXT NOT NULL,
                        CONSTRAINT "PK_StoryClusterArticles" PRIMARY KEY ("ClusterId", "ArticleId"),
                        CONSTRAINT "FK_StoryClusterArticles_StoryClusters_ClusterId" FOREIGN KEY ("ClusterId") REFERENCES "StoryClusters" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_StoryClusterArticles_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_StoryClusterArticles_ArticleId" ON "StoryClusterArticles" ("ArticleId");

                    CREATE TABLE IF NOT EXISTS "ArticleEnrichments" (
                        "ArticleId" TEXT NOT NULL CONSTRAINT "PK_ArticleEnrichments" PRIMARY KEY,
                        "KeyTakeawaysJson" TEXT NOT NULL,
                        "TimelineFactsJson" TEXT NOT NULL,
                        "FaqsJson" TEXT NOT NULL,
                        "FullSummaryJson" TEXT NULL,
                        "Engine" TEXT NOT NULL DEFAULT 'TextRank-Extractive',
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL,
                        CONSTRAINT "FK_ArticleEnrichments_Articles_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Articles" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_ArticleEnrichments_CreatedAt" ON "ArticleEnrichments" ("CreatedAt");

                    CREATE TABLE IF NOT EXISTS "MarketDataRecords" (
                        "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        "MetricKey" TEXT NOT NULL,
                        "Title" TEXT NOT NULL,
                        "PrimaryValue" TEXT NOT NULL,
                        "SecondaryValue" TEXT NULL,
                        "Change24h" TEXT NULL,
                        "Unit" TEXT NOT NULL,
                        "SourceAttribution" TEXT NOT NULL,
                        "HistoricalDataJson" TEXT NOT NULL,
                        "FaqsJson" TEXT NOT NULL,
                        "SummaryMarkdown" TEXT NOT NULL,
                        "LastUpdated" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketDataRecords_MetricKey" ON "MarketDataRecords" ("MetricKey");
                    CREATE INDEX IF NOT EXISTS "IX_MarketDataRecords_LastUpdated" ON "MarketDataRecords" ("LastUpdated");
                    """, ct);
            }
            logger.LogInformation("Ensured all required database tables and indexes exist.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure all tables exist; will rely on individual seeds and migrations.");
        }
    }

    private async Task EnsureSchemaColumnsUpdatedAsync(CancellationToken ct)
    {
        var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var isPostgres = db.Database.ProviderName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;

        if (!isSqlite && !isPostgres) return;

        await AddColumnIfMissingAsync("Sources", "SitemapUrl", "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Sources", "ScraperType", "TEXT NULL DEFAULT 'Hybrid'", ct);
        await AddColumnIfMissingAsync("VideoStories", "IsTrending", isPostgres ? "boolean NOT NULL DEFAULT false" : "INTEGER NOT NULL DEFAULT 0", ct);
        await AddColumnIfMissingAsync("VideoStories", "TrendingRank", "INTEGER NULL", ct);
        await AddColumnIfMissingAsync("VideoStories", "ViewCount", isPostgres ? "bigint NOT NULL DEFAULT 0" : "INTEGER NOT NULL DEFAULT 0", ct);
        await AddColumnIfMissingAsync("VideoStories", "LikeCount", isPostgres ? "bigint NOT NULL DEFAULT 0" : "INTEGER NOT NULL DEFAULT 0", ct);
        await AddColumnIfMissingAsync("Articles", "IsSponsored", isPostgres ? "boolean NOT NULL DEFAULT false" : "INTEGER NOT NULL DEFAULT 0", ct);
        await AddColumnIfMissingAsync("Articles", "SponsorName", "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Articles", "SponsorUrl", "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Articles", "CampaignExpiresAt", isPostgres ? "timestamp with time zone NULL" : "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Articles", "IsPinned", isPostgres ? "boolean NOT NULL DEFAULT false" : "INTEGER NOT NULL DEFAULT 0", ct);
        await AddColumnIfMissingAsync("Articles", "ImpressionCount", "INTEGER NOT NULL DEFAULT 0", ct);
        await AddColumnIfMissingAsync("Articles", "ClickCount", "INTEGER NOT NULL DEFAULT 0", ct);
        await AddColumnIfMissingAsync("Articles", "Author", "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Articles", "ContentType", "TEXT NULL DEFAULT 'News'", ct);
        await AddColumnIfMissingAsync("Articles", "TargetPosition", "INTEGER NULL", ct);
        await AddColumnIfMissingAsync("Articles", "PriorityWeight", "INTEGER NOT NULL DEFAULT 1", ct);
        await AddColumnIfMissingAsync("Articles", "UpdatedAt", isPostgres ? "timestamp with time zone NULL" : "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Articles", "Slug", "TEXT NOT NULL DEFAULT ''", ct);
        await AddColumnIfMissingAsync("Articles", "Excerpt", "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Articles", "SeoTitle", "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Articles", "SeoDescription", "TEXT NULL", ct);
        await AddColumnIfMissingAsync("Sources", "Slug", "TEXT NULL", ct);
        await AddColumnIfMissingAsync("ArticleEnrichments", "FullSummaryJson", "TEXT NULL", ct);
    }

    private async Task AddColumnIfMissingAsync(string tableName, string columnName, string columnDefinition, CancellationToken ct)
    {
        try
        {
            var isPostgres = db.Database.ProviderName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;

            if (isPostgres)
            {
#pragma warning disable EF1002
                var sql = $"ALTER TABLE \"{tableName}\" ADD COLUMN IF NOT EXISTS \"{columnName}\" {columnDefinition};";
                await db.Database.ExecuteSqlRawAsync(sql, ct);
#pragma warning restore EF1002
                return;
            }

            // SQLite: check PRAGMA table_info with double-quoted identifier to ensure column discovery
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var conn = db.Database.GetDbConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";

            if (conn.State != System.Data.ConnectionState.Open)
                await db.Database.OpenConnectionAsync(ct);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                existingColumns.Add(reader.GetString(1));
            }

            if (!existingColumns.Contains(columnName))
            {
#pragma warning disable EF1002
                var sql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};";
                await db.Database.ExecuteSqlRawAsync(sql, ct);
#pragma warning restore EF1002
                logger.LogInformation("Added missing column {Column} to SQLite table {Table}.", columnName, tableName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not verify/add column {Column} to table {Table}.", columnName, tableName);
        }
    }

    /// <summary>
    /// Configures high-performance Full-Text Search (FTS) structures:
    /// - SQLite: FTS5 virtual table "Articles_fts" with unicode61/porter stemming and triggers.
    /// - PostgreSQL: tsvector generated column "SearchVector" with GIN index.
    /// </summary>
    public static async Task EnsureFullTextSearchCreatedAsync(NewsDbContext db, ILogger? logger, CancellationToken ct = default)
    {
        var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var isPostgres = db.Database.ProviderName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;

        if (isSqlite)
        {
            try
            {
                // 1. Create FTS5 Virtual Table
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE VIRTUAL TABLE IF NOT EXISTS "Articles_fts" USING fts5(
                        Id UNINDEXED,
                        Title,
                        Summary,
                        Content,
                        tokenize = 'porter unicode61'
                    );
                    """, ct);

                // 2. Create triggers for automatic synchronization
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TRIGGER IF NOT EXISTS "articles_ai_fts" AFTER INSERT ON "Articles" BEGIN
                        INSERT INTO "Articles_fts"(Id, Title, Summary, Content)
                        VALUES (new.Id, new.Title, coalesce(new.Summary, ''), coalesce(new.Content, ''));
                    END;
                    """, ct);

                // Fix #42: Standalone SQLite FTS5 tables require standard DELETE FROM syntax rather than external content insert 'delete' syntax
                await db.Database.ExecuteSqlRawAsync("""
                    DROP TRIGGER IF EXISTS "articles_ad_fts";
                    CREATE TRIGGER "articles_ad_fts" AFTER DELETE ON "Articles" BEGIN
                        DELETE FROM "Articles_fts" WHERE Id = old.Id;
                    END;
                    """, ct);

                await db.Database.ExecuteSqlRawAsync("""
                    DROP TRIGGER IF EXISTS "articles_au_fts";
                    CREATE TRIGGER "articles_au_fts" AFTER UPDATE ON "Articles" BEGIN
                        DELETE FROM "Articles_fts" WHERE Id = old.Id;
                        INSERT INTO "Articles_fts"(Id, Title, Summary, Content)
                        VALUES (new.Id, new.Title, coalesce(new.Summary, ''), coalesce(new.Content, ''));
                    END;
                    """, ct);

                // 3. Backfill any existing articles not yet in FTS5
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "Articles_fts"(Id, Title, Summary, Content)
                    SELECT "Id", "Title", coalesce("Summary", ''), coalesce("Content", '')
                    FROM "Articles"
                    WHERE "Id" NOT IN (SELECT Id FROM "Articles_fts");
                    """, ct);

                logger?.LogInformation("SQLite FTS5 index and synchronization triggers initialized successfully.");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not initialize SQLite FTS5 index. Standard search fallback will be used.");
            }
        }
        else if (isPostgres)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE "Articles" ADD COLUMN IF NOT EXISTS "SearchVector" tsvector
                        GENERATED ALWAYS AS (
                            setweight(to_tsvector('english', coalesce("Title", '')), 'A') ||
                            setweight(to_tsvector('english', coalesce("Summary", '')), 'B') ||
                            setweight(to_tsvector('english', coalesce("Content", '')), 'C')
                        ) STORED;
                    """, ct);

                await db.Database.ExecuteSqlRawAsync("""
                    CREATE INDEX IF NOT EXISTS "IX_Articles_SearchVector" ON "Articles" USING GIN ("SearchVector");
                    """, ct);

                logger?.LogInformation("PostgreSQL tsvector generated column and GIN index initialized successfully.");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not initialize PostgreSQL tsvector column. Standard search fallback will be used.");
            }
        }
    }

    // ── Sources ──────────────────────────────────────────────────────────────

    private async Task SeedSourcesAsync(CancellationToken ct)
    {
        if (await db.Sources.AnyAsync(ct)) 
        {
            await EnsureChannelsTvAsync(ct);
            await EnsureNairametricsAsync(ct);
            await EnsureBusinessDayAsync(ct);
            await EnsureLindaIkejiAsync(ct);
            await UpgradeGuardianToHybridAsync(ct);
            await EnsureOpinionSourcesAsync(ct);
            return;
        }

        db.Sources.AddRange(
            new Source { Id = "punch",        Name = "Punch Newspaper",       RssUrl = "https://punchng.com/feed/" },
            new Source { Id = "guardian",     Name = "The Guardian Nigeria",  SitemapUrl = "https://guardian.ng/news-sitemap.xml",          RssUrl = "https://news.google.com/rss/search?q=site:guardian.ng&hl=en-NG&gl=NG&ceid=NG:en", ScraperType = "Hybrid" },
            new Source { Id = "premiumtimes", Name = "Premium Times",         RssUrl = "https://www.premiumtimesng.com/feed" },
            new Source { Id = "channelstv",   Name = "Channels Television",   SitemapUrl = "https://www.channelstv.com/news-sitemap.xml",    RssUrl = "https://www.channelstv.com/feed/", ScraperType = "Hybrid" },
            new Source { Id = "nairametrics", Name = "Nairametrics",          SitemapUrl = "https://nairametrics.com/news-sitemap.xml",     RssUrl = "https://nairametrics.com/feed/",      ScraperType = "Hybrid" },
            new Source { Id = "businessday",  Name = "BusinessDay Nigeria",   RssUrl = "https://businessday.ng/feed/",                      ScraperType = "Rss" },
            new Source { Id = "lindaikeji",   Name = "Linda Ikeji's Blog",    RssUrl = "https://www.lindaikejisblog.com/feed",             ScraperType = "Rss" }
        );
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Default news sources seeded.");
    }

    private async Task EnsureChannelsTvAsync(CancellationToken ct)
    {
        if (await db.Sources.AnyAsync(s => s.Id == "channelstv", ct)) return;
        db.Sources.Add(new Source
        {
            Id = "channelstv", Name = "Channels Television",
            SitemapUrl = "https://www.channelstv.com/news-sitemap.xml",
            RssUrl = "https://www.channelstv.com/feed/", ScraperType = "Hybrid"
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Channels Television source added to existing database.");
    }

    private async Task EnsureNairametricsAsync(CancellationToken ct)
    {
        if (await db.Sources.AnyAsync(s => s.Id == "nairametrics", ct)) return;
        db.Sources.Add(new Source
        {
            Id = "nairametrics", Name = "Nairametrics",
            SitemapUrl = "https://nairametrics.com/news-sitemap.xml",
            RssUrl = "https://nairametrics.com/feed/", ScraperType = "Hybrid"
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Nairametrics source added to existing database.");
    }

    private async Task EnsureBusinessDayAsync(CancellationToken ct)
    {
        if (await db.Sources.AnyAsync(s => s.Id == "businessday", ct)) return;
        db.Sources.Add(new Source
        {
            Id = "businessday", Name = "BusinessDay Nigeria",
            RssUrl = "https://businessday.ng/feed/", ScraperType = "Rss"
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("BusinessDay source added to existing database.");
    }

    private async Task EnsureLindaIkejiAsync(CancellationToken ct)
    {
        if (await db.Sources.AnyAsync(s => s.Id == "lindaikeji", ct)) return;
        db.Sources.Add(new Source
        {
            Id = "lindaikeji", Name = "Linda Ikeji's Blog",
            RssUrl = "https://www.lindaikejisblog.com/feed", ScraperType = "Rss"
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Linda Ikeji source added to existing database.");
    }

    private async Task UpgradeGuardianToHybridAsync(CancellationToken ct)
    {
        var guardian = await db.Sources.FindAsync(["guardian"], ct);
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
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Guardian Nigeria updated to news-sitemap.xml and Google News proxy.");
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
            if (!await db.Sources.AnyAsync(s => s.Id == id, ct))
            {
                db.Sources.Add(new Source
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
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Dedicated editorial and opinion sources seeded successfully.");
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
            if (!await db.Sources.AnyAsync(s => s.Id == id, ct))
            {
                db.Sources.Add(new Source
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
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Dedicated international/world sources seeded successfully.");
        }
    }

    // ── Articles ─────────────────────────────────────────────────────────────

    private async Task SeedArticlesAsync(CancellationToken ct)
    {
        if (await db.Articles.AnyAsync(ct)) return;

        db.Articles.AddRange(
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "Federal Government Unveils New Digital Economy Roadmap",   Slug = "fg-unveils-digital-economy-roadmap-seed01",   Excerpt = "The Ministry of Communications and Digital Economy announced a strategic initiative.", Summary = "The Ministry of Communications and Digital Economy announced a strategic initiative.", Url = "https://punchng.com/news/digital-roadmap",               Source = "Punch Newspaper",        Category = "Politics",     PublishedAt = DateTime.UtcNow },
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "Super Eagles Prepare for Upcoming International Friendly", Slug = "super-eagles-prepare-friendly-match-seed02", Excerpt = "Coaching staff confirm full squad training ahead of weekend clash.",               Summary = "Coaching staff confirm full squad training ahead of weekend clash.",               Url = "https://guardian.ng/sports/super-eagles-friendly",         Source = "The Guardian Nigeria",   Category = "Sports",       PublishedAt = DateTime.UtcNow.AddHours(-2) },
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "Central Bank Highlights Monetary Policy Outlook for Q3",  Slug = "cbn-monetary-policy-outlook-q3-seed03",      Excerpt = "Key indicators show steady stabilization across foreign exchange markets.",          Summary = "Key indicators show steady stabilization across foreign exchange markets.",          Url = "https://www.premiumtimesng.com/business/cbn-monetary-policy", Source = "Premium Times",        Category = "Business",     PublishedAt = DateTime.UtcNow.AddHours(-4) },
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "Tech Hub Ecosystem Grows Across Lagos and Abuja",         Slug = "tech-hub-ecosystem-lagos-abuja-seed04",       Excerpt = "Venture investments in Nigerian fintech startups reach record highs this quarter.",  Summary = "Venture investments in Nigerian fintech startups reach record highs this quarter.",  Url = "https://punchng.com/tech/startup-growth",                  Source = "Punch Newspaper",        Category = "Technology",   PublishedAt = DateTime.UtcNow.AddHours(-6) },
            new Article { Id = Guid.NewGuid().ToString("N"), Title = "United Nations Security Council Convenes Summit on Global Diplomatic Treaties", Slug = "un-security-council-global-summit-seed05", Excerpt = "World leaders and international envoys assemble at UN headquarters to negotiate multilateral regional agreements.", Summary = "World leaders and international envoys assemble at UN headquarters to negotiate multilateral regional agreements.", Url = "https://guardian.ng/news/un-global-security-summit", Source = "The Guardian Nigeria", Category = "International", PublishedAt = DateTime.UtcNow.AddHours(-8) }
        );
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Default sample articles seeded.");
    }

    // ── Sponsored Articles (Direct Monetization) ──────────────────────────────

    private async Task SeedSponsoredArticlesAsync(CancellationToken ct)
    {
        if (await db.Articles.AnyAsync(a => a.IsSponsored, ct)) return;

        db.Articles.Add(new Article
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
            ImpressionCount = 1420,
            ClickCount = 118
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Default sample sponsored campaign seeded.");
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
        if (!await db.VideoChannels.AnyAsync(ct))
        {
            db.VideoChannels.AddRange(DefaultChannels);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Default video channels seeded.");
            return;
        }

        var existing = await db.VideoChannels.ToListAsync(ct);
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
                db.VideoChannels.Add(def);
                changed = true;
            }
        }
        if (changed) await db.SaveChangesAsync(ct);
    }

    // ── Social Handles ────────────────────────────────────────────────────────

    private async Task SeedSocialHandlesAsync(CancellationToken ct)
    {
        if (await db.SocialHandles.AnyAsync(ct)) return;

        db.SocialHandles.AddRange(
            new SocialHandle { Id = "channels_tv_x",  Handle = "@channelstv",      DisplayName = "Channels Television",  ProfileUrl = "https://x.com/channelstv",      AvatarUrl = "https://pbs.twimg.com/profile_images/1683847522/channels_tv_400x400.jpg",      Bio = "Nigeria's No.1 News Station.",                            Category = "Media" },
            new SocialHandle { Id = "premium_times_x", Handle = "@PremiumTimesng",  DisplayName = "Premium Times",        ProfileUrl = "https://x.com/PremiumTimesng",  AvatarUrl = "https://pbs.twimg.com/profile_images/premium_times_400x400.jpg",            Bio = "Award-winning investigative journalism.",                  Category = "Media" },
            new SocialHandle { Id = "punch_x",         Handle = "@MobilePunch",     DisplayName = "Punch Newspapers",     ProfileUrl = "https://x.com/MobilePunch",     AvatarUrl = "https://pbs.twimg.com/profile_images/punch_400x400.jpg",                  Bio = "Most widely read newspaper in Nigeria.",                   Category = "Media" },
            new SocialHandle { Id = "guardian_x",      Handle = "@GuardianNigeria", DisplayName = "The Guardian Nigeria", ProfileUrl = "https://x.com/GuardianNigeria", AvatarUrl = "https://pbs.twimg.com/profile_images/guardian_ng_400x400.jpg",           Bio = "Conscience, nurtured by truth.",                           Category = "Media" },
            new SocialHandle { Id = "vanguard_x",      Handle = "@vanguardngrnews", DisplayName = "Vanguard Newspapers",  ProfileUrl = "https://x.com/vanguardngrnews", AvatarUrl = "https://pbs.twimg.com/profile_images/vanguard_400x400.jpg",              Bio = "Towards a better life for the people.",                    Category = "Media" },
            new SocialHandle { Id = "arise_x",         Handle = "@ARISETV",         DisplayName = "Arise Television",     ProfileUrl = "https://x.com/ARISETV",         AvatarUrl = "https://pbs.twimg.com/profile_images/arise_tv_400x400.jpg",             Bio = "Global broadcast news network.",                           Category = "Media" },
            new SocialHandle { Id = "omokri_x",        Handle = "@renoomokri",      DisplayName = "Reno Omokri",          ProfileUrl = "https://x.com/renoomokri",      AvatarUrl = "https://pbs.twimg.com/profile_images/reno_omokri_400x400.jpg",          Bio = "Bestselling author and political commentator.",            Category = "Commentator" },
            new SocialHandle { Id = "efcc_x",          Handle = "@officialEFCC",    DisplayName = "EFCC Nigeria",         ProfileUrl = "https://x.com/officialEFCC",    AvatarUrl = "https://pbs.twimg.com/profile_images/efcc_400x400.jpg",                 Bio = "Official Twitter account of EFCC.",                        Category = "Government" },
            new SocialHandle { Id = "atiku_x",         Handle = "@atiku",           DisplayName = "Atiku Abubakar",       ProfileUrl = "https://x.com/atiku",           AvatarUrl = "https://pbs.twimg.com/profile_images/atiku_400x400.jpg",                Bio = "Former Vice President of Nigeria.",                        Category = "Commentator" }
        );
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Default social handles seeded.");
    }

    // ── Social Posts ──────────────────────────────────────────────────────────

    private async Task SeedSocialPostsAsync(CancellationToken ct)
    {
        if (await db.SocialPosts.AnyAsync(ct)) return;

        db.SocialPosts.AddRange(
            new SocialPost { Id = Guid.NewGuid().ToString("N"), AuthorName = "Premium Times",    AuthorHandle = "@PremiumTimesng",  AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/premium_times_400x400.jpg", Content = "BREAKING: Federal Executive Council approves new national infrastructure development fund.", PostUrl = "https://x.com/PremiumTimesng", MediaUrl = "https://images.unsplash.com/photo-1541888946425-d0fbb18086f6?w=600&auto=format&fit=crop&q=80", Category = "Politics",    LikesCount = 2450, RetweetsCount = 890,  PublishedAt = DateTime.UtcNow.AddMinutes(-35) },
            new SocialPost { Id = Guid.NewGuid().ToString("N"), AuthorName = "Channels TV",      AuthorHandle = "@channelstv",      AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/1683847522/channels_tv_400x400.jpg",     Content = "JUST IN: Central Bank reiterates foreign exchange stability guidelines.",               PostUrl = "https://x.com/channelstv",    Category = "Business",   LikesCount = 1820, RetweetsCount = 540,  PublishedAt = DateTime.UtcNow.AddHours(-1) },
            new SocialPost { Id = Guid.NewGuid().ToString("N"), AuthorName = "Punch Newspapers", AuthorHandle = "@MobilePunch",     AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/punch_400x400.jpg",                    Content = "SPORTS: Nigeria Football Federation announces final roster ahead of international tournament.", PostUrl = "https://x.com/MobilePunch",  MediaUrl = "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80", Category = "Sports",     LikesCount = 3890, RetweetsCount = 1220, PublishedAt = DateTime.UtcNow.AddHours(-2) },
            new SocialPost { Id = Guid.NewGuid().ToString("N"), AuthorName = "EFCC Nigeria",     AuthorHandle = "@officialEFCC",    AuthorAvatarUrl = "https://pbs.twimg.com/profile_images/efcc_400x400.jpg",                    Content = "PRESS RELEASE: EFCC recovers over ₦4.2 Billion in fraudulent real estate scheme.",         PostUrl = "https://x.com/officialEFCC", Category = "Government", LikesCount = 5120, RetweetsCount = 1940, PublishedAt = DateTime.UtcNow.AddHours(-4) }
        );
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Default social posts seeded.");
    }

    // ── Video Stories ─────────────────────────────────────────────────────────

    private async Task SeedVideoStoriesAsync(CancellationToken ct)
    {
        if (await db.VideoStories.AnyAsync(ct)) return;

        db.VideoStories.AddRange(
            new VideoStory { Id = Guid.NewGuid().ToString("N"), VideoId = "r8FW2pqYrq4", Title = "WCQ Play-Off: 'It's A Do Or Die Match', South Africa Coach On Nigeria Clash",    Summary = "South Africa coach speaks ahead of the crucial world cup qualifying match.",               VideoUrl = "https://www.youtube.com/watch?v=r8FW2pqYrq4", ThumbnailUrl = "https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg", ChannelName = "Channels Television", ChannelId = "channels_tv", Duration = "LIVE / RECENT", Category = "Sports",    PublishedAt = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow },
            new VideoStory { Id = Guid.NewGuid().ToString("N"), VideoId = "2nrElTSJE58", Title = "TVC News Headline News: National Economy, Markets & Policy Review",              Summary = "Comprehensive coverage and analysis of fiscal reforms and governance developments.",        VideoUrl = "https://www.youtube.com/watch?v=2nrElTSJE58", ThumbnailUrl = "https://i.ytimg.com/vi/2nrElTSJE58/hqdefault.jpg", ChannelName = "TVC News Nigeria",    ChannelId = "tvc_news",    Duration = "12:10",        Category = "News",      PublishedAt = DateTime.UtcNow.AddHours(-3), CreatedAt = DateTime.UtcNow },
            new VideoStory { Id = Guid.NewGuid().ToString("N"), VideoId = "hmjB9EQ66V8", Title = "TheCable Special Feature: Investigative Spotlight and Governance Insights",     Summary = "In-depth investigative documentary exploring key policy implementation.",                  VideoUrl = "https://www.youtube.com/watch?v=hmjB9EQ66V8", ThumbnailUrl = "https://i.ytimg.com/vi/hmjB9EQ66V8/hqdefault.jpg", ChannelName = "TheCable",            ChannelId = "the_cable",   Duration = "09:30",        Category = "Politics",  PublishedAt = DateTime.UtcNow.AddHours(-5), CreatedAt = DateTime.UtcNow },
            new VideoStory { Id = Guid.NewGuid().ToString("N"), VideoId = "7LA4EfXNXzc", Title = "SaharaTV Report: National Assembly and Legal Developments",                     Summary = "Coverage of recent legislative proceedings and civic accountability discussions.",           VideoUrl = "https://www.youtube.com/watch?v=7LA4EfXNXzc", ThumbnailUrl = "https://i.ytimg.com/vi/7LA4EfXNXzc/hqdefault.jpg", ChannelName = "SaharaTV",            ChannelId = "sahara_tv",   Duration = "15:20",        Category = "Politics",  PublishedAt = DateTime.UtcNow.AddHours(-7), CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Default video stories seeded.");
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private async Task CleanDummyStoriesAsync(CancellationToken ct)
    {
        var dummies = await db.VideoStories
            .Where(v => v.VideoUrl.Contains("dQw4w9WgXcQ") || v.VideoId.Contains("dQw4w9WgXcQ"))
            .ToListAsync(ct);

        if (dummies.Count > 0)
        {
            db.VideoStories.RemoveRange(dummies);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Removed {Count} dummy placeholder video stories.", dummies.Count);
        }
    }

    // ── SEO Slugs & Topics Backfill ───────────────────────────────────────────

    private async Task EnsureArticleSlugsBackfilledAsync(CancellationToken ct)
    {
        try
        {
            // 1. Backfill Sources
            var sources = await db.Sources.Where(s => string.IsNullOrEmpty(s.Slug)).ToListAsync(ct);
            if (sources.Count > 0)
            {
                foreach (var s in sources)
                {
                    s.Slug = _slugService.GenerateSlug(string.IsNullOrWhiteSpace(s.Name) ? s.Id : s.Name, 60);
                }
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Backfilled SEO slugs for {Count} sources.", sources.Count);
            }

            // 2. Seed Default Topics if table is empty
            await SeedDefaultTopicsAsync(ct);

            // 3. Backfill Articles
            var articlesNeedingSeo = await db.Articles
                .Where(a => string.IsNullOrEmpty(a.Slug) || string.IsNullOrEmpty(a.Excerpt))
                .Take(500)
                .ToListAsync(ct);

            if (articlesNeedingSeo.Count > 0)
            {
                var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in articlesNeedingSeo)
                {
                    if (string.IsNullOrWhiteSpace(a.Slug))
                    {
                        var slug = _slugService.GenerateArticleSlug(a.Title, a.Id);
                        int attempts = 0;
                        while (usedSlugs.Contains(slug) && attempts < 5)
                        {
                            slug = _slugService.GenerateArticleSlug(a.Title, null);
                            attempts++;
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

                await db.SaveChangesAsync(ct);
                logger.LogInformation("Backfilled SEO slugs & excerpts for {Count} initial articles.", articlesNeedingSeo.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Automatic SEO backfill during initialization encountered a non-fatal warning; backfill service remains available via API.");
        }
    }

    private async Task SeedDefaultTopicsAsync(CancellationToken ct)
    {
        if (await db.Topics.AnyAsync(ct)) return;

        var defaultTopics = new List<Topic>
        {
            // Persons
            new() { Id = "topic_tinubu",   Slug = "bola-tinubu",    Name = "Bola Ahmed Tinubu",            Type = TopicType.Person,       Description = "President of the Federal Republic of Nigeria." },
            new() { Id = "topic_shettima", Slug = "kashim-shettima", Name = "Kashim Shettima",             Type = TopicType.Person,       Description = "Vice President of the Federal Republic of Nigeria." },
            new() { Id = "topic_cardoso",  Slug = "yemi-cardoso",   Name = "Olayemi Cardoso",              Type = TopicType.Person,       Description = "Governor of the Central Bank of Nigeria (CBN)." },
            new() { Id = "topic_dangote",  Slug = "aliko-dangote",  Name = "Aliko Dangote",                Type = TopicType.Person,       Description = "Nigerian industrialist and founder of Dangote Group." },

            // Organizations
            new() { Id = "topic_cbn",      Slug = "cbn",            Name = "Central Bank of Nigeria",      Type = TopicType.Organization, Description = "The apex monetary authority of Nigeria." },
            new() { Id = "topic_nnpc",     Slug = "nnpc",           Name = "NNPC Limited",                 Type = TopicType.Organization, Description = "National oil company and primary energy supplier." },
            new() { Id = "topic_efcc",     Slug = "efcc",           Name = "EFCC",                         Type = TopicType.Organization, Description = "Economic and Financial Crimes Commission." },
            new() { Id = "topic_eagles",   Slug = "super-eagles",   Name = "Super Eagles",                 Type = TopicType.Organization, Description = "Nigeria national men's football team." },

            // Places
            new() { Id = "topic_lagos",    Slug = "lagos",          Name = "Lagos State",                  Type = TopicType.Place,        Description = "Commercial nerve centre and economic capital of Nigeria." },
            new() { Id = "topic_abuja",    Slug = "abuja",          Name = "Abuja FCT",                    Type = TopicType.Place,        Description = "Federal Capital Territory and political seat of Nigeria." },
            new() { Id = "topic_kano",     Slug = "kano",           Name = "Kano State",                   Type = TopicType.Place,        Description = "Major northern commercial and historic metropolis." },
            new() { Id = "topic_rivers",   Slug = "rivers",         Name = "Rivers State",                 Type = TopicType.Place,        Description = "Niger Delta industrial and hydrocarbon hub." },

            // Themes
            new() { Id = "topic_fx",       Slug = "naira-exchange-rate", Name = "Naira Exchange Rate",      Type = TopicType.Theme,        Description = "Real-time updates on official and parallel market foreign exchange rates." },
            new() { Id = "topic_fuel",     Slug = "fuel-prices",    Name = "Fuel & Energy Prices",         Type = TopicType.Theme,        Description = "Petroleum pump price monitoring, refinery output, and energy policy." },
            new() { Id = "topic_tech",     Slug = "fintech-startups", Name = "Fintech & Startups",         Type = TopicType.Theme,        Description = "Venture investments, digital banking, and innovation ecosystem across Africa." }
        };

        db.Topics.AddRange(defaultTopics);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} canonical topics.", defaultTopics.Count);
    }
}
