import { api } from "../../src/services/api";
import { escapeXml, xmlResponse, getSiteUrl } from "../utils/xml";

export async function loader({ params }) {
  const categoryParam = params.category || "general";
  const siteUrl = getSiteUrl();

  const formattedCategory =
    categoryParam.charAt(0).toUpperCase() + categoryParam.slice(1).toLowerCase();

  let articles = [];
  try {
    const data = await api.getArticles({
      category: categoryParam,
      pageSize: 50,
    });
    articles = Array.isArray(data) ? data : data?.items || [];
  } catch (err) {
    console.warn(`[Category RSS Feed] Failed for ${categoryParam}:`, err.message);
  }

  const nowRfc822 = new Date().toUTCString();
  const feedUrl = `${siteUrl}/category/${categoryParam}/feed.xml`;
  const categoryPageUrl = `${siteUrl}/category/${categoryParam}`;

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">
  <channel>
    <title>News Stand NG — ${escapeXml(formattedCategory)} Headlines</title>
    <link>${escapeXml(categoryPageUrl)}</link>
    <description>Curated ${escapeXml(formattedCategory)} Nigerian news aggregation, breaking stories, and editorial coverage.</description>
    <language>en-ng</language>
    <lastBuildDate>${escapeXml(nowRfc822)}</lastBuildDate>
    <atom:link href="${escapeXml(feedUrl)}" rel="self" type="application/rss+xml" />
${articles
  .slice(0, 50)
  .map((a) => {
    const slug = a.slug || a.id;
    const itemUrl = `${siteUrl}/news/${slug}`;
    const pubDate = a.publishedAt ? new Date(a.publishedAt).toUTCString() : nowRfc822;
    const title = a.title || "News Story";
    const description = a.excerpt || a.summary || `Read the latest ${formattedCategory} update on News Stand NG.`;
    const category = a.category || formattedCategory;

    return `    <item>
      <title>${escapeXml(title)}</title>
      <link>${escapeXml(itemUrl)}</link>
      <guid isPermaLink="true">${escapeXml(itemUrl)}</guid>
      <pubDate>${escapeXml(pubDate)}</pubDate>
      <description>${escapeXml(description)}</description>
      <category>${escapeXml(category)}</category>
    </item>`;
  })
  .join("\n")}
  </channel>
</rss>`;

  return xmlResponse(xml, 300);
}
