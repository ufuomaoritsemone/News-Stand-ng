using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Android.Content;
using Android.Graphics;
using Bitmap = Android.Graphics.Bitmap;
using Paint = Android.Graphics.Paint;
using RectF = Android.Graphics.RectF;
using Matrix = Android.Graphics.Matrix;
using File = System.IO.File;
using Path = System.IO.Path;

namespace NigerianNewGrid.Platforms.Android;

/// <summary>
/// Helper for downloading, downsampling, rounding, and caching thumbnails for the Android Home Screen Widget.
/// Operates on background binder threads to avoid IPC transaction overflow and UI stutter.
/// </summary>
public static class WidgetImageHelper
{
    private static readonly ConcurrentDictionary<string, Bitmap> MemoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private const int MaxMemoryCacheEntries = 25;

    /// <summary>
    /// Gets a downscaled and rounded corner Bitmap for a given image URL, checking memory cache and disk cache before downloading.
    /// </summary>
    public static Bitmap? GetOrDownloadThumbnailBitmap(Context context, string? imageUrl, int targetSizeDp = 77)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // 1. Check in-memory cache
        if (MemoryCache.TryGetValue(imageUrl, out var cachedBitmap) && cachedBitmap != null && !cachedBitmap.IsRecycled)
        {
            return cachedBitmap;
        }

        // Calculate target dimensions in pixels based on screen density
        var density = context.Resources?.DisplayMetrics?.Density ?? 2.5f;
        var targetPx = Math.Max(72, (int)(targetSizeDp * density));
        // Hard-cap pixel size to protect Android RemoteViews 1MB binder limit (increased proportionally for 77dp)
        targetPx = Math.Min(targetPx, 220);

        var cacheDir = Path.Combine(context.CacheDir?.AbsolutePath ?? Path.GetTempPath(), "widget_thumbs");
        var hash = ComputeHash(imageUrl);
        var diskCacheFile = Path.Combine(cacheDir, $"{hash}.png");

        try
        {
            // 2. Check disk cache
            if (File.Exists(diskCacheFile))
            {
                var diskBitmap = BitmapFactory.DecodeFile(diskCacheFile);
                if (diskBitmap != null)
                {
                    CacheInMemory(imageUrl, diskBitmap);
                    return diskBitmap;
                }
            }

            // 3. Download from network
            Directory.CreateDirectory(cacheDir);
            var response = HttpClient.GetAsync(uri).ConfigureAwait(false).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var imageBytes = response.Content.ReadAsByteArrayAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return null;
            }

            // 4. Sub-sample to avoid high memory allocation
            var boundsOptions = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length, boundsOptions);

            var sampleSize = 1;
            if (boundsOptions.OutHeight > targetPx || boundsOptions.OutWidth > targetPx)
            {
                var halfHeight = boundsOptions.OutHeight / 2;
                var halfWidth = boundsOptions.OutWidth / 2;
                while ((halfHeight / sampleSize) >= targetPx && (halfWidth / sampleSize) >= targetPx)
                {
                    sampleSize *= 2;
                }
            }

            var decodeOptions = new BitmapFactory.Options
            {
                InJustDecodeBounds = false,
                InSampleSize = sampleSize,
                InPreferredConfig = Bitmap.Config.Argb8888
            };

            using var rawBitmap = BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length, decodeOptions);
            if (rawBitmap == null)
            {
                return null;
            }

            // 5. Create center-cropped square with rounded corners
            var roundedBitmap = CreateRoundedThumbnail(rawBitmap, targetPx, 10f * density);
            if (roundedBitmap == null)
            {
                return null;
            }

            // 6. Save to disk cache for fast offline / repeated loads
            try
            {
                using var fs = File.Open(diskCacheFile, FileMode.Create, FileAccess.Write, FileShare.None);
                roundedBitmap.Compress(Bitmap.CompressFormat.Png!, 90, fs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WidgetImageHelper] Disk cache write error: {ex.Message}");
            }

            // 7. Store in memory cache
            CacheInMemory(imageUrl, roundedBitmap);
            return roundedBitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetImageHelper] Error fetching image '{imageUrl}': {ex.Message}");
            return null;
        }
    }

    private static Bitmap? CreateRoundedThumbnail(Bitmap source, int targetSize, float cornerRadiusPx)
    {
        try
        {
            var output = Bitmap.CreateBitmap(targetSize, targetSize, Bitmap.Config.Argb8888!);
            if (output == null) return null;

            using var canvas = new Canvas(output);
            using var paint = new Paint(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);

            var rectF = new RectF(0, 0, targetSize, targetSize);
            canvas.DrawRoundRect(rectF, cornerRadiusPx, cornerRadiusPx, paint);

            paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.SrcIn));

            // Compute center-crop transform matrix
            float scale;
            float dx = 0;
            float dy = 0;

            if (source.Width * targetSize > targetSize * source.Height)
            {
                scale = (float)targetSize / source.Height;
                dx = (targetSize - (source.Width * scale)) * 0.5f;
            }
            else
            {
                scale = (float)targetSize / source.Width;
                dy = (targetSize - (source.Height * scale)) * 0.5f;
            }

            using var matrix = new Matrix();
            matrix.SetScale(scale, scale);
            matrix.PostTranslate(dx, dy);

            canvas.DrawBitmap(source, matrix, paint);
            return output;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetImageHelper] Rounded thumbnail generation failed: {ex.Message}");
            return null;
        }
    }

    private static void CacheInMemory(string key, Bitmap bitmap)
    {
        if (MemoryCache.Count >= MaxMemoryCacheEntries)
        {
            MemoryCache.Clear();
        }

        MemoryCache[key] = bitmap;
    }

    private static string ComputeHash(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
