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
    ISlugService? slugService = null,
    IDataSeeder? dataSeeder = null) : IDbInitializer
{
    private readonly IDataSeeder _dataSeeder = dataSeeder ?? new DataSeeder(db, logger, slugService);
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
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
            await _dataSeeder.SeedAsync(cancellationToken);
        }
        finally
        {
            _initLock.Release();
        }
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
            bool hasSeoSlugsAndTopics = false;

            if (isSqlite)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Articles';";
                var result = await cmd.ExecuteScalarAsync(ct);
                hasLegacyTables = Convert.ToInt32(result) > 0;

                if (hasLegacyTables)
                {
                    using var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = """
                        SELECT (
                            (SELECT COUNT(*) FROM pragma_table_info('Sources') WHERE name='Slug') +
                            (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Topics')
                        );
                        """;
                    var checkRes = await checkCmd.ExecuteScalarAsync(ct);
                    hasSeoSlugsAndTopics = Convert.ToInt32(checkRes) > 0;
                }
            }
            else if (isPostgres)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Articles';";
                var result = await cmd.ExecuteScalarAsync(ct);
                hasLegacyTables = Convert.ToInt32(result) > 0;

                if (hasLegacyTables)
                {
                    using var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = """
                        SELECT (
                            (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND lower(table_name) = 'sources' AND lower(column_name) = 'slug') +
                            (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND lower(table_name) = 'topics')
                        );
                        """;
                    var checkRes = await checkCmd.ExecuteScalarAsync(ct);
                    hasSeoSlugsAndTopics = Convert.ToInt32(checkRes) > 0;
                }
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

                    if (hasSeoSlugsAndTopics)
                    {
                        histCmd.CommandText += """

                            INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                            VALUES ('20261003204804_AddSeoSlugsAndTopics', '10.0.11');
                            """;
                    }

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

                    if (hasSeoSlugsAndTopics)
                    {
                        histCmd.CommandText += """

                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                            VALUES ('20261003204804_AddSeoSlugsAndTopics', '10.0.11')
                            ON CONFLICT DO NOTHING;
                            """;
                    }

                    await histCmd.ExecuteNonQueryAsync(ct);
                }
                logger.LogInformation("Legacy database schema baselined (hasSeoSlugsAndTopics: {HasSeoSlugsAndTopics}).", hasSeoSlugsAndTopics);
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
                    CREATE INDEX IF NOT EXISTS "IX_UserEvents_ArticleId" ON "UserEvents" ("ArticleId");
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

                    CREATE TABLE IF NOT EXISTS "SystemSettings" (
                        "Key" character varying(150) NOT NULL,
                        "Value" text NULL,
                        "Category" character varying(50) NOT NULL DEFAULT 'General',
                        "Description" text NULL,
                        "IsSecret" boolean NOT NULL DEFAULT false,
                        "UpdatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_SystemSettings" PRIMARY KEY ("Key")
                    );
                    CREATE INDEX IF NOT EXISTS "IX_SystemSettings_Category" ON "SystemSettings" ("Category");
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
                    CREATE INDEX IF NOT EXISTS "IX_UserEvents_ArticleId" ON "UserEvents" ("ArticleId");
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

                    CREATE TABLE IF NOT EXISTS "SystemSettings" (
                        "Key" TEXT NOT NULL CONSTRAINT "PK_SystemSettings" PRIMARY KEY,
                        "Value" TEXT NULL,
                        "Category" TEXT NOT NULL DEFAULT 'General',
                        "Description" TEXT NULL,
                        "IsSecret" INTEGER NOT NULL DEFAULT 0,
                        "UpdatedAt" TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_SystemSettings_Category" ON "SystemSettings" ("Category");
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
        await AddColumnIfMissingAsync("Articles", "PlacementZone", "TEXT NOT NULL DEFAULT 'MainPage_InFeed'", ct);
        await AddColumnIfMissingAsync("Articles", "TargetPlatform", "TEXT NOT NULL DEFAULT 'All'", ct);
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
}
