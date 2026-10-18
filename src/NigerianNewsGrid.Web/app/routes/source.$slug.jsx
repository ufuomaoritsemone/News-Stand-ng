import React from "react";
import { useLoaderData, Link } from "react-router";
import { api, getOptimizedImageUrl } from "../../src/services/api";
import { formatTimeAgo, cleanEditorialSummary } from "../../src/services/timeUtils";
import Footer from "../../src/components/Footer";
import { DEFAULT_OG_IMAGE, paginatedUrl, paginationLinks, buildArticleItemList } from "../utils/seo";
import {
  ArrowLeft,
  Globe,
  ExternalLink,
  Layers,
  Clock,
  ShieldCheck,
  Radio,
  FileText
} from "lucide-react";

export function meta({ data }) {
  if (!data?.source) {
    return [
      { title: "Publisher Not Found | News Stand NG" },
      { name: "robots", content: "noindex, nofollow" }
    ];
  }

  const { source, page = 1, total = 0, pageSize = 24 } = data;
  const title = `${source.name} News, Headlines & Latest Reports | News Stand NG`;
  const description =
    source.description ||
    `Read verified headlines and aggregated news reports from ${source.name} on News Stand NG.`;
  const sourcePath = `/source/${source.slug}`;
  const canonicalUrl = paginatedUrl(sourcePath, page);
  const totalPages = Math.ceil((total || 0) / (pageSize || 24));

  return [
    { title: page > 1 ? `${title} — Page ${page}` : title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    { name: "robots", content: "index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1" },
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
    ...paginationLinks(sourcePath, page, totalPages),
  ];
}

export async function loader({ params, request }) {
  const { slug } = params;
  if (!slug) {
    throw new Response("Source slug is required", { status: 400 });
  }

  const url = new URL(request.url);
  const page = parseInt(url.searchParams.get("page") || "1", 10);
  const pageSize = 24;

  try {
    const source = await api.getSourceBySlug(slug);
    if (!source) {
      throw new Response("Publisher Not Found", { status: 404 });
    }

    const articlesResponse = await api.getArticles({
      source: source.name,
      page,
      pageSize,
    }).catch(() => ({
      articles: [],
      total: 0,
      page,
      pageSize,
    }));

    return {
      source,
      articles: articlesResponse.articles || [],
      total: articlesResponse.total || (articlesResponse.articles?.length || 0),
      page,
      pageSize,
    };
  } catch (err) {
    if (err instanceof Response) throw err;
    console.warn(`[SSR Source Loader] Failed for slug '${slug}':`, err.message);
    throw new Response("Publisher Not Found", { status: 404 });
  }
}

export default function SourceHubPage() {
  const { source, articles, total, page, pageSize } = useLoaderData();

  const totalPages = Math.ceil(total / pageSize);

  const organizationJsonLd = {
    "@context": "https://schema.org",
    "@type": "NewsMediaOrganization",
    "name": source.name,
    "url": source.websiteUrl || `https://newsstand.ng/source/${source.slug}`,
    "description": source.description || `Aggregated Nigerian news publisher ${source.name}.`,
    "parentOrganization": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng"
    }
  };

  const sourceItemList = buildArticleItemList(articles, (page - 1) * pageSize + 1, pageSize);

  const collectionPageJsonLd = {
    "@context": "https://schema.org",
    "@type": "CollectionPage",
    "name": `${source.name} Coverage & Headlines`,
    "description": source.description || `Latest verified reports from ${source.name}.`,
    "url": paginatedUrl(`/source/${source.slug}`, page),
    ...(sourceItemList ? { "mainEntity": sourceItemList } : {}),
    "publisher": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng"
    },
    "about": {
      "@type": "NewsMediaOrganization",
      "name": source.name
    }
  };

  return (
    <div className="topic-page-layout">
      {/* Structured Data */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationJsonLd) }}
      />
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(collectionPageJsonLd) }}
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
            <li><span>Publishers</span></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">{source.name}</li>
          </ol>
        </nav>

        {/* Source Hero Banner */}
        <section className="topic-hero-banner">
          <div className="topic-type-chip" style={{ background: "rgba(0, 135, 81, 0.12)", color: "var(--brand-green, #008751)" }}>
            <Radio size={14} />
            <span>Verified News Partner</span>
          </div>

          <h1 className="topic-hero-title" style={{ display: "flex", alignItems: "center", gap: "10px", flexWrap: "wrap" }}>
            <span>{source.name}</span>
            <ShieldCheck size={28} color="var(--brand-green, #008751)" title="Verified Editorial Feed" />
          </h1>

          <p className="topic-hero-description">
            {source.description ||
              `Aggregated news coverage, investigative dispatches, and breaking reports from ${source.name}. Full stories remain published at their official editorial portal.`}
          </p>

          <div className="topic-stats-row">
            <div className="topic-stat-badge">
              <Layers size={14} />
              <span>{total || source.articleCount || articles.length} Aggregated Stories</span>
            </div>

            {source.websiteUrl && (
              <a
                href={source.websiteUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="topic-stat-badge"
                style={{
                  color: "var(--brand-green, #008751)",
                  textDecoration: "underline",
                  textUnderlineOffset: "3px",
                  fontWeight: 600
                }}
              >
                <Globe size={14} />
                <span>Visit Official Website</span>
                <ExternalLink size={12} />
              </a>
            )}
          </div>
        </section>

        {/* Articles Feed */}
        <section className="topic-articles-section">
          <div className="topic-section-header">
            <h2 className="topic-section-title">
              <FileText size={18} />
              <span>Latest Coverage by {source.name}</span>
            </h2>
            <span className="topic-section-count">
              Showing {articles.length} of {total} headlines
            </span>
          </div>

          {articles.length === 0 ? (
            <div className="topic-empty-state">
              <p>No recent articles found for this publisher right now.</p>
              <Link to="/" className="topic-empty-btn">
                Browse All Live Headlines
              </Link>
            </div>
          ) : (
            <div className="topic-cards-grid">
              {articles.map((article) => {
                const articleSlug = article.slug || article.id;
                const readingMinutes = Math.max(1, Math.round(((article.excerpt || article.summary || article.title || "").split(/\s+/).length) / 180));
                const pubDate = formatTimeAgo(article.publishedAt);

                return (
                  <article key={article.id} className="topic-story-card">
                    <div className="topic-card-media">
                      <img
                        src={getOptimizedImageUrl(article.imageUrl, 480)}
                        alt={article.title}
                        loading="lazy"
                        width="480"
                        height="270"
                        className="topic-card-img"
                        onError={(e) => {
                          e.target.src =
                            "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=480&q=80";
                        }}
                      />
                      {article.category && (
                        <span className="topic-card-category-badge">
                          {article.category}
                        </span>
                      )}
                      {article.isSponsored && (
                        <span
                          className="badge-sponsored"
                          style={{
                            position: "absolute",
                            top: 8,
                            right: 8,
                            fontSize: "0.7rem",
                            fontWeight: 700,
                            padding: "2px 8px",
                            borderRadius: "4px",
                            background: "rgba(217, 119, 6, 0.9)",
                            color: "#ffffff"
                          }}
                        >
                          Sponsored
                        </span>
                      )}
                    </div>

                    <div className="topic-card-body">
                      <div className="topic-card-meta">
                        <span className="topic-card-source">Source: {article.source || source.name}</span>
                        <span className="meta-dot">•</span>
                        <span className="topic-card-time">{pubDate}</span>
                        <span className="meta-dot">•</span>
                        <span className="topic-card-read">
                          <Clock size={12} style={{ verticalAlign: "text-bottom", marginRight: 2 }} />
                          {readingMinutes}m
                        </span>
                      </div>

                      <h3 className="topic-card-title">
                        <Link to={`/news/${articleSlug}`}>{article.title}</Link>
                      </h3>

                      {(article.excerpt || article.summary) && (
                        <p className="topic-card-excerpt">
                          {cleanEditorialSummary(article.excerpt || article.summary)}
                        </p>
                      )}

                      <div className="topic-card-footer" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "1rem" }}>
                        <Link to={`/news/${articleSlug}`} className="topic-card-read-more">
                          Read Overview →
                        </Link>
                        {article.url && (
                          <a
                            href={article.url}
                            target="_blank"
                            rel={article.isSponsored ? "sponsored noopener" : "noopener noreferrer"}
                            style={{
                              fontSize: "0.8rem",
                              fontWeight: 600,
                              color: "var(--brand-green, #008751)",
                              textDecoration: "underline",
                              textUnderlineOffset: "3px"
                            }}
                          >
                            Read full story ↗
                          </a>
                        )}
                      </div>
                    </div>
                  </article>
                );
              })}
            </div>
          )}

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="topic-pagination">
              {page > 1 && (
                <Link
                  to={`/source/${source.slug}?page=${page - 1}`}
                  className="topic-page-nav-btn"
                >
                  ← Previous
                </Link>
              )}
              <span className="topic-page-indicator">
                Page {page} of {totalPages}
              </span>
              {page < totalPages && (
                <Link
                  to={`/source/${source.slug}?page=${page + 1}`}
                  className="topic-page-nav-btn"
                >
                  Next →
                </Link>
              )}
            </div>
          )}
        </section>
      </main>

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}
