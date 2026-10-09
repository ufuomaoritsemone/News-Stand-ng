using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Services;

public class ArticleSeoBackfillService(
    NewsDbContext db,
    ISlugService slugService,
    ILogger<ArticleSeoBackfillService> logger) : IArticleSeoBackfillService
{
    public async Task<BackfillSeoResultDto> BackfillArticleSeoAsync(int batchSize = 500, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var safeBatchSize = Math.Clamp(batchSize, 10, 1000);
        int articlesProcessed = 0;
        int slugsGenerated = 0;
        int excerptsGenerated = 0;
        int sourcesProcessed = 0;
        int sourceSlugsGenerated = 0;

        logger.LogInformation("Starting idempotent SEO backfill for articles and sources (batch size: {BatchSize})...", safeBatchSize);

        // 1. Backfill Sources without Slugs
        var sourcesWithoutSlugs = await db.Sources
            .Where(s => string.IsNullOrEmpty(s.Slug))
            .ToListAsync(cancellationToken);

        if (sourcesWithoutSlugs.Count > 0)
        {
            var usedSourceSlugs = (await db.Sources
                .Where(s => !string.IsNullOrEmpty(s.Slug))
                .Select(s => s.Slug!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var src in sourcesWithoutSlugs)
            {
                sourcesProcessed++;
                var baseSlug = slugService.GenerateSlug(string.IsNullOrWhiteSpace(src.Name) ? src.Id : src.Name, 60);
                var candidate = baseSlug;
                int counter = 1;
                while (usedSourceSlugs.Contains(candidate))
                {
                    candidate = $"{baseSlug}-{counter++}";
                }

                src.Slug = candidate;
                usedSourceSlugs.Add(candidate);
                sourceSlugsGenerated++;
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Source SEO backfill complete: {Count} sources assigned slugs.", sourceSlugsGenerated);
        }

        // 2. Batch-process Articles without Slugs or Excerpts
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await db.Articles
                .Where(a => string.IsNullOrEmpty(a.Slug) || string.IsNullOrEmpty(a.Excerpt))
                .OrderBy(a => a.PublishedAt)
                .Take(safeBatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            // Track slugs in current batch to prevent intra-batch collision
            var batchSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var article in batch)
            {
                articlesProcessed++;
                bool modified = false;

                if (string.IsNullOrWhiteSpace(article.Slug))
                {
                    var newSlug = slugService.GenerateArticleSlug(article.Title, article.Id);
                    int attempts = 0;
                    while (batchSlugs.Contains(newSlug) && attempts < 5)
                    {
                        newSlug = slugService.GenerateArticleSlug(article.Title, null);
                        attempts++;
                    }

                    article.Slug = newSlug;
                    batchSlugs.Add(newSlug);
                    slugsGenerated++;
                    modified = true;
                }

                if (string.IsNullOrWhiteSpace(article.Excerpt))
                {
                    article.Excerpt = slugService.GenerateExcerpt(article.Content, article.Summary, 300);
                    excerptsGenerated++;
                    modified = true;
                }

                if (modified)
                {
                    article.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Processed SEO batch: {Count} articles (total processed: {TotalProcessed}).", batch.Count, articlesProcessed);

            // Detach tracked entities to release memory
            db.ChangeTracker.Clear();

            if (batch.Count < safeBatchSize)
            {
                break;
            }
        }

        stopwatch.Stop();
        var message = $"SEO backfill finished in {stopwatch.Elapsed.TotalSeconds:F1}s: {articlesProcessed} articles processed ({slugsGenerated} slugs, {excerptsGenerated} excerpts), {sourcesProcessed} sources processed.";
        logger.LogInformation("{Message}", message);

        return new BackfillSeoResultDto
        {
            ArticlesProcessed = articlesProcessed,
            SlugsGenerated = slugsGenerated,
            ExcerptsGenerated = excerptsGenerated,
            SourcesProcessed = sourcesProcessed,
            SourceSlugsGenerated = sourceSlugsGenerated,
            Duration = stopwatch.Elapsed,
            Message = message
        };
    }
}
