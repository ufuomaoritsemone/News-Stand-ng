import React, { useState, useEffect, Suspense, lazy } from "react";
import { useLoaderData, Link, redirect } from "react-router";
import { api, getOptimizedImageUrl } from "../../src/services/api";
import { formatFullDate, formatTimeAgo, cleanEditorialSummary } from "../../src/services/timeUtils";
import { shareOnX, shareOnWhatsApp, shareGeneric } from "../../src/services/share";
import {
  trackArticleView,
  trackOutboundClick,
  trackBookmark,
  trackArticleShare,
} from "../../src/services/analytics";
import { XIcon, WhatsAppIcon } from "../../src/components/SocialIcons";
import Footer from "../../src/components/Footer";
import { toAbsoluteImageUrl, buildAuthorNode } from "../utils/seo";

const CommentSection = lazy(() => import("../../src/components/CommentSection"));
import {
  ExternalLink,
  Share2,
  Bookmark,
  ArrowLeft,
  Flame,
  Check,
  Tag,
  Clock,
  Compass,
  ArrowRight,
  Sparkles,
  HelpCircle,
  ChevronDown
} from "lucide-react";

export function meta({ data }) {
  if (!data?.article) {
    return [
      { title: "Story Not Found | News Stand NG" },
      { name: "robots", content: "noindex, nofollow" }
    ];
  }

  const { article } = data;
  const title = `${article.seoTitle || article.title} | News Stand NG`;
  const description =
    article.seoDescription ||
    article.excerpt ||
    article.summary ||
    "Read the latest curated news report on News Stand NG.";
  const canonicalUrl = `https://newsstand.ng/news/${article.slug}`;
  const imageUrl = toAbsoluteImageUrl(article.imageUrl);

  return [
    { title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    { name: "robots", content: "index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1" },
    { property: "og:type", content: "article" },
    { property: "og:site_name", content: "News Stand NG" },
    { property: "og:title", content: article.title },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { property: "og:image", content: imageUrl },
    { property: "og:image:secure_url", content: imageUrl },
    { property: "og:image:type", content: "image/jpeg" },
    { property: "og:image:width", content: "1200" },
    { property: "og:image:height", content: "630" },
    { property: "og:image:alt", content: article.title },
    { property: "article:published_time", content: article.publishedAt || "" },
    { property: "article:section", content: article.category || "General" },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: article.title },
    { name: "twitter:description", content: description },
    { name: "twitter:image", content: imageUrl },
    ...(article.imageUrl
      ? [{ tagName: "link", rel: "preload", as: "image", href: imageUrl, fetchpriority: "high" }]
      : []),
  ];
}

export async function loader({ params }) {
  const { slug } = params;
  if (!slug) {
    throw new Response("Slug is required", { status: 400 });
  }

  try {
    const article = await api.getArticleBySlug(slug);
    if (!article) {
      throw new Response("Article Not Found", { status: 404 });
    }

    // 301 Permanent Redirect if the slug was aliased or updated
    if (article.redirectSlug && article.redirectSlug !== slug) {
      return redirect(`/news/${article.redirectSlug}`, 301);
    }

    return { article };
  } catch (err) {
    if (err instanceof Response) throw err;
    console.warn(`[SSR News Loader] Failed for slug '${slug}':`, err.message);
    throw new Response("Article Not Found", { status: 404 });
  }
}

export default function ArticleLandingPage() {
  const { article: initialArticle } = useLoaderData();
  const [article, setArticle] = useState(initialArticle);
  const [copied, setCopied] = useState(false);
  const [isBookmarked, setIsBookmarked] = useState(false);
  const [isGeneratingSummary, setIsGeneratingSummary] = useState(false);
  const [summaryError, setSummaryError] = useState(null);
  const [isSummaryExpanded, setIsSummaryExpanded] = useState(true);

  useEffect(() => {
    setArticle(initialArticle);
  }, [initialArticle]);

  const handleGenerateSummary = async () => {
    if (isGeneratingSummary || !article) return;
    setIsGeneratingSummary(true);
    setSummaryError(null);
    try {
      const summaryResult = await api.generateAiSummary(article.slug || article.id);
      if (summaryResult) {
        setArticle((prev) => ({
          ...prev,
          enrichment: {
            ...(prev.enrichment || {}),
            fullSummary: summaryResult,
            engine: summaryResult?.engine || "Gemini-3.1-Flash-Lite"
          }
        }));
      }
    } catch (err) {
      console.warn("[AI Summary] Generation failed:", err.message);
      setSummaryError("Could not generate summary at this time. Please try again.");
    } finally {
      setIsGeneratingSummary(false);
    }
  };

  useEffect(() => {
    if (article) {
      trackArticleView(article);
    }
    try {
      const saved = JSON.parse(localStorage.getItem("nng_saved_stories") || "[]");
      setIsBookmarked(saved.some((s) => s.id === article?.id));
    } catch {}
  }, [article?.id]);

  const toggleBookmark = () => {
    try {
      const saved = JSON.parse(localStorage.getItem("nng_saved_stories") || "[]");
      const exists = saved.some((s) => s.id === article.id);
      let updated;
      if (exists) {
        updated = saved.filter((s) => s.id !== article.id);
        setIsBookmarked(false);
        trackBookmark('remove', article.id, article.title);
      } else {
        updated = [article, ...saved];
        setIsBookmarked(true);
        trackBookmark('add', article.id, article.title);
      }
      localStorage.setItem("nng_saved_stories", JSON.stringify(updated));
    } catch {}
  };

  const handleCopyLink = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setCopied(true);
      trackArticleShare('clipboard', article.title, article.id);
      setTimeout(() => setCopied(false), 2200);
    } catch {}
  };

  const handleNativeShare = async () => {
    const res = await shareGeneric(article);
    if (res?.method === "clipboard") {
      setCopied(true);
      setTimeout(() => setCopied(false), 2200);
    }
  };

  const newsArticleJsonLd = {
    "@context": "https://schema.org",
    "@type": "NewsArticle",
    "mainEntityOfPage": {
      "@type": "WebPage",
      "@id": `https://newsstand.ng/news/${article.slug}`
    },
    "headline": article.title,
    "description": article.excerpt || article.summary || article.title,
    "image": [toAbsoluteImageUrl(article.imageUrl)],
    "datePublished": article.publishedAt,
    "dateModified": article.updatedAt || article.publishedAt,
    "articleSection": article.category || "General",
    "inLanguage": "en-NG",
    "author": buildAuthorNode(article),
    ...(article.sourceUrl ? { "isBasedOn": article.sourceUrl } : {}),
    "publisher": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng",
      "logo": {
        "@type": "ImageObject",
        "url": "https://newsstand.ng/logo.png",
        "width": 512,
        "height": 512
      }
    }
  };

  const breadcrumbsJsonLd = {
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
        "name": article.category || "News",
        "item": `https://newsstand.ng/category/${encodeURIComponent((article.category || "general").toLowerCase())}`
      },
      {
        "@type": "ListItem",
        "position": 3,
        "name": article.title,
        "item": `https://newsstand.ng/news/${article.slug}`
      }
    ]
  };

  const faqPageJsonLd =
    article.enrichment?.faqs && article.enrichment.faqs.length > 0
      ? {
          "@context": "https://schema.org",
          "@type": "FAQPage",
          "mainEntity": article.enrichment.faqs.map((faq) => ({
            "@type": "Question",
            "name": faq.question,
            "acceptedAnswer": {
              "@type": "Answer",
              "text": faq.answer
            }
          }))
        }
      : null;

  return (
    <div className="article-page-layout">
      {/* Structured Data (Exclude sponsored from NewsArticle schema) */}
      {!article.isSponsored && (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(newsArticleJsonLd) }}
        />
      )}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumbsJsonLd) }}
      />
      {faqPageJsonLd && (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(faqPageJsonLd) }}
        />
      )}

      {/* Top Header / Masthead */}
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

          <div className="article-header-actions">
            <button
              onClick={toggleBookmark}
              className={`article-action-btn ${isBookmarked ? "active" : ""}`}
              aria-label="Bookmark Story"
            >
              <Bookmark size={17} fill={isBookmarked ? "currentColor" : "none"} />
            </button>
            <button
              onClick={handleNativeShare}
              className="article-action-btn"
              aria-label="Share Story"
            >
              <Share2 size={17} />
            </button>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="article-page-container">
        {/* Breadcrumb Navigation */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            {article.category && (
              <>
                <li>
                  <Link to={`/category/${encodeURIComponent(article.category.toLowerCase())}`}>
                    {article.category}
                  </Link>
                </li>
                <li><span className="breadcrumb-sep">/</span></li>
              </>
            )}
            <li className="breadcrumb-active" aria-current="page">Story</li>
          </ol>
        </nav>

        {/* Story Masthead */}
        <article className="article-detail-sheet">
          <div className="article-badge-row">
            {article.isSponsored ? (
              <span className="sponsored-chip">Sponsored</span>
            ) : null}
            {article.category && (
              <Link
                to={`/category/${encodeURIComponent(article.category.toLowerCase())}`}
                className="article-category-chip"
              >
                {article.category}
              </Link>
            )}
            {article.publishedAt && (
              <span className="article-timestamp-chip">
                <Clock size={12} />
                <span>{formatTimeAgo(article.publishedAt)}</span>
              </span>
            )}
          </div>

          <h1 className="article-main-title">{article.title}</h1>

          {/* Byline / Source Row */}
          <div className="article-meta-row">
            <div className="article-source-profile">
              {article.sourceLogo ? (
                <img src={article.sourceLogo} alt={article.source} className="source-avatar-img" />
              ) : (
                <div className="source-avatar-fallback">
                  {(article.source || "N").charAt(0).toUpperCase()}
                </div>
              )}
              <div className="source-meta-text">
                <div className="source-name-label">{article.source || "News Stand NG"}</div>
                {article.author ? (
                  <div className="source-author-byline">Reported by {article.author}</div>
                ) : (
                  <div className="source-author-byline">Editorial Desk</div>
                )}
              </div>
            </div>

            <div className="article-date-stamp">
              {formatFullDate(article.publishedAt)}
            </div>
          </div>

          {/* Lead Media Banner */}
          {article.imageUrl && (
            <figure className="article-hero-media">
              <img
                src={getOptimizedImageUrl(article.imageUrl, 1200)}
                srcSet={`${getOptimizedImageUrl(article.imageUrl, 640)} 640w, ${getOptimizedImageUrl(article.imageUrl, 960)} 960w, ${getOptimizedImageUrl(article.imageUrl, 1200)} 1200w`}
                sizes="(max-width: 768px) 100vw, 840px"
                alt={article.title}
                className="article-hero-img"
                loading="eager"
                fetchPriority="high"
                width="1200"
                height="675"
                decoding="async"
                onError={(e) => {
                  e.target.onerror = null;
                  e.target.srcset = '';
                  e.target.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=1200&q=80';
                }}
              />
              <figcaption className="article-hero-caption">
                Photo courtesy of {article.source || "Publisher"} · Verified via News Stand NG
              </figcaption>
            </figure>
          )}

          {/* Publisher Referral Notice (Aggregator Compliance & Attribution) */}
          <div className="publisher-referral-banner">
            <div className="referral-banner-header">
              <Compass size={18} className="referral-banner-icon" />
              <div className="referral-banner-title">
                {article.isSponsored
                  ? `Sponsored Briefing · Partner: ${article.sponsorName || article.source || "Official Sponsor"}`
                  : `Source Attribution: ${article.source || "Original Publisher"}`}
              </div>
            </div>
            <p className="referral-banner-body">
              {article.isSponsored
                ? "This article is sponsored commercial content presented in collaboration with our verified partners. News Stand NG labels all paid features transparently."
                : `News Stand NG operates under ethical news aggregation guidelines. Source: ${article.source || "Original Publisher"}. To read the unabridged investigative reporting and full gallery, visit the original publication.`}
            </p>
            {(article.sourceUrl || article.sponsorUrl) && (
              <a
                href={article.isSponsored ? (article.sponsorUrl || article.sourceUrl) : article.sourceUrl}
                target="_blank"
                rel={article.isSponsored ? "sponsored noopener" : "noopener noreferrer"}
                className="referral-primary-btn"
                onClick={() => trackOutboundClick(article, article.isSponsored ? (article.sponsorUrl || article.sourceUrl) : article.sourceUrl)}
              >
                <span>Read Full Story on {article.isSponsored ? (article.sponsorName || article.source || "Partner") : (article.source || "Publisher")}</span>
                <ExternalLink size={15} />
              </a>
            )}
          </div>

          {/* AI Executive Briefing (Full-Body Synthesis with Gemini 3.1 Flash-Lite) */}
          {article.enrichment?.fullSummary ? (
            <div className="ai-executive-briefing-box" aria-label="AI Executive Briefing">
              <div className="briefing-header">
                <div className="briefing-header-left">
                  <div className="briefing-badge">
                    <Sparkles size={16} className="briefing-icon" />
                    <span>AI Executive Briefing</span>
                  </div>
                  <span className="briefing-engine-tag">
                    {article.enrichment.engine?.toLowerCase().includes("gemini")
                      ? "Gemini Flash AI"
                      : "Verified Synthesis"}
                  </span>
                  <span className="briefing-reading-time">
                    <Clock size={12} />
                    <span>~{article.enrichment.fullSummary.readingTimeSeconds || 45}s read</span>
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => setIsSummaryExpanded(!isSummaryExpanded)}
                  className={`briefing-toggle-btn ${isSummaryExpanded ? "expanded" : ""}`}
                  aria-label={isSummaryExpanded ? "Collapse briefing" : "Expand briefing"}
                >
                  <ChevronDown size={18} />
                </button>
              </div>

              <div className="briefing-overview">
                {article.enrichment.fullSummary.executiveOverview}
              </div>

              {isSummaryExpanded && (
                <div className="briefing-subsections">
                  {article.enrichment.fullSummary.contextAndBackground &&
                    article.enrichment.fullSummary.contextAndBackground.length > 0 && (
                      <div className="briefing-section">
                        <div className="briefing-section-title">Context & Background</div>
                        <ul className="briefing-bullet-list">
                          {article.enrichment.fullSummary.contextAndBackground.map((item, idx) => (
                            <li key={idx} className="briefing-bullet-item">
                              <span className="briefing-bullet-dot">✦</span>
                              <span>{item}</span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                  {article.enrichment.fullSummary.keyFiguresAndEntities &&
                    article.enrichment.fullSummary.keyFiguresAndEntities.length > 0 && (
                      <div className="briefing-section">
                        <div className="briefing-section-title">Key Figures & Named Entities</div>
                        <div className="briefing-figures-grid">
                          {article.enrichment.fullSummary.keyFiguresAndEntities.map((fig, idx) => (
                            <span key={idx} className="briefing-figure-chip">
                              <strong>{fig.includes(":") ? fig.split(":")[0] + ":" : "•"}</strong>
                              <span>{fig.includes(":") ? fig.substring(fig.indexOf(":") + 1).trim() : fig}</span>
                            </span>
                          ))}
                        </div>
                      </div>
                    )}

                  {article.enrichment.fullSummary.outlookOrNextSteps && (
                    <div className="briefing-section">
                      <div className="briefing-section-title">Outlook & Next Steps</div>
                      <div className="briefing-outlook-box">
                        {article.enrichment.fullSummary.outlookOrNextSteps}
                      </div>
                    </div>
                  )}

                  <div className="briefing-attribution-footer">
                    <span>
                      Synthesized by AI from reporting by <strong>{article.isSponsored ? (article.sponsorName || article.source || "Partner") : (article.source || "Publisher")}</strong>.
                    </span>
                    {(article.sourceUrl || article.url) && (
                      <a
                        href={article.sourceUrl || article.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="briefing-publisher-link"
                      >
                        <span>Read full story on {article.source || "Publisher"}</span>
                        <ExternalLink size={12} />
                      </a>
                    )}
                  </div>
                </div>
              )}
            </div>
          ) : isGeneratingSummary ? (
            <div className="briefing-shimmer-box" aria-live="polite">
              <div className="briefing-header">
                <div className="briefing-badge">
                  <Sparkles size={16} className="briefing-icon" />
                  <span>Synthesizing Full Article with Gemini AI...</span>
                </div>
              </div>
              <div className="shimmer-line" style={{ width: "95%" }} />
              <div className="shimmer-line" style={{ width: "88%" }} />
              <div className="shimmer-line" style={{ width: "70%" }} />
            </div>
          ) : (
            <div className="briefing-generate-card">
              <div className="briefing-generate-info">
                <div className="briefing-generate-title">
                  <Sparkles size={17} className="briefing-icon" />
                  <span>AI Executive Briefing</span>
                </div>
                <div className="briefing-generate-desc">
                  Generate a deep executive synthesis of the full article with Gemini 3.1 Flash-Lite in ~2 seconds.
                </div>
                {summaryError && (
                  <div style={{ color: "#ef4444", fontSize: "0.78rem", marginTop: "4px" }}>
                    {summaryError}
                  </div>
                )}
              </div>
              <button
                type="button"
                onClick={handleGenerateSummary}
                className="briefing-generate-btn"
                disabled={isGeneratingSummary}
              >
                <Sparkles size={15} />
                <span>Generate Summary</span>
              </button>
            </div>
          )}

          {/* Key Takeaways Box (Phase 7: Extractive AI Summarization & 0% Hallucination) */}
          {article.enrichment?.keyTakeaways && article.enrichment.keyTakeaways.length > 0 && (
            <div className="key-takeaways-box" aria-label="Key Takeaways">
              <div className="takeaways-header">
                <div className="takeaways-badge">
                  <Sparkles size={15} className="takeaways-icon" />
                  <span>Key Takeaways</span>
                </div>
                <span className="takeaways-engine-tag">
                  {article.enrichment.engine?.toLowerCase().includes("gemini") ? "AI Summary" : "Verified Extract"}
                </span>
              </div>
              <ul className="takeaways-list">
                {article.enrichment.keyTakeaways.map((bullet, idx) => (
                  <li key={idx} className="takeaways-item">
                    <span className="takeaways-bullet-icon">✦</span>
                    <span className="takeaways-text">{bullet}</span>
                  </li>
                ))}
              </ul>
              {article.enrichment.timelineFacts && article.enrichment.timelineFacts.length > 0 && (
                <div className="timeline-facts-strip">
                  <span className="timeline-strip-label">Key Facts:</span>
                  {article.enrichment.timelineFacts.slice(0, 4).map((fact, idx) => (
                    <span key={idx} className="timeline-fact-chip">
                      <strong>{fact.dateOrTime}:</strong> {fact.fact}
                    </span>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Excerpt Body */}
          <div className="article-lead-excerpt">
            <div className="excerpt-label">Editorial Excerpt</div>
            <p className="excerpt-paragraph">
              {cleanEditorialSummary(article.excerpt || article.summary || "")}
            </p>
          </div>

          {/* Social Broadcasting & Action Pill Bar */}
          <div className="article-toolbar-shelf">
            <div className="toolbar-actions-left">
              <button
                onClick={() => shareOnX(article)}
                className="action-pill-btn x-pill"
                title="Share on X"
              >
                <XIcon size={14} />
                <span>Post</span>
              </button>

              <button
                onClick={() => shareOnWhatsApp(article)}
                className="action-pill-btn wa-pill"
                title="Share on WhatsApp"
              >
                <WhatsAppIcon size={15} />
                <span>WhatsApp</span>
              </button>

              <button
                onClick={handleNativeShare}
                className="action-pill-btn share-pill"
                title="Share Story"
              >
                {copied ? <Check size={14} /> : <Share2 size={14} />}
                <span>{copied ? "Copied!" : "Share"}</span>
              </button>
            </div>

            <div className="toolbar-actions-right">
              <button
                onClick={toggleBookmark}
                className={`action-pill-btn bookmark-pill ${isBookmarked ? "active" : ""}`}
              >
                <Bookmark size={14} fill={isBookmarked ? "currentColor" : "none"} />
                <span>{isBookmarked ? "Saved" : "Save Story"}</span>
              </button>
            </div>
          </div>

          {/* Story FAQ Accordion (Phase 7: Structured Q&A & Search Rich Snippets) */}
          {article.enrichment?.faqs && article.enrichment.faqs.length > 0 && (
            <section className="story-faq-section" aria-label="Frequently Asked Questions">
              <h2 className="faq-section-title">
                <HelpCircle size={18} />
                <span>Frequently Asked Questions About This Story</span>
              </h2>
              <div className="faq-accordion-group">
                {article.enrichment.faqs.map((faq, idx) => (
                  <details key={idx} className="story-faq-item" open={idx === 0}>
                    <summary className="story-faq-question">
                      <span className="faq-q-text">{faq.question}</span>
                      <ChevronDown size={16} className="faq-chevron" />
                    </summary>
                    <div className="story-faq-answer">
                      <p>{faq.answer}</p>
                    </div>
                  </details>
                ))}
              </div>
            </section>
          )}

          {/* Tagged Topics Shelf */}
          {Array.isArray(article.topics) && article.topics.length > 0 && (
            <section className="article-topics-shelf">
              <h2 className="shelf-section-title">
                <Tag size={16} />
                <span>Related Topics & Entities</span>
              </h2>
              <div className="topics-pill-cloud">
                {article.topics.map((topic) => (
                  <Link
                    key={topic.id || topic.slug}
                    to={`/topic/${topic.slug}`}
                    className="topic-cloud-pill"
                  >
                    <span className="topic-type-dot" />
                    <span className="topic-name">{topic.name}</span>
                  </Link>
                ))}
              </div>
            </section>
          )}

          {/* Related Stories Shelf */}
          {Array.isArray(article.relatedArticles) && article.relatedArticles.length > 0 && (
            <section className="article-related-shelf">
              <h2 className="shelf-section-title">
                <Flame size={16} />
                <span>More Coverage on This Story</span>
              </h2>
              <div className="related-stories-grid">
                {article.relatedArticles.map((rel) => (
                  <Link
                    key={rel.id}
                    to={`/news/${rel.slug || rel.id}`}
                    className="related-story-card"
                  >
                    {rel.imageUrl && (
                      <div className="related-thumb-container">
                        <img
                          src={getOptimizedImageUrl(rel.imageUrl, 400)}
                          alt=""
                          className="related-thumb-img"
                          loading="lazy"
                          onError={(e) => {
                            e.target.onerror = null;
                            e.target.srcset = '';
                            e.target.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=400&q=80';
                          }}
                        />
                      </div>
                    )}
                    <div className="related-card-details">
                      <div className="related-source-line">{rel.source || "News Stand NG"}</div>
                      <h3 className="related-title">{rel.title}</h3>
                      {rel.publishedAt && (
                        <div className="related-time">{formatTimeAgo(rel.publishedAt)}</div>
                      )}
                    </div>
                  </Link>
                ))}
              </div>
            </section>
          )}

          {/* Outbound Deep-Link Callout */}
          {article.sourceUrl && (
            <div className="outbound-cta-container">
              <a
                href={article.sourceUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="outbound-big-cta"
                onClick={() => trackOutboundClick(article, article.sourceUrl)}
              >
                <div className="outbound-cta-text">
                  <div className="outbound-cta-lead">Continue Reading Full Article</div>
                  <div className="outbound-cta-sub">Redirecting to original article on {article.source || "publisher"}</div>
                </div>
                <ArrowRight size={20} />
              </a>
            </div>
          )}

          {/* Community Discussion */}
          <section className="article-comments-container">
            <Suspense fallback={<div className="skeleton" style={{ minHeight: 180, borderRadius: 12, margin: '1.5rem 0' }} />}>
              <CommentSection articleId={article.id} />
            </Suspense>
          </section>
        </article>
      </main>

      <Footer />
    </div>
  );
}
