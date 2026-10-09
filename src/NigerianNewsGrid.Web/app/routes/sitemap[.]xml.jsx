import { api } from "../../src/services/api";
import { escapeXml, toIso8601, xmlResponse, getSiteUrl } from "../utils/xml";

export async function loader() {
  const siteUrl = getSiteUrl();
  const nowIso = toIso8601(new Date());

  let totalArticles = 0;
  try {
    const articlesMeta = await api.getSitemapEntries("articles", 1, 1);
    totalArticles = articlesMeta?.totalCount || 0;
  } catch (err) {
    console.warn("[Sitemap Index] Failed to get articles count:", err.message);
  }

  const articlesPages = Math.max(1, Math.ceil(totalArticles / 50000));
  const sitemaps = [
    { loc: `${siteUrl}/sitemap-news.xml`, lastmod: nowIso },
  ];

  for (let p = 1; p <= articlesPages; p++) {
    sitemaps.push({ loc: `${siteUrl}/sitemap-articles-${p}.xml`, lastmod: nowIso });
  }

  sitemaps.push(
    { loc: `${siteUrl}/sitemap-stories.xml`, lastmod: nowIso },
    { loc: `${siteUrl}/sitemap-states.xml`, lastmod: nowIso },
    { loc: `${siteUrl}/sitemap-data.xml`, lastmod: nowIso },
    { loc: `${siteUrl}/sitemap-topics.xml`, lastmod: nowIso },
    { loc: `${siteUrl}/sitemap-categories.xml`, lastmod: nowIso },
    { loc: `${siteUrl}/sitemap-sources.xml`, lastmod: nowIso }
  );

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${sitemaps
  .map(
    (s) => `  <sitemap>
    <loc>${escapeXml(s.loc)}</loc>
    <lastmod>${escapeXml(s.lastmod)}</lastmod>
  </sitemap>`
  )
  .join("\n")}
</sitemapindex>`;

  return xmlResponse(xml, 300);
}
