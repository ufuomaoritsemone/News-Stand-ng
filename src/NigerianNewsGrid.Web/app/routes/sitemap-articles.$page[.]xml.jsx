import { api } from "../../src/services/api";
import { escapeXml, toIso8601, xmlResponse, getSiteUrl } from "../utils/xml";

export async function loader({ params }) {
  const rawPage = params.page || "1";
  const parsedPage = parseInt(rawPage.replace(/\.xml$/i, ""), 10);
  const page = isNaN(parsedPage) || parsedPage < 1 ? 1 : parsedPage;
  const pageSize = 50000;

  const siteUrl = getSiteUrl();

  let entries = [];
  try {
    const data = await api.getSitemapEntries("articles", page, pageSize);
    entries = data?.entries || [];
  } catch (err) {
    console.warn(`[Articles Sitemap] Failed to fetch page ${page}:`, err.message);
  }

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${entries
  .map((entry) => {
    const canonicalLoc = entry.loc.startsWith("http")
      ? entry.loc
      : `${siteUrl}${entry.loc.startsWith("/") ? "" : "/"}${entry.loc}`;
    const lastMod = toIso8601(entry.lastMod || entry.publishedAt);
    const changeFreq = entry.changeFreq || "daily";
    const priority = (entry.priority ?? 0.7).toFixed(1);

    return `  <url>
    <loc>${escapeXml(canonicalLoc)}</loc>
    <lastmod>${escapeXml(lastMod)}</lastmod>
    <changefreq>${escapeXml(changeFreq)}</changefreq>
    <priority>${escapeXml(priority)}</priority>
  </url>`;
  })
  .join("\n")}
</urlset>`;

  return xmlResponse(xml, 300);
}
