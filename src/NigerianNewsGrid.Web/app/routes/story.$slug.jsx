import React from "react";
import { useLoaderData, Link } from "react-router";
import { api, getOptimizedImageUrl } from "../../src/services/api";
import { formatTimeAgo, formatFullDate } from "../../src/services/timeUtils";
import Footer from "../../src/components/Footer";
import { toAbsoluteImageUrl, buildArticleItemList } from "../utils/seo";
import {
  ArrowLeft,
  Layers,
  Clock,
  ExternalLink,
  ShieldCheck,
  TrendingUp,
  Globe,
  Radio,
  Share2
} from "lucide-react";

export function meta({ data }) {
  if (!data?.story) {
    return [
      { title: "Story Cluster Not Found | News Stand NG" },
      { name: "robots", content: "noindex, nofollow" }
    ];
  }

  const { story } = data;
  const title = `${story.title} · How Nigerian Media Covered This | News Stand NG`;
  const description =
    story.summary ||
    `Comparative analysis and timeline of how ${story.sourceCount} Nigerian publishers reported on '${story.title}'.`;
  const canonicalUrl = `https://newsstand.ng/story/${story.slug}`;
  const imageUrl = toAbsoluteImageUrl(story.imageUrl);

  return [
    { title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    { name: "robots", content: "index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1" },
    { property: "og:type", content: "article" },
    { property: "og:site_name", content: "News Stand NG" },
    { property: "og:title", content: title },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { property: "og:image", content: imageUrl },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: title },
    { name: "twitter:description", content: description },
    { name: "twitter:image", content: imageUrl },
  ];
}

export async function loader({ params }) {
  const { slug } = params;
  if (!slug) {
    throw new Response("Story slug is required", { status: 400 });
  }

  try {
    const story = await api.getStoryBySlug(slug);
    if (!story) {
      throw new Response("Story Cluster Not Found", { status: 404 });
    }

    return { story };
  } catch (err) {
    if (err instanceof Response) throw err;
    console.warn(`[SSR Story Cluster Loader] Failed for slug '${slug}':`, err.message);
    throw new Response("Story Cluster Not Found", { status: 404 });
  }
}

export default function StoryClusterPage() {
  const { story } = useLoaderData();

  const storyItemList = buildArticleItemList(
    (story.articles || []).map((a) => ({ slug: a.slug, id: a.articleId, title: a.title }))
  );

  const collectionJsonLd = {
    "@context": "https://schema.org",
    "@type": "CollectionPage",
    "name": `${story.title} - Comparative Coverage`,
    "description": story.summary || `How Nigerian media covered '${story.title}'.`,
    "url": `https://newsstand.ng/story/${story.slug}`,
    "image": toAbsoluteImageUrl(story.imageUrl),
    ...(story.updatedAt ? { "dateModified": story.updatedAt } : {}),
    ...(storyItemList ? { "mainEntity": storyItemList } : {}),
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
        "name": story.title,
        "item": `https://newsstand.ng/story/${story.slug}`
      }
    ]
  };

  return (
    <div className="story-cluster-layout">
      {/* Structured Data */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(collectionJsonLd) }}
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
      <main className="story-cluster-container">
        {/* Breadcrumb */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li><span>Stories</span></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">{story.title}</li>
          </ol>
        </nav>

        {/* Story Cluster Hero */}
        <section className="story-cluster-hero">
          <div className="cluster-developing-badge">
            <Radio size={14} className="badge-pulse-icon" />
            <span>Cross-Publisher Story Cluster</span>
          </div>

          <h1 className="cluster-main-title">{story.title}</h1>

          {story.imageUrl && (
            <div className="story-cluster-hero-media" style={{ margin: '20px 0 24px', borderRadius: 16, overflow: 'hidden', maxHeight: 420 }}>
              <img
                src={getOptimizedImageUrl(story.imageUrl, 1200)}
                alt={story.title}
                className="story-cluster-hero-img"
                style={{ width: '100%', height: 'auto', maxHeight: 420, objectFit: 'cover', display: 'block' }}
                loading="eager"
                onError={(e) => {
                  e.target.onerror = null;
                  e.target.srcset = '';
                  e.target.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=1200&q=80';
                }}
              />
            </div>
          )}

          {story.summary && (
            <p className="cluster-main-summary">{story.summary}</p>
          )}

          {/* Source Diversity Gauge */}
          <div className="source-diversity-card">
            <div className="diversity-metrics-row">
              <div className="diversity-metric-item">
                <Globe size={18} className="metric-icon" />
                <div>
                  <span className="metric-value">{story.sourceCount}</span>
                  <span className="metric-label">Independent Outlets</span>
                </div>
              </div>

              <div className="diversity-metric-item">
                <Layers size={18} className="metric-icon" />
                <div>
                  <span className="metric-value">{story.articleCount}</span>
                  <span className="metric-label">Perspectives Aggregated</span>
                </div>
              </div>

              <div className="diversity-metric-item">
                <Clock size={18} className="metric-icon" />
                <div>
                  <span className="metric-value">{formatTimeAgo(story.updatedAt)}</span>
                  <span className="metric-label">Latest Development</span>
                </div>
              </div>
            </div>

            <div className="diversity-publishers-bar">
              <span className="publishers-label">Covered By:</span>
              <div className="publisher-tags-list">
                {(story.sources || []).map((source) => (
                  <span key={source} className="publisher-tag-pill">
                    {source}
                  </span>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* Multi-Perspective Coverage Section */}
        <section className="cluster-coverage-section">
          <div className="coverage-section-header">
            <h2 className="coverage-section-title">How Nigerian Media Covered This</h2>
            <p className="coverage-section-subtitle">
              Compare angles, headlines, and reporting across leading Nigerian newsrooms in chronological sequence.
            </p>
          </div>

          <div className="coverage-timeline-list">
            {(story.articles || []).map((article, index) => (
              <div key={article.articleId || article.slug || index} className="coverage-timeline-item">
                <div className="timeline-connector">
                  <div className="timeline-circle" />
                  {index < (story.articles.length - 1) && <div className="timeline-line" />}
                </div>

                <article className="coverage-card">
                  <div className="coverage-card-header">
                    <span className="coverage-source-badge">{article.source || "News Outlet"}</span>
                    {article.publishedAt && (
                      <span className="coverage-time-stamp">
                        <Clock size={12} />
                        {formatFullDate(article.publishedAt)}
                      </span>
                    )}
                  </div>

                  <div className="coverage-card-content-grid" style={{ display: 'flex', gap: '16px', alignItems: 'flex-start', marginBottom: '12px' }}>
                    {article.imageUrl && (
                      <div className="coverage-card-media" style={{ width: 120, height: 80, flexShrink: 0, borderRadius: 8, overflow: 'hidden' }}>
                        <img
                          src={getOptimizedImageUrl(article.imageUrl, 320)}
                          alt=""
                          style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                          loading="lazy"
                          onError={(e) => {
                            e.target.onerror = null;
                            e.target.srcset = '';
                            e.target.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=320&q=80';
                          }}
                        />
                      </div>
                    )}
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <h3 className="coverage-headline" style={{ marginTop: 0 }}>
                        <Link to={`/news/${article.slug || article.articleId}`}>
                          {article.title}
                        </Link>
                      </h3>

                      {article.excerpt && (
                        <p className="coverage-excerpt">{article.excerpt}</p>
                      )}
                    </div>
                  </div>

                  <div className="coverage-card-footer">
                    <Link
                      to={`/news/${article.slug || article.articleId}`}
                      className="coverage-summary-link"
                    >
                      <span>Read Overview</span>
                    </Link>

                    {article.url && (
                      <a
                        href={article.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="coverage-outbound-link"
                      >
                        <span>Full story on {article.source || "Publisher"}</span>
                        <ExternalLink size={13} />
                      </a>
                    )}
                  </div>
                </article>
              </div>
            ))}
          </div>
        </section>

        {/* Aggregator Attribution Disclaimer */}
        <section className="cluster-attribution-notice">
          <ShieldCheck size={20} className="notice-icon" />
          <div className="notice-content">
            <h4>Ethical Aggregation & Primary Source Attribution</h4>
            <p>
              News Stand NG aggregates verified public reporting from independent Nigerian news media to provide a balanced overview of fast-moving events. All reporting credits, trademarks, and copyright remain with the respective newsrooms.
            </p>
          </div>
        </section>
      </main>

      <Footer />
    </div>
  );
}
