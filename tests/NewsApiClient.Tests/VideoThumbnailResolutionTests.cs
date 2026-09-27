using NewsApi.Infrastructure;
using NewsApi.Services;
using NigerianNewsGrid.Client.Models;
using Xunit;

namespace NewsApiClient.Tests;

public class VideoThumbnailResolutionTests
{
    [Fact]
    public void ResolveBestThumbnail_PrioritizesMaxRes_WhenPresent()
    {
        var thumbnails = new YouTubeVideoThumbnails
        {
            Default  = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/default.jpg" },
            Medium   = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/mqdefault.jpg" },
            High     = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/hqdefault.jpg" },
            Standard = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/sddefault.jpg" },
            MaxRes   = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/maxresdefault.jpg" },
        };

        var resolved = YouTubeFeedService.ResolveBestThumbnail(thumbnails, "abc123", "Politics");

        Assert.Equal("https://i.ytimg.com/vi/abc123/maxresdefault.jpg", resolved);
    }

    [Fact]
    public void ResolveBestThumbnail_PrioritizesStandard_WhenMaxResAbsent()
    {
        var thumbnails = new YouTubeVideoThumbnails
        {
            Default  = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/default.jpg" },
            Medium   = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/mqdefault.jpg" },
            High     = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/hqdefault.jpg" },
            Standard = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/sddefault.jpg" },
            MaxRes   = null,
        };

        var resolved = YouTubeFeedService.ResolveBestThumbnail(thumbnails, "abc123", "Politics");

        Assert.Equal("https://i.ytimg.com/vi/abc123/sddefault.jpg", resolved);
    }

    [Fact]
    public void ResolveBestThumbnail_PrioritizesHigh_WhenMaxResAndStandardAbsent()
    {
        var thumbnails = new YouTubeVideoThumbnails
        {
            Default  = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/default.jpg" },
            Medium   = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/mqdefault.jpg" },
            High     = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/hqdefault.jpg" },
        };

        var resolved = YouTubeFeedService.ResolveBestThumbnail(thumbnails, "abc123", "Politics");

        Assert.Equal("https://i.ytimg.com/vi/abc123/hqdefault.jpg", resolved);
    }

    [Fact]
    public void ResolveBestThumbnail_PrioritizesMedium_WhenOnlyMediumAndDefaultPresent()
    {
        var thumbnails = new YouTubeVideoThumbnails
        {
            Default  = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/default.jpg" },
            Medium   = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/mqdefault.jpg" },
        };

        var resolved = YouTubeFeedService.ResolveBestThumbnail(thumbnails, "abc123", "Politics");

        Assert.Equal("https://i.ytimg.com/vi/abc123/mqdefault.jpg", resolved);
    }

    [Fact]
    public void ResolveBestThumbnail_UsesDefault_WhenOnlyDefaultPresent()
    {
        var thumbnails = new YouTubeVideoThumbnails
        {
            Default  = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/default.jpg" },
        };

        var resolved = YouTubeFeedService.ResolveBestThumbnail(thumbnails, "abc123", "Politics");

        Assert.Equal("https://i.ytimg.com/vi/abc123/default.jpg", resolved);
    }

    [Fact]
    public void ResolveBestThumbnail_DirectUrlOverride_TakesPrecedenceWhenValid()
    {
        var directUrl = "https://custom.cdn.com/thumbnail.png";
        var thumbnails = new YouTubeVideoThumbnails
        {
            MaxRes = new YouTubeThumbnailInfo { Url = "https://i.ytimg.com/vi/abc123/maxresdefault.jpg" },
        };

        var resolved = YouTubeFeedService.ResolveBestThumbnail(directUrl, thumbnails, "abc123", "Politics");

        Assert.Equal(directUrl, resolved);
    }

    [Fact]
    public void ResolveBestThumbnail_FallsBackToDeterministicYtCdn_WhenThumbnailsNull()
    {
        var resolved = YouTubeFeedService.ResolveBestThumbnail(null, "xyz987", "Politics");

        Assert.Equal("https://i.ytimg.com/vi/xyz987/hqdefault.jpg", resolved);
    }

    [Fact]
    public void ResolveBestThumbnail_FallsBackToCategory_WhenThumbnailsNullAndVideoIdEmpty()
    {
        var resolved = YouTubeFeedService.ResolveBestThumbnail(null, "", "Sports");

        Assert.Equal(CategoryImageMap.Resolve(null, "Sports"), resolved);
    }

    [Fact]
    public void VideoStoryItem_DisplayThumbnailUrl_PrioritizesThumbnailUrl()
    {
        var item = new VideoStoryItem
        {
            VideoId = "vid123",
            ThumbnailUrl = "https://images.example.com/custom.jpg"
        };

        Assert.Equal("https://images.example.com/custom.jpg", item.DisplayThumbnailUrl);
    }

    [Fact]
    public void VideoStoryItem_DisplayThumbnailUrl_FallsBackToDeterministicCdn_WhenThumbnailUrlEmpty()
    {
        var item = new VideoStoryItem
        {
            VideoId = "vid123",
            ThumbnailUrl = ""
        };

        Assert.Equal("https://i.ytimg.com/vi/vid123/hqdefault.jpg", item.DisplayThumbnailUrl);
    }

    [Fact]
    public void VideoStoryItem_DisplayThumbnailUrl_FallsBackToDefaultNewsImage_WhenBothEmpty()
    {
        var item = new VideoStoryItem
        {
            VideoId = "",
            ThumbnailUrl = ""
        };

        Assert.Equal("https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600&auto=format&fit=crop&q=80", item.DisplayThumbnailUrl);
    }

    [Fact]
    public void VideoStoryItem_DisplayThumbnailUrl_UpgradesHttpToHttps()
    {
        var item = new VideoStoryItem
        {
            VideoId = "vid123",
            ThumbnailUrl = "http://i.ytimg.com/vi/vid123/hqdefault.jpg"
        };

        Assert.Equal("https://i.ytimg.com/vi/vid123/hqdefault.jpg", item.DisplayThumbnailUrl);
    }

    [Fact]
    public void ResolveBestThumbnail_DirectUrlOverride_UpgradesHttpToHttps()
    {
        var directUrl = "http://custom.cdn.com/thumbnail.png";
        var resolved = YouTubeFeedService.ResolveBestThumbnail(directUrl, null, "abc123", "Politics");

        Assert.Equal("https://custom.cdn.com/thumbnail.png", resolved);
    }

    [Fact]
    public void ResolveBestThumbnail_Candidate_UpgradesHttpToHttps()
    {
        var thumbnails = new YouTubeVideoThumbnails
        {
            High = new YouTubeThumbnailInfo { Url = "http://i.ytimg.com/vi/abc123/hqdefault.jpg" }
        };
        var resolved = YouTubeFeedService.ResolveBestThumbnail(null, thumbnails, "abc123", "Politics");

        Assert.Equal("https://i.ytimg.com/vi/abc123/hqdefault.jpg", resolved);
    }
}

