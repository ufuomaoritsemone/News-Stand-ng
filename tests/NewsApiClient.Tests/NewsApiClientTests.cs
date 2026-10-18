using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NigerianNewsGrid.Client;
using Xunit;

namespace NigerianNewsGrid.Tests
{
    public class NewsApiClientTests
    {
        private class FakeHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;
            public FakeHandler(HttpResponseMessage response) => _response = response;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }

        [Fact]
        public async Task GetDailyBriefingAsync_ReturnsCategories_WhenApiReturnsOk()
        {
            var json = "[{\"category\":\"Top\",\"top\":[]}]";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var categories = await api.GetDailyBriefingAsync();
            Assert.NotNull(categories);
            Assert.Single(categories);
            Assert.Equal("Top", categories[0].Category);
        }

        [Fact]
        public async Task GetDailyBriefingAsync_ReturnsEmptyList_WhenApiReturnsError()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var categories = await api.GetDailyBriefingAsync();
            Assert.NotNull(categories);
            Assert.Empty(categories);
        }

        [Fact]
        public async Task GetVideoStoriesAsync_ReturnsStories_WhenApiReturnsOk()
        {
            var json = "[{\"id\":\"vid1\",\"videoId\":\"r8FW2pqYrq4\",\"title\":\"Breaking News Video\",\"summary\":\"Brief summary\",\"videoUrl\":\"https://youtube.com/watch?v=r8FW2pqYrq4\",\"thumbnailUrl\":\"https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg\",\"channelName\":\"Channels TV\",\"channelId\":\"channels_tv\",\"duration\":\"12:00\",\"category\":\"Politics\",\"publishedAt\":\"2026-08-13T10:00:00Z\"}]";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var stories = await api.GetVideoStoriesAsync();
            Assert.NotNull(stories);
            Assert.Single(stories);
            Assert.Equal("Breaking News Video", stories[0].Title);
            Assert.Equal("Channels TV", stories[0].ChannelName);
        }

        [Fact]
        public async Task GetSocialPostsAsync_ReturnsPosts_WhenApiReturnsOk()
        {
            var json = "[{\"id\":\"post1\",\"authorName\":\"Premium Times\",\"authorHandle\":\"@PremiumTimesng\",\"authorAvatarUrl\":\"https://avatar.url/img.jpg\",\"content\":\"Breaking update\",\"postUrl\":\"https://x.com/post1\",\"category\":\"Politics\",\"likesCount\":150,\"retweetsCount\":45,\"publishedAt\":\"2026-08-13T10:00:00Z\"}]";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var posts = await api.GetSocialPostsAsync();
            Assert.NotNull(posts);
            Assert.Single(posts);
            Assert.Equal("@PremiumTimesng", posts[0].AuthorHandle);
            Assert.Equal("Breaking update", posts[0].Content);
        }

        [Fact]
        public async Task GetRelatedStoriesAsync_ReturnsMultiSourceStories_WhenApiReturnsOk()
        {
            var json = "{\"articles\":[{\"id\":\"art1\",\"title\":\"Related Article 1\",\"category\":\"Politics\",\"source\":\"Punch\"}],\"videos\":[{\"id\":\"vid1\",\"title\":\"Related Video 1\",\"videoId\":\"r8FW2pqYrq4\",\"videoUrl\":\"https://youtube.com/watch?v=r8FW2pqYrq4\"}],\"socialPosts\":[{\"id\":\"post1\",\"authorHandle\":\"@channelstv\",\"content\":\"Related Tweet\"}]}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var result = await api.GetRelatedStoriesAsync("art0", "Politics", "Politics");
            Assert.NotNull(result);
            Assert.Single(result.Articles);
            Assert.Single(result.Videos);
            Assert.Single(result.SocialPosts);
            Assert.Equal(3, result.TotalCount);
            Assert.Equal("r8FW2pqYrq4", result.Videos[0].VideoId);
            Assert.Equal("@channelstv", result.SocialPosts[0].AuthorHandle);
        }

        [Fact]
        public async Task GetTrendingVideoStoriesAsync_ReturnsTrendingStories_WhenApiReturnsOk()
        {
            var json = "[{\"id\":\"vid_trend_1\",\"videoId\":\"r8FW2pqYrq4\",\"title\":\"🔥 Top Trending Broadcast\",\"summary\":\"Trending news story\",\"videoUrl\":\"https://youtube.com/watch?v=r8FW2pqYrq4\",\"thumbnailUrl\":\"https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg\",\"channelName\":\"Channels TV\",\"channelId\":\"channels_tv\",\"duration\":\"14:22\",\"category\":\"Politics\",\"publishedAt\":\"2026-08-16T12:00:00Z\",\"isTrending\":true,\"trendingRank\":1,\"viewCount\":250000,\"likeCount\":12000}]";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var stories = await api.GetTrendingVideoStoriesAsync(10, "Politics");
            Assert.NotNull(stories);
            Assert.Single(stories);
            Assert.Equal("🔥 Top Trending Broadcast", stories[0].Title);
            Assert.True(stories[0].IsTrending);
            Assert.Equal(1, stories[0].TrendingRank);
            Assert.Equal(250000, stories[0].ViewCount);
            Assert.Equal(12000, stories[0].LikeCount);
            Assert.Equal("14:22", stories[0].Duration);
        }

        [Fact]
        public async Task GetArticleByIdAsync_ReturnsArticleWithContent_WhenApiReturnsOk()
        {
            var json = "{\"id\":\"guid-1234\",\"title\":\"Headline Story\",\"content\":\"Paragraph 1\\n\\nParagraph 2\",\"summary\":\"Story Summary\",\"url\":\"https://punchng.com/story-1\",\"imageUrl\":\"https://punchng.com/img.jpg\",\"category\":\"Politics\",\"source\":\"The Punch\",\"author\":\"Reporter Name\",\"contentType\":\"News\"}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var article = await api.GetArticleByIdAsync("guid-1234");

            Assert.NotNull(article);
            Assert.Equal("guid-1234", article.Id);
            Assert.Equal("Headline Story", article.Title);
            Assert.Equal("Paragraph 1\n\nParagraph 2", article.Content);
            Assert.Equal("Story Summary", article.Summary);
            Assert.Equal("The Punch", article.Source);
            Assert.Equal("Reporter Name", article.Author);
        }

        [Fact]
        public async Task GetArticleByIdAsync_ReturnsNull_WhenApiReturnsNotFound()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound);
            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var article = await api.GetArticleByIdAsync("non-existent-guid");

            Assert.Null(article);
        }

        [Fact]
        public async Task GetArticleByIdAsync_ReturnsNull_WhenIdIsNullOrWhitespace()
        {
            var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK));
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var articleNull = await api.GetArticleByIdAsync(null!);
            var articleEmpty = await api.GetArticleByIdAsync("   ");

            Assert.Null(articleNull);
            Assert.Null(articleEmpty);
        }

        [Fact]
        public async Task GetStoryClusterBySlugAsync_ReturnsClusterDetail_WhenApiReturnsOk()
        {
            var json = "{\"id\":\"cluster-1\",\"slug\":\"cbn-monetary-policy-update\",\"title\":\"CBN Holds MPR Steady\",\"summary\":\"Executive briefing summary\",\"sourceCount\":2,\"articleCount\":2,\"sources\":[\"The Punch\",\"Premium Times\"],\"articles\":[{\"articleId\":\"art-1\",\"title\":\"Punch Report\",\"url\":\"https://punchng.com/cbn\",\"source\":\"The Punch\",\"publishedAt\":\"2026-10-18T00:00:00Z\",\"similarityScore\":0.95}]}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var cluster = await api.GetStoryClusterBySlugAsync("cbn-monetary-policy-update");

            Assert.NotNull(cluster);
            Assert.Equal("cluster-1", cluster!.Id);
            Assert.Equal("CBN Holds MPR Steady", cluster.Title);
            Assert.Equal(2, cluster.SourceCount);
            Assert.Single(cluster.Articles);
            Assert.Equal("The Punch", cluster.Articles[0].Source);
        }

        [Fact]
        public async Task GetMarketMetricAsync_ReturnsNairaFxMetric_WhenApiReturnsOk()
        {
            var json = "{\"metricKey\":\"naira-fx\",\"title\":\"Naira Exchange Rate Today\",\"primaryValue\":\"₦1,655/$\",\"secondaryValue\":\"CBN NAFEM: ₦1,590/$\",\"sourceAttribution\":\"CBN & AbokiFX\",\"currencies\":{\"USD\":{\"code\":\"USD\",\"name\":\"US Dollar\",\"symbol\":\"$\",\"parallelBuy\":1640,\"parallelSell\":1655,\"officialBuy\":1580,\"officialSell\":1590}}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var handler = new FakeHandler(response);
            var client = new HttpClient(handler)
            {
                BaseAddress = new System.Uri("http://localhost")
            };

            var api = new NigerianNewsGrid.Client.NewsApiClient(client);
            var metric = await api.GetMarketMetricAsync("naira-fx");

            Assert.NotNull(metric);
            Assert.Equal("naira-fx", metric!.MetricKey);
            Assert.Equal("₦1,655/$", metric.PrimaryValue);
            Assert.Equal("CBN NAFEM: ₦1,590/$", metric.SecondaryValue);
            Assert.NotNull(metric.Currencies);
            Assert.True(metric.Currencies!.ContainsKey("USD"));
            Assert.Equal(1655m, metric.Currencies["USD"].ParallelSell);
        }
    }
}
