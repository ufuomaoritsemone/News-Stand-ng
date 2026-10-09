import React from "react";
import { useLoaderData } from "react-router";
import App from "../../src/App";
import { api } from "../../src/services/api";

function capitalizeCategory(cat) {
  if (!cat) return "News";
  return cat.charAt(0).toUpperCase() + cat.slice(1).toLowerCase();
}

export function meta({ data }) {
  const category = data?.category || "News";
  const title = `${category} News Nigeria — Top Stories & Headlines | News Stand NG`;
  const description =
    `Latest ${category.toLowerCase()} news in Nigeria. Live breaking updates, verified reports, and in-depth analysis from top national publications.`;
  const canonicalUrl = `https://newsstand.ng/category/${encodeURIComponent(category.toLowerCase())}`;

  return [
    { title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    {
      tagName: "link",
      rel: "alternate",
      type: "application/rss+xml",
      title: `${category} Headlines RSS Feed | News Stand NG`,
      href: `https://newsstand.ng/category/${encodeURIComponent(category.toLowerCase())}/feed.xml`,
    },
    { name: "robots", content: "index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1" },
    { property: "og:type", content: "website" },
    { property: "og:site_name", content: "News Stand NG" },
    { property: "og:title", content: title },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { property: "og:image", content: "https://newsstand.ng/newsstand-masthead.png" },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: title },
    { name: "twitter:description", content: description },
    { name: "twitter:image", content: "https://newsstand.ng/newsstand-masthead.png" },
  ];
}

export async function loader({ params, request }) {
  const rawCategory = params.category || "General";
  const category = capitalizeCategory(rawCategory);
  const url = new URL(request.url);
  const page = parseInt(url.searchParams.get("page") || "1", 10);
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

    return {
      category,
      initialArticles: articles,
      initialCategories: Array.isArray(categories) && categories.length > 0 ? categories : null,
      initialSources: Array.isArray(sources) && sources.length > 0 ? sources : null,
      initialSponsored: [],
    };
  } catch (err) {
    console.warn(`[SSR Category Loader] Failed for ${category}:`, err.message);
    return {
      category,
      initialArticles: [],
      initialCategories: null,
      initialSources: null,
      initialSponsored: [],
    };
  }
}

export default function CategoryPage() {
  const data = useLoaderData();

  const categoryJsonLd = {
    "@context": "https://schema.org",
    "@type": "CollectionPage",
    "name": `${data.category} News Nigeria`,
    "description": `Curated ${data.category.toLowerCase()} news in Nigeria across verified publications.`,
    "url": `https://newsstand.ng/category/${encodeURIComponent(data.category.toLowerCase())}`,
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
      <App initialData={data} initialCategory={data.category} />
    </>
  );
}
