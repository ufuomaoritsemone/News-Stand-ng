import React from "react";
import { useLoaderData, Link } from "react-router";
import { api, getOptimizedImageUrl } from "../../src/services/api";
import { formatTimeAgo } from "../../src/services/timeUtils";
import Footer from "../../src/components/Footer";
import {
  DEFAULT_OG_IMAGE,
  MIN_TOPIC_ARTICLES_FOR_INDEXING,
  paginatedUrl,
  paginationLinks,
  buildArticleItemList,
} from "../utils/seo";
import {
  ArrowLeft,
  Tag,
  Calendar,
  Layers,
  Clock,
  User,
  Building,
  MapPin,
  Sparkles
} from "lucide-react";

export function meta({ data }) {
  if (!data?.topic) {
    return [
      { title: "Topic Not Found | News Stand NG" },
      { name: "robots", content: "noindex, nofollow" }
    ];
  }

  const { topic, page, total, pageSize } = data;
  const title = `${topic.name} News, Analysis & Reports | News Stand NG`;
  const description =
    topic.description ||
    `Explore aggregated news coverage, timeline reports, and updates regarding ${topic.name} on News Stand NG.`;
  const topicPath = `/topic/${topic.slug}`;
  const canonicalUrl = paginatedUrl(topicPath, page);
  const totalPages = Math.ceil((total || 0) / (pageSize || 20));

  // Thin content guardrail: noindex, follow when the TOPIC (not the current page) has fewer
  // than MIN_TOPIC_ARTICLES_FOR_INDEXING articles. Mirrors the sitemap filter in SeoController.
  const isThin = (total || 0) < MIN_TOPIC_ARTICLES_FOR_INDEXING;
  const robots = isThin
    ? "noindex, follow"
    : "index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1";

  return [
    { title: page > 1 ? `${title} — Page ${page}` : title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    { name: "robots", content: robots },
    { property: "og:type", content: "website" },
    { property: "og:site_name", content: "News Stand NG" },
    { property: "og:title", content: title },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { property: "og:image", content: DEFAULT_OG_IMAGE },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: title },
    { name: "twitter:description", content: description },
    { name: "twitter:image", content: DEFAULT_OG_IMAGE },
    ...paginationLinks(topicPath, page, totalPages),
  ];
}

export async function loader({ params, request }) {
  const { slug } = params;
  if (!slug) {
    throw new Response("Topic slug is required", { status: 400 });
  }

  const url = new URL(request.url);
  const page = parseInt(url.searchParams.get("page") || "1", 10);
  const pageSize = 20;

  try {
    const topic = await api.getTopicBySlug(slug);
    if (!topic) {
      throw new Response("Topic Not Found", { status: 404 });
    }

    const articlesResponse = await api.getTopicArticles(slug, { page, pageSize }).catch(() => ({
      articles: [],
      total: 0,
      page,
      pageSize
    }));

    return {
      topic,
      articles: articlesResponse.articles || [],
      total: articlesResponse.total || (articlesResponse.articles?.length || 0),
      page,
      pageSize,
    };
  } catch (err) {
    if (err instanceof Response) throw err;
    console.warn(`[SSR Topic Loader] Failed for slug '${slug}':`, err.message);
    throw new Response("Topic Not Found", { status: 404 });
  }
}

function getTopicIcon(type) {
  switch ((type || "").toLowerCase()) {
    case "person":
      return <User size={14} />;
    case "organization":
      return <Building size={14} />;
    case "place":
      return <MapPin size={14} />;
    default:
      return <Sparkles size={14} />;
  }
}

export default function TopicHubPage() {
  const { topic, articles, total, page, pageSize } = useLoaderData();

  const totalPages = Math.ceil(total / pageSize);

  const topicItemList = buildArticleItemList(articles, (page - 1) * pageSize + 1, pageSize);
  const aboutType =
    topic.type === "Person" ? "Person"
      : topic.type === "Place" ? "Place"
      : topic.type === "Organization" ? "Organization"
      : "Thing";

  const collectionPageJsonLd = {
    "@context": "https://schema.org",
    "@type": "CollectionPage",
    "name": `${topic.name} News & Reports`,
    "description": topic.description || `Aggregated news coverage and reports regarding ${topic.name}.`,
    "url": paginatedUrl(`/topic/${topic.slug}`, page),
    "about": {
      "@type": aboutType,
      "name": topic.name
    },
    ...(topicItemList ? { "mainEntity": topicItemList } : {}),
    "publisher": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng"
    }
  };

  const breadcrumbJsonLd = {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    "itemListElement": [
      {
        "@type": "ListItem",
        "position": 1,
        "name": "Home",
        "item": "https://newsstand.ng/"
      },
      {
        "@type": "ListItem",
        "position": 2,
        "name": topic.name,
        "item": `https://newsstand.ng/topic/${topic.slug}`
      }
    ]
  };

  return (
    <div className="topic-page-layout">
      {/* Structured Data */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(collectionPageJsonLd) }}
      />
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumbJsonLd) }}
      />

      {/* Header */}
      <header className="article-page-header">
        <div className="article-header-container">
          <Link to="/" className="article-header-back-btn" title="Back to Headlines">
            <ArrowLeft size={18} />
            <span>Headlines</span>
          </Link>

          <Link to="/" className="article-header-brand">
            <span className="brand-dot" />
            <span className="brand-title">News Stand NG</span>
          </Link>

          <div style={{ width: 80 }} />
        </div>
      </header>

      {/* Main Container */}
      <main className="topic-page-container">
        {/* Breadcrumb */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li><span>Topics</span></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">{topic.name}</li>
          </ol>
        </nav>

        {/* Topic Hero Spotlight */}
        <section className="topic-hero-banner">
          <div className="topic-type-chip">
            {getTopicIcon(topic.type)}
            <span>{topic.type || "Topic Hub"}</span>
          </div>

          <h1 className="topic-hero-title">{topic.name}</h1>

          {topic.description ? (
            <p className="topic-hero-description">{topic.description}</p>
          ) : (
            <p className="topic-hero-description">
              Curated news aggregation, verified publisher reports, and chronological timeline developments regarding {topic.name}.
            </p>
          )}

          <div className="topic-stats-row">
            <div className="topic-stat-badge">
              <Layers size={14} />
              <span>{total} Total Reports</span>
            </div>
            {topic.createdAt && (
              <div className="topic-stat-badge">
                <Calendar size={14} />
                <span>Tracking since {new Date(topic.createdAt).getFullYear()}</span>
              </div>
            )}
          </div>
        </section>

        {/* Articles Feed */}
        <section className="topic-articles-section">
          <div className="topic-feed-header">
            <h2 className="topic-feed-title">Curated Stories & Coverage</h2>
            <div className="topic-feed-count">Showing {articles.length} of {total} stories</div>
          </div>

          {articles.length === 0 ? (
            <div className="topic-empty-feed">
              <Tag size={36} className="empty-icon" />
              <h3>No stories recorded under this topic yet</h3>
              <p>Our autonomous ingestion pipelines regularly monitor major Nigerian publications for fresh reports.</p>
              <Link to="/" className="empty-return-link">Explore Top Headlines</Link>
            </div>
          ) : (
            <div className="topic-cards-grid">
              {articles.map((article) => (
                <article key={article.id} className="topic-article-card">
                  {article.imageUrl && (
                    <Link to={`/news/${article.slug || article.id}`} className="topic-card-media-link">
                      <img
                        src={getOptimizedImageUrl(article.imageUrl, 480)}
                        alt={article.title}
                        className="topic-card-media-img"
                        loading="lazy"
                        onError={(e) => {
                          e.target.onerror = null;
                          e.target.srcset = '';
                          e.target.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=480&q=80';
                        }}
                      />
                    </Link>
                  )}

                  <div className="topic-card-body">
                    <div className="topic-card-meta">
                      <span className="topic-card-source">{article.source || "News Stand NG"}</span>
                      {article.category && (
                        <>
                          <span className="meta-dot">•</span>
                          <span className="topic-card-category">{article.category}</span>
                        </>
                      )}
                      {article.publishedAt && (
                        <>
                          <span className="meta-dot">•</span>
                          <span className="topic-card-time">
                            <Clock size={11} />
                            {formatTimeAgo(article.publishedAt)}
                          </span>
                        </>
                      )}
                    </div>

                    <h3 className="topic-card-headline">
                      <Link to={`/news/${article.slug || article.id}`}>
                        {article.title}
                      </Link>
                    </h3>

                    {(article.excerpt || article.summary) && (
                      <p className="topic-card-excerpt">
                        {article.excerpt || article.summary}
                      </p>
                    )}

                    <div className="topic-card-footer">
                      <Link to={`/news/${article.slug || article.id}`} className="topic-card-read-link">
                        <span>Read Summary</span>
                        <ArrowLeft size={13} style={{ transform: "rotate(180deg)" }} />
                      </Link>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="topic-pagination-bar">
              {page > 1 && (
                <Link
                  to={`/topic/${topic.slug}?page=${page - 1}`}
                  className="pagination-btn"
                >
                  Previous
                </Link>
              )}
              <span className="pagination-current">
                Page {page} of {totalPages}
              </span>
              {page < totalPages && (
                <Link
                  to={`/topic/${topic.slug}?page=${page + 1}`}
                  className="pagination-btn"
                >
                  Next
                </Link>
              )}
            </div>
          )}
        </section>
      </main>

      <Footer />
    </div>
  );
}
