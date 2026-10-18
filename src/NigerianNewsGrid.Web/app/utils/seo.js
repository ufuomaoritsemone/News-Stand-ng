/**
 * Shared SEO helpers for SSR routes (canonical URLs, absolute images, JSON-LD builders).
 * Keeps structured data consistent across home, category, topic, source, story and article pages.
 */

export const SITE_URL = "https://newsstand.ng";
export const DEFAULT_OG_IMAGE = `${SITE_URL}/newsstand-masthead.png`;

/** Thin-content threshold — must match SeoController.MinTopicArticlesForIndexing in NewsApi. */
export const MIN_TOPIC_ARTICLES_FOR_INDEXING = 5;

/**
 * Normalizes an image URL to an absolute https URL (required by Google for JSON-LD and OG images).
 * @param {string|null|undefined} url
 * @param {string} [fallback]
 */
export function toAbsoluteImageUrl(url, fallback = DEFAULT_OG_IMAGE) {
  if (!url || typeof url !== "string") return fallback;
  const trimmed = url.trim();
  if (!trimmed) return fallback;
  if (trimmed.startsWith("//")) return `https:${trimmed}`;
  if (trimmed.startsWith("/")) return `${SITE_URL}${trimmed}`;
  if (/^http:\/\//i.test(trimmed)) return trimmed.replace(/^http:\/\//i, "https://");
  if (/^https:\/\//i.test(trimmed)) return trimmed;
  return fallback;
}

/**
 * Builds a self-referencing canonical for paginated hubs. Page 2+ keeps its own canonical
 * (never canonicalize page 2+ back to page 1 — seo-news SKILL §2.1).
 */
export function paginatedUrl(path, page) {
  return `${SITE_URL}${path}${page > 1 ? `?page=${page}` : ""}`;
}

/** Returns rel=prev / rel=next link descriptors for React Router meta(). */
export function paginationLinks(path, page, totalPages) {
  const links = [];
  if (page > 1) {
    links.push({ tagName: "link", rel: "prev", href: paginatedUrl(path, page - 1) });
  }
  if (totalPages && page < totalPages) {
    links.push({ tagName: "link", rel: "next", href: paginatedUrl(path, page + 1) });
  }
  return links;
}

/**
 * Builds a schema.org ItemList of article URLs for hub pages (CollectionPage.mainEntity).
 * Sponsored items are excluded. Returns undefined when there is nothing to list.
 * @param {Array<{slug?: string, id?: string, title?: string, isSponsored?: boolean}>} articles
 * @param {number} [startPosition=1]
 * @param {number} [limit=20]
 */
export function buildArticleItemList(articles, startPosition = 1, limit = 20) {
  if (!Array.isArray(articles)) return undefined;
  const items = articles
    .filter((a) => a && !a.isSponsored && (a.slug || a.id))
    .slice(0, limit)
    .map((a, idx) => ({
      "@type": "ListItem",
      position: startPosition + idx,
      url: `${SITE_URL}/news/${a.slug || a.id}`,
      ...(a.title ? { name: a.title } : {}),
    }));

  if (items.length === 0) return undefined;
  return {
    "@type": "ItemList",
    itemListOrder: "https://schema.org/ItemListOrderDescending",
    numberOfItems: items.length,
    itemListElement: items,
  };
}

/** Heuristic: does a byline look like an organization/publication rather than a person? */
const ORG_HINTS = /\b(news|newspaper|newspapers|media|times|post|tribune|guardian|punch|vanguard|daily|herald|agency|desk|editorial|staff|reporters?|correspondents?|tv|radio|online|ng|limited|ltd|plc|group|network|bureau|nan|channels|premium)\b/i;

function cleanByline(name) {
  return String(name || "")
    .replace(/^\s*(reported\s+by|written\s+by|by)\s*[:\-]?\s*/i, "")
    .trim();
}

/**
 * Builds the NewsArticle `author` node. Uses Person for human bylines and Organization for
 * publications/desks, without the "Reported by" UI prefix (Google author markup best practice).
 */
export function buildAuthorNode(article) {
  const author = cleanByline(article?.author);
  const source = cleanByline(article?.source) || "News Stand NG";

  if (author && !ORG_HINTS.test(author) && author.toLowerCase() !== source.toLowerCase()) {
    return { "@type": "Person", name: author };
  }

  return {
    "@type": "Organization",
    name: author || source,
    ...(article?.sourceUrl ? { url: safeOrigin(article.sourceUrl) } : {}),
  };
}

function safeOrigin(url) {
  try {
    return new URL(url).origin;
  } catch {
    return undefined;
  }
}
