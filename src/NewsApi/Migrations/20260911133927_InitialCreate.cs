using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AudioUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsSponsored = table.Column<bool>(type: "INTEGER", nullable: false),
                    SponsorName = table.Column<string>(type: "TEXT", nullable: true),
                    SponsorUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CampaignExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImpressionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ClickCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArticleId = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Briefings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Briefings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryCorrections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ArticleId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    OldCategory = table.Column<string>(type: "TEXT", nullable: false),
                    NewCategory = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryCorrections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    UserEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsEmailSent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialHandles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Handle = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    ProfileUrl = table.Column<string>(type: "TEXT", nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Bio = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialHandles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialPosts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorHandle = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorAvatarUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    PostUrl = table.Column<string>(type: "TEXT", nullable: false),
                    MediaUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    LikesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RetweetsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RssUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SitemapUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ScraperType = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ArticleId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ArticleTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoChannels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", nullable: false),
                    YoutubeChannelId = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoStories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    VideoId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    VideoUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsTrending = table.Column<bool>(type: "INTEGER", nullable: false),
                    TrendingRank = table.Column<int>(type: "INTEGER", nullable: true),
                    ViewCount = table.Column<long>(type: "INTEGER", nullable: false),
                    LikeCount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoStories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CampaignExpiresAt",
                table: "Articles",
                column: "CampaignExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Category",
                table: "Articles",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_IsPinned",
                table: "Articles",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_IsSponsored",
                table: "Articles",
                column: "IsSponsored");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_PublishedAt",
                table: "Articles",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Source",
                table: "Articles",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Title",
                table: "Articles",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Url",
                table: "Articles",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_AudioAssets_ArticleId",
                table: "AudioAssets",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryCorrections_ArticleId",
                table: "CategoryCorrections",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryCorrections_CreatedAt",
                table: "CategoryCorrections",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_Category",
                table: "Feedbacks",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_CreatedAt",
                table: "Feedbacks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_AuthorHandle",
                table: "SocialPosts",
                column: "AuthorHandle");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_PublishedAt",
                table: "SocialPosts",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserEvents_DeviceId",
                table: "UserEvents",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEvents_EventType",
                table: "UserEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_UserEvents_OccurredAt",
                table: "UserEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_VideoStories_ChannelId",
                table: "VideoStories",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoStories_IsTrending",
                table: "VideoStories",
                column: "IsTrending");

            migrationBuilder.CreateIndex(
                name: "IX_VideoStories_PublishedAt",
                table: "VideoStories",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VideoStories_TrendingRank",
                table: "VideoStories",
                column: "TrendingRank");

            migrationBuilder.CreateIndex(
                name: "IX_VideoStories_VideoId",
                table: "VideoStories",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "AudioAssets");

            migrationBuilder.DropTable(
                name: "Briefings");

            migrationBuilder.DropTable(
                name: "CategoryCorrections");

            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "SocialHandles");

            migrationBuilder.DropTable(
                name: "SocialPosts");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "UserEvents");

            migrationBuilder.DropTable(
                name: "VideoChannels");

            migrationBuilder.DropTable(
                name: "VideoStories");
        }
    }
}
