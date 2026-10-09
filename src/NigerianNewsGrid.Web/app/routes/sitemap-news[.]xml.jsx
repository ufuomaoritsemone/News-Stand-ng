import { api } from "../../src/services/api";
import { escapeXml, toIso8601, xmlResponse, getSiteUrl } from "../utils/xml";

export async function loader() {
  const siteUrl = getSiteUrl();

  let entries = [];
  try {
    const data = await api.getSitemapEntries("news", 1, 1000);
    entries = data?.entries || [];
  } catch (err) {
    console.warn("[Google News Sitemap] Failed to fetch news entries:", err.message);
  }

  // Double check 48h cutoff and max 1000 URLs
  const cutoffTime = Date.now() - 48 * 60 * 60 * 1000;
  const filtered = entries
    .filter((e) => {
      const pubTime = new Date(e.publishedAt || e.lastMod).getTime();
      return !isNaN(pubTime) && pubTime >= cutoffTime;
    })
    .slice(0, 1000);

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
        xmlns:news="http://www.google.com/schemas/sitemap-news/0.9">
${filtered
  .map((entry) => {
    const canonicalLoc = entry.loc.startsWith("http")
      ? entry.loc
      : `${siteUrl}${entry.loc.startsWith("/") ? "" : "/"}${entry.loc}`;
    const pubDate = toIso8601(entry.publishedAt || entry.lastMod);
    const pubName = entry.publicationName || "News Stand NG";
    const pubLang = entry.publicationLanguage || "en";
    const title = entry.title || "News Article";

    return `  <url>
    <loc>${escapeXml(canonicalLoc)}</loc>
    <news:news>
      <news:publication>
        <news:name>${escapeXml(pubName)}</news:name>
        <news:language>${escapeXml(pubLang)}</news:language>
      </news:publication>
      <news:publication_date>${escapeXml(pubDate)}</news:publication_date>
      <news:title>${escapeXml(title)}</news:title>
    </news:news>
  </url>`;
  })
  .join("\n")}
</urlset>`;

  return xmlResponse(xml, 300);
}
