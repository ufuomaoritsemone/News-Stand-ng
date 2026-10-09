using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/img")]
[Route("api/img")]
public class ImageProxyController(
    IHttpClientFactory httpClientFactory,
    IWebHostEnvironment env,
    ILogger<ImageProxyController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "punchng.com",
        "vanguardngr.com",
        "premiumtimesng.com",
        "guardian.ng",
        "thenationonlineng.net",
        "dailypost.ng",
        "channelstv.com",
        "tribuneonlineng.com",
        "thecable.ng",
        "saharareporters.com",
        "businessday.ng",
        "sunnewsonline.com",
        "leadership.ng",
        "dailytrust.com",
        "thisdaylive.com",
        "peoplesdailyng.com",
        "blueprint.ng",
        "nairametrics.com",
        "technext24.com",
        "techcabal.com",
        "techpoint.africa",
        "nan.ng",
        "von.gov.ng",
        "arise.tv",
        "ariseplay.com",
        "images.unsplash.com",
        "unsplash.com",
        "cdn.punchng.com",
        "wp.com",
        "i0.wp.com",
        "ytimg.com",
        "youtube.com",
        "s.w.org",
        "cloudfront.net",
        "cloudinary.com",
        "newsstand.ng",
        "nigeriannewsgrid.com",
        "bbci.co.uk",
        "aljazeera.net",
        "reuters.com"
    };

    [HttpGet]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetOptimizedImage(
        [FromQuery] string url,
        [FromQuery] int? w = 800,
        [FromQuery] int? q = 80,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("Image URL is required.");
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var targetUri) ||
            (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest("Invalid image URL scheme. Only HTTP and HTTPS are permitted.");
        }

        // SSRF Guard: Verify host is not loopback/private and matches allowed publisher domains
        if (!IsAllowedHost(targetUri.Host))
        {
            logger.LogWarning("Blocked image proxy request to unauthorized host: {Host}", targetUri.Host);
            return StatusCode(StatusCodes.Status403Forbidden, "Target host is not permitted by proxy policy.");
        }

        var safeWidth = Math.Clamp(w ?? 800, 64, 1920);
        var safeQuality = Math.Clamp(q ?? 80, 20, 100);

        var cacheKey = ComputeHash($"{targetUri}_{safeWidth}_{safeQuality}");
        var cacheDir = Path.Combine(env.ContentRootPath, "data", "img_cache");
        Directory.CreateDirectory(cacheDir);
        var cachedFilePath = Path.Combine(cacheDir, $"{cacheKey}.webp");

        // Serve cached file if present
        if (System.IO.File.Exists(cachedFilePath))
        {
            var eTag = $"\"{cacheKey}\"";
            if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            Response.Headers.Append("ETag", eTag);
            Response.Headers.Append("Cache-Control", "public, max-age=604800, s-maxage=2592000");
            return PhysicalFile(cachedFilePath, "image/webp");
        }

        try
        {
            var client = httpClientFactory.CreateClient("ImageProxy");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var response = await client.GetAsync(targetUri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Upstream image fetch failed with status {Status} for {Url}", response.StatusCode, url);
                return StatusCode((int)response.StatusCode, "Upstream image could not be retrieved.");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Upstream resource is not a valid image format.");
            }

            await using var sourceStream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var image = await Image.LoadAsync(sourceStream, cts.Token);

            // Resize only if original image is wider than target width
            if (image.Width > safeWidth)
            {
                var targetHeight = (int)Math.Round((double)image.Height * safeWidth / image.Width);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(safeWidth, targetHeight),
                    Mode = ResizeMode.Max
                }));
            }

            var encoder = new WebpEncoder
            {
                Quality = safeQuality,
                FileFormat = WebpFileFormatType.Lossy
            };

            await using (var fileStream = new FileStream(cachedFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await image.SaveAsync(fileStream, encoder, cts.Token);
            }

            var fileETag = $"\"{cacheKey}\"";
            Response.Headers.Append("ETag", fileETag);
            Response.Headers.Append("Cache-Control", "public, max-age=604800, s-maxage=2592000");

            return PhysicalFile(cachedFilePath, "image/webp");
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, "Image download timed out.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to optimize image: {Url}", url);
            return StatusCode(StatusCodes.Status502BadGateway, "Image processing failure.");
        }
    }

    private static bool IsAllowedHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        // Block localhost and loopbacks
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1") ||
            host.Equals("::1"))
        {
            return false;
        }

        // Block private IP address ranges
        if (IPAddress.TryParse(host, out var ip))
        {
            var bytes = ip.GetAddressBytes();
            if (ip.IsIPv4MappedToIPv6) return false;
            // 10.0.0.0/8
            if (bytes[0] == 10) return false;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return false;
            // 169.254.0.0/16 (Link Local)
            if (bytes[0] == 169 && bytes[1] == 254) return false;
        }

        return AllowedDomains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                                            host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes)[..16];
    }
}
