import { textResponse, getSiteUrl } from "../utils/xml";

export async function loader() {
  const siteUrl = getSiteUrl();

  const robots = [
    "User-agent: *",
    "Allow: /",
    "Disallow: /api/",
    "Disallow: /search",
    "Disallow: /auth",
    "Disallow: /admin",
    "",
    `Sitemap: ${siteUrl}/sitemap.xml`,
  ].join("\n");

  return textResponse(robots, 300);
}
