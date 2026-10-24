import React from "react";
import { useLoaderData } from "react-router";
import App from "../../src/App";
import { api } from "../../src/services/api";
import { SITE_URL, DEFAULT_OG_IMAGE, paginatedUrl, paginationLinks, buildArticleItemList } from "../utils/seo";

function capitalizeCategory(cat) {
  if (!cat) return "News";
  return cat.charAt(0).toUpperCase() + cat.slice(1).toLowerCase();
}

export function meta({ data }) {
  const category = data?.category || "News";
  const page = data?.page || 1;
  const pageSize = data?.pageSize || 24;
  const total = data?.total || 0;
  const categoryPath = `/category/${encodeURIComponent(category.toLowerCase())}`;
  const baseTitle = `${category} News Nigeria — Top Stories & Headlines | News Stand NG`;
  const title = page > 1 ? `${baseTitle} — Page ${page}` : baseTitle;
  const description =
    `Latest ${category.toLowerCase()} news in Nigeria. Live breaking updates, verified reports, and in-depth analysis from top national publications.`;
  // Never canonicalize page 2+ back to page 1 (seo-news SKILL §2.1).
  const canonicalUrl = paginatedUrl(categoryPath, page);
  // When the API doesn't report a total, assume another page exists if this one is full.
  const totalPages = total > 0
    ? Math.ceil(total / pageSize)
    : ((data?.initialArticles?.length || 0) >= pageSize ? page + 1 : page);

  return [
    { title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    {
      tagName: "link",
      rel: "alternate",
      type: "application/rss+xml",
      title: `${category} Headlines RSS Feed | News Stand NG`,
      href: `${SITE_URL}${categoryPath}/feed.xml`,
    },
    { name: "robots", content: "index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1" },
    { property: "og:type", content: "website" },
    { property: "og:site_name", content: "News Stand NG" },
    { property: "og:title", content: baseTitle },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { property: "og:image", content: DEFAULT_OG_IMAGE },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: baseTitle },
    { name: "twitter:description", content: description },
    { name: "twitter:image", content: DEFAULT_OG_IMAGE },
    ...paginationLinks(categoryPath, page, totalPages),
  ];
}

export async function loader({ params, request }) {
  const rawCategory = params.category || "General";
  const category = capitalizeCategory(rawCategory);
  const url = new URL(request.url);
  const page = Math.max(1, parseInt(url.searchParams.get("page") || "1", 10) || 1);
  const pageSize = 24;

  try {
    const [articlesData, categories, sources] = await Promise.all([
      api.getArticles({ category, page, pageSize }).catch(() => []),
      api.getCategories().catch(() => []),
      api.getSources().catch(() => []),
    ]);

    const articles = Array.isArray(articlesData)
      ? articlesData
      : (articlesData?.articles || []);
    const total = Array.isArray(articlesData)
      ? 0
      : (articlesData?.total || articlesData?.totalCount || 0);

    return {
      category,
      page,
      pageSize,
      total,
      initialArticles: articles,
      initialCategories: Array.isArray(categories) && categories.length > 0 ? categories : null,
      initialSources: Array.isArray(sources) && sources.length > 0 ? sources : null,
      initialSponsored: [],
    };
  } catch (err) {
    console.warn(`[SSR Category Loader] Failed for ${category}:`, err.message);
    return {
      category,
      page,
      pageSize,
      total: 0,
      initialArticles: [],
      initialCategories: null,
      initialSources: null,
      initialSponsored: [],
    };
  }
}

export default function CategoryPage() {
  const data = useLoaderData();
  const page = data.page || 1;
  const pageSize = data.pageSize || 24;
  const categoryItemList = buildArticleItemList(data.initialArticles, (page - 1) * pageSize + 1, pageSize);

  const categoryJsonLd = {
    "@context": "https://schema.org",
    "@type": "CollectionPage",
    "name": `${data.category} News Nigeria`,
    "description": `Curated ${data.category.toLowerCase()} news in Nigeria across verified publications.`,
    "url": paginatedUrl(`/category/${encodeURIComponent(data.category.toLowerCase())}`, page),
    ...(categoryItemList ? { "mainEntity": categoryItemList } : {}),
    "publisher": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng"
    }
  };

  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(categoryJsonLd) }}
      />
      <App key={data.category} initialData={data} initialCategory={data.category} />
    </>
  );
}
