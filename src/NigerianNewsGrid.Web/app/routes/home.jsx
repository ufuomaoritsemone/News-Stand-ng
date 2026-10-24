import React from "react";
import { useLoaderData } from "react-router";
import App from "../../src/App";
import { api, getOptimizedImageUrl } from "../../src/services/api";
import { buildArticleItemList } from "../utils/seo";

export function meta({ data }) {
  const title = "News Stand NG | Live Breaking News, Top Stories & In-Depth Journalism";
  const description =
    "Real-time Nigerian news aggregation, breaking alerts, politics, business, tech, opinion, and video coverage curated across top national publications.";
  const canonicalUrl = "https://newsstand.ng/";

  const leadImage = data?.initialArticles?.[0]?.imageUrl;
  const leadPreload = leadImage
    ? [{ tagName: "link", rel: "preload", as: "image", href: getOptimizedImageUrl(leadImage, 800), fetchpriority: "high" }]
    : [];

  return [
    { title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    { name: "robots", content: "index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1" },
    { property: "og:type", content: "website" },
    { property: "og:site_name", content: "News Stand NG" },
    { property: "og:title", content: "News Stand NG | Live Breaking News & Top Stories" },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { property: "og:image", content: "https://newsstand.ng/newsstand-masthead.png" },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: "News Stand NG | Live Breaking News" },
    { name: "twitter:description", content: description },
    { name: "twitter:image", content: "https://newsstand.ng/newsstand-masthead.png" },
    ...leadPreload,
  ];
}

export async function loader() {
  try {
    const [articlesData, categories, sources, sponsored, trendingStoriesData] = await Promise.all([
      api.getArticles({ pageSize: 24 }).catch(() => []),
      api.getCategories().catch(() => []),
      api.getSources().catch(() => []),
      api.getSponsoredArticles().catch(() => []),
      api.getTrendingStories({ limit: 6, windowHours: 24 }).catch(() => ({ stories: [] })),
    ]);

    const articles = Array.isArray(articlesData)
      ? articlesData
      : (articlesData?.articles || []);

    const trendingStories = Array.isArray(trendingStoriesData?.stories)
      ? trendingStoriesData.stories
      : (Array.isArray(trendingStoriesData) ? trendingStoriesData : []);

    return {
      initialArticles: articles,
      initialCategories: Array.isArray(categories) && categories.length > 0 ? categories : null,
      initialSources: Array.isArray(sources) && sources.length > 0 ? sources : null,
      initialSponsored: Array.isArray(sponsored) ? sponsored : [],
      initialTrendingStories: trendingStories,
    };
  } catch (err) {
    console.warn("[SSR Home Loader] Error:", err.message);
    return {
      initialArticles: [],
      initialCategories: null,
      initialSources: null,
      initialSponsored: [],
      initialTrendingStories: [],
    };
  }
}

export default function Home() {
  const data = useLoaderData();
  const homeItemList = buildArticleItemList(data.initialArticles, 1, 20);

  const organizationStructuredData = {
    "@context": "https://schema.org",
    "@graph": [
      {
        "@type": "NewsMediaOrganization",
        "@id": "https://newsstand.ng/#organization",
        "name": "News Stand NG",
        "url": "https://newsstand.ng",
        "logo": {
          "@type": "ImageObject",
          "url": "https://newsstand.ng/logo.png",
          "width": 512,
          "height": 512
        },
        "sameAs": [
          "https://x.com/newsstandng",
          "https://facebook.com/newsstandng"
        ],
        "description": "Real-time Nigerian news aggregation, breaking alerts, politics, business, and in-depth journalism."
      },
      {
        "@type": "WebSite",
        "@id": "https://newsstand.ng/#website",
        "url": "https://newsstand.ng",
        "name": "News Stand NG",
        "publisher": {
          "@id": "https://newsstand.ng/#organization"
        },
        "potentialAction": {
          "@type": "SearchAction",
          "target": "https://newsstand.ng/?search={search_term_string}",
          "query-input": "required name=search_term_string"
        }
      },
      ...(homeItemList
        ? [{
            "@type": "CollectionPage",
            "@id": "https://newsstand.ng/#homepage",
            "url": "https://newsstand.ng/",
            "name": "Top Nigerian News Stories",
            "isPartOf": { "@id": "https://newsstand.ng/#website" },
            "publisher": { "@id": "https://newsstand.ng/#organization" },
            "mainEntity": homeItemList
          }]
        : [])
    ]
  };

  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationStructuredData) }}
      />
      <App initialData={data} />
    </>
  );
}
