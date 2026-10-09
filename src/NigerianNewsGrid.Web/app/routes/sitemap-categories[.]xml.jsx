import { api } from "../../src/services/api";
import { escapeXml, toIso8601, xmlResponse, getSiteUrl } from "../utils/xml";

export async function loader() {
  const siteUrl = getSiteUrl();

  let entries = [];
  try {
    const data = await api.getSitemapEntries("categories", 1, 100);
    entries = data?.entries || [];
  } catch (err) {
    console.warn("[Categories Sitemap] Failed to fetch category entries:", err.message);
  }

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${entries
  .map((entry) => {
    const canonicalLoc = entry.loc.startsWith("http")
      ? entry.loc
      : `${siteUrl}${entry.loc.startsWith("/") ? "" : "/"}${entry.loc}`;
    const lastMod = toIso8601(entry.lastMod);
    const changeFreq = entry.changeFreq || "hourly";
    const priority = (entry.priority ?? 0.8).toFixed(1);

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
