using NigerianNewsGrid.Client.Helpers;
using Xunit;

namespace NewsApiClient.Tests;

public class XShareFormatterTests
{
    [Theory]
    [InlineData("Punch", "@MobilePunch")]
    [InlineData("the punch", "@MobilePunch")]
    [InlineData("Vanguard", "@vanguardngrnews")]
    [InlineData("Channels TV", "@channelstv")]
    [InlineData("The Guardian", "@GuardianNigeria")]
    [InlineData("Premium Times", "@PremiumTimesng")]
    [InlineData("Daily Post", "@DailyPostNGR")]
    [InlineData("TheCable", "@thecableng")]
    [InlineData("Arise News", "@ARISEtv")]
    [InlineData("Unknown Paper", "via Unknown Paper")]
    public void MapSourceToHandle_MapsCorrectly(string source, string expectedHandle)
    {
        var result = XShareFormatter.MapSourceToHandle(source);
        Assert.Equal(expectedHandle, result);
    }

    [Fact]
    public void MapSourceToHandle_NullOrEmpty_ReturnsDefault()
    {
        Assert.Equal("News Stand NG", XShareFormatter.MapSourceToHandle(null));
        Assert.Equal("News Stand NG", XShareFormatter.MapSourceToHandle("   "));
    }

    [Fact]
    public void BuildTweetText_ContainsHeadlineSourceAndAppCta()
    {
        var title = "FG Approves Major Upgrade for Power Transmission Grid";
        var source = "Punch";
        var appStoreUrl = "https://play.google.com/store/apps/details?id=com.nigeriannewsgrid.app";
        var storyUrl = "https://punchng.com/fg-approves-power-upgrade";

        var tweet = XShareFormatter.BuildTweetText(title, source, appStoreUrl, storyUrl);

        Assert.Contains("FG Approves Major Upgrade", tweet);
        Assert.Contains("Via @MobilePunch", tweet);
        Assert.Contains(XShareFormatter.DefaultAppCta, tweet);
        Assert.Contains(appStoreUrl, tweet);
    }

    [Fact]
    public void BuildTweetText_LongHeadline_TruncatedSafely()
    {
        var longTitle = new string('A', 300);
        var source = "Channels TV";
        var appStoreUrl = "https://play.google.com/store/apps/details?id=com.nigeriannewsgrid.app";
        var storyUrl = "https://channelstv.com/story";

        var tweet = XShareFormatter.BuildTweetText(longTitle, source, appStoreUrl, storyUrl);

        Assert.EndsWith(appStoreUrl, tweet);
        Assert.Contains("...", tweet);
        Assert.Contains("Via @channelstv", tweet);
        // Ensure the headline was trimmed well below 300
        Assert.True(tweet.Length <= 280);
    }

    [Fact]
    public void BuildWebIntentUri_GeneratesValidHttpsUri()
    {
        var tweet = "Breaking News Story\n\nVia @MobilePunch";
        var storyUrl = "https://punchng.com/news/123";

        var uri = XShareFormatter.BuildWebIntentUri(tweet, storyUrl);

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("x.com", uri.Host);
        Assert.Equal("/intent/tweet", uri.AbsolutePath);
        Assert.Contains("text=", uri.Query);
        Assert.Contains("url=", uri.Query);
    }

    [Fact]
    public void BuildNativeIntentUri_GeneratesTwitterSchemeUri()
    {
        var tweet = "Breaking News Story\n\nVia @channelstv";
        var storyUrl = "https://channelstv.com/news/123";

        var uri = XShareFormatter.BuildNativeIntentUri(tweet, storyUrl);

        Assert.Equal("twitter", uri.Scheme);
        Assert.Equal("post", uri.Host);
        Assert.Contains("message=", uri.Query);
    }

    [Fact]
    public void BuildTweetText_EmptyStoreUrl_OmitsAppCtaAndStoreLink()
    {
        var title = "FG Approves Major Upgrade for Power Transmission Grid";
        var source = "Punch";

        var tweet = XShareFormatter.BuildTweetText(title, source, appDownloadUrl: "");

        Assert.Contains("FG Approves Major Upgrade", tweet);
        Assert.Contains("Via @MobilePunch", tweet);
        Assert.DoesNotContain(XShareFormatter.DefaultAppCta, tweet);
        Assert.DoesNotContain("play.google.com", tweet);
    }

    [Fact]
    public void AppStoreConstants_DefaultValues_AreConfigurableEmptyStrings()
    {
        // Verified by default: empty so share tweets don't include dead store links until published
        Assert.True(string.IsNullOrEmpty(AppStoreConstants.GooglePlayStoreUrl));
        Assert.True(string.IsNullOrEmpty(AppStoreConstants.AppleAppStoreUrl));
        Assert.False(AppStoreConstants.HasGooglePlayStoreUrl);
        Assert.False(AppStoreConstants.HasAppleAppStoreUrl);
    }
}
