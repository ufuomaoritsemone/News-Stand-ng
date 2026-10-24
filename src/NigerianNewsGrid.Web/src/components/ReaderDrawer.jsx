import React, { useState, useEffect, useRef, Suspense, lazy } from 'react';
import { X, Bookmark, Share2, ExternalLink, Type, Flame, Megaphone, ArrowRight, ArrowLeft, Sparkles, Clock, ChevronDown, CheckCircle, Zap, Layers } from 'lucide-react';
import { api, getOptimizedImageUrl } from '../services/api';
import { XIcon, WhatsAppIcon } from './SocialIcons';
import { shareOnX, shareOnWhatsApp, shareGeneric } from '../services/share';
import { getRecencyTag, isHotStory } from '../services/articleMetadata';
import { formatFullDate, cleanEditorialSummary } from '../services/timeUtils';
import { trackArticleView, trackOutboundClick } from '../services/analytics';

const CommentSection = lazy(() => import('./CommentSection'));

export default function ReaderDrawer({
  article,
  isOpen,
  onClose,
  isBookmarked,
  onToggleBookmark,
  onSelectRelatedStory,
  currentUser,
  onOpenAuth,
  featuredSponsoredStory,
  headerSponsor = null,
  midContentSponsor = null,
  footerSponsor = null,
  onSelectStory,
  initialScrollToComments = false,
}) {
  const [fontSizeLevel, setFontSizeLevel] = useState(1); // 0: small, 1: medium, 2: large
  const [scrollProgress, setScrollProgress] = useState(0);
  const [relatedStories, setRelatedStories] = useState([]);
  const [copied, setCopied] = useState(false);
  const [enrichment, setEnrichment] = useState(article?.enrichment || null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [isSummaryExpanded, setIsSummaryExpanded] = useState(true);
  const drawerRef = useRef(null);

  const fontSizes = ['1rem', '1.12rem', '1.25rem'];

  // Listen for escape key & reset scroll on article change
  useEffect(() => {
    if (isOpen) {
      const handleKeyDown = (e) => {
        if (e.key === 'Escape') onClose();
      };
      window.addEventListener('keydown', handleKeyDown);
      return () => window.removeEventListener('keydown', handleKeyDown);
    }
  }, [isOpen, onClose]);

  // Scroll to comments if opened via comment button
  useEffect(() => {
    if (isOpen && initialScrollToComments && drawerRef.current) {
      const timer = setTimeout(() => {
        const commentSection = drawerRef.current?.querySelector('.comment-section');
        if (commentSection) {
          commentSection.scrollIntoView({ behavior: 'smooth' });
        }
      }, 350);
      return () => clearTimeout(timer);
    }
  }, [isOpen, initialScrollToComments, article]);

  // Handle selecting a related story (scroll to top and update active article)
  const handleSelectRelated = (rel) => {
    if (!rel) return;
    if (drawerRef.current) {
      drawerRef.current.scrollTop = 0;
    }
    if (onSelectRelatedStory) {
      onSelectRelatedStory(rel);
    } else if (onSelectStory) {
      onSelectStory(rel);
    }
  };

  // Fetch related stories when an article is opened
  useEffect(() => {
    if (article && isOpen) {
      if (!initialScrollToComments) {
        setScrollProgress(0);
        if (drawerRef.current) drawerRef.current.scrollTop = 0;
      }

      api.getRelatedStories(article.id, article.title, article.category, 4)
        .then(async (res) => {
          let list = [];
          if (Array.isArray(res)) {
            list = res;
          } else if (res && Array.isArray(res.articles)) {
            list = res.articles;
          } else if (res && Array.isArray(res.data)) {
            list = res.data;
          }

          // Filter out current article
          list = list.filter((item) => item && item.id !== article.id);

          // If fewer than 4 stories, fallback to fetching recent stories in the same category
          if (list.length < 4 && article.category) {
            try {
              const catRes = await api.getArticles({
                category: article.category !== 'All' ? article.category : '',
                pageSize: 8,
              });
              const rawCat = catRes?.articles || (Array.isArray(catRes) ? catRes : []);
              const catArticles = rawCat.filter((a) => a && a.id !== article.id);

              const seen = new Set(list.map((a) => a.id));
              for (const a of catArticles) {
                if (!seen.has(a.id)) {
                  list.push(a);
                  seen.add(a.id);
                  if (list.length >= 4) break;
                }
              }
            } catch {
              // silent fallback
            }
          }
          setRelatedStories(list.slice(0, 4));
        })
        .catch(async () => {
          try {
            const catRes = await api.getArticles({
              category: article.category !== 'All' ? article.category : '',
              pageSize: 6,
            });
            const rawCat = catRes?.articles || (Array.isArray(catRes) ? catRes : []);
            setRelatedStories(rawCat.filter((a) => a && a.id !== article.id).slice(0, 4));
          } catch {
            setRelatedStories([]);
          }
        });

      // Track engagement telemetry
      api.trackEngagement(article.id, 'read_open');
      trackArticleView(article);

      // Track sponsored impression only if viewing an actual sponsored article
      if (article.isSponsored) {
        api.trackSponsoredImpression(article.id);
      } else {
        if (headerSponsor?.id) api.trackSponsoredImpression(headerSponsor.id);
        if (midContentSponsor?.id) api.trackSponsoredImpression(midContentSponsor.id);
        if ((footerSponsor || featuredSponsoredStory)?.id) {
          api.trackSponsoredImpression((footerSponsor || featuredSponsoredStory).id);
        }
      }
    }
  }, [article, isOpen, headerSponsor, midContentSponsor, footerSponsor, featuredSponsoredStory]);

  // Synchronize enrichment or fetch on article change
  useEffect(() => {
    if (article && isOpen) {
      if (article.enrichment) {
        setEnrichment(article.enrichment);
      } else if (article.id) {
        api.getArticleEnrichment(article.id)
          .then((res) => {
            if (res) setEnrichment(res);
          })
          .catch(() => {
            if (article.aiSummary) {
              setEnrichment({
                fullSummary: {
                  executiveOverview: article.aiSummary,
                  readingTimeSeconds: 45,
                },
                engine: article.aiSummaryEngine || 'AI Synthesis',
              });
            } else {
              setEnrichment(null);
            }
          });
      } else {
        setEnrichment(null);
      }
    } else {
      setEnrichment(null);
    }
  }, [article, isOpen]);

  const handleGenerateAiSummary = async () => {
    if (!article?.id || isGenerating) return;
    setIsGenerating(true);
    try {
      const res = await api.generateAiSummary(article.id);
      if (res) {
        const fullEnr = await api.getArticleEnrichment(article.id).catch(() => null);
        setEnrichment(fullEnr || {
          fullSummary: res,
          keyTakeaways: [],
          engine: res?.engine || 'Gemini-3.1-Flash-Lite',
        });
      }
    } catch (err) {
      console.warn('AI summary generation notice:', err.message);
    } finally {
      setIsGenerating(false);
    }
  };

  // Handle scroll progress
  const handleScroll = (e) => {
    const el = e.currentTarget;
    const total = el.scrollHeight - el.clientHeight;
    if (total > 0) {
      setScrollProgress((el.scrollTop / total) * 100);
    }
  };

  const handleShare = async () => {
    if (!article) return;
    const res = await shareGeneric(article);
    if (res?.method === 'clipboard') {
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    }
  };

  if (!isOpen || !article) return null;

  const formattedDate = article.publishedAt
    ? new Date(article.publishedAt).toLocaleDateString('en-NG', {
        weekday: 'short',
        month: 'short',
        day: 'numeric',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      })
    : '';

  return (
    <>
      <div
        className="drawer-backdrop"
        onClick={onClose}
        aria-hidden="true"
      />

      <aside
        ref={drawerRef}
        className="reader-drawer"
        onScroll={handleScroll}
        role="dialog"
        aria-modal="true"
        aria-label={`Article Reader: ${article.title}`}
        style={{ '--reader-font-size': fontSizes[fontSizeLevel] }}
      >
        {/* Scroll Reading Progress Bar */}
        <div
          className="reader-progress-bar"
          style={{ width: `${scrollProgress}%` }}
        />

        {/* Top Control Bar */}
        <div className="reader-top-bar">
          <div className="reader-top-bar-left">
            <button
              type="button"
              className="reader-back-btn"
              onClick={onClose}
              aria-label="Back to headlines"
              title="Back to headlines"
            >
              <ArrowLeft size={16} />
              <span className="reader-back-text">Back</span>
            </button>

            <div className="reader-font-controls">
              <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginRight: 4, display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                <Type size={14} /> Text
              </span>
              <button
                type="button"
                className="icon-btn"
                style={{ width: 28, height: 28, fontSize: '0.75rem', fontWeight: 700 }}
                onClick={() => setFontSizeLevel((p) => Math.max(0, p - 1))}
                title="Decrease font size"
              >
                A-
              </button>
              <button
                type="button"
                className="icon-btn"
                style={{ width: 28, height: 28, fontSize: '0.85rem', fontWeight: 800 }}
                onClick={() => setFontSizeLevel((p) => Math.min(2, p + 1))}
                title="Increase font size"
              >
                A+
              </button>
            </div>
          </div>

          <div className="reader-toolbar-actions">
            {/* Bookmark button */}
            <div className="btn-callout-wrapper">
              <button
                type="button"
                className={`drawer-toolbar-btn ${isBookmarked ? 'active' : ''}`}
                onClick={() => onToggleBookmark(article)}
                aria-label={isBookmarked ? 'Remove bookmark' : 'Bookmark article'}
              >
                <Bookmark
                  size={14}
                  fill={isBookmarked ? 'var(--gold-accent)' : 'none'}
                  color={isBookmarked ? 'var(--gold-accent)' : 'currentColor'}
                />
                <span>{isBookmarked ? 'Saved' : 'Save'}</span>
              </button>
              <div className="btn-callout bottom">
                <span className="btn-callout-title">{isBookmarked ? 'Saved Story' : 'Save Article'}</span>
                <span className="btn-callout-desc">
                  {isBookmarked ? 'Click to remove from your bookmarks' : 'Add to your reading list for offline reading'}
                </span>
              </div>
            </div>

            {/* Share on X (Twitter) */}
            <div className="btn-callout-wrapper">
              <button
                type="button"
                className="drawer-toolbar-btn share-x-btn"
                onClick={() => shareOnX(article)}
                aria-label="Post to X (Twitter)"
              >
                <XIcon size={13} />
                <span>Post</span>
              </button>
              <div className="btn-callout bottom">
                <span className="btn-callout-title">Share on X</span>
                <span className="btn-callout-desc">Broadcast headline and link to your X timeline</span>
              </div>
            </div>

            {/* Share on WhatsApp */}
            <div className="btn-callout-wrapper">
              <button
                type="button"
                className="drawer-toolbar-btn share-wa-btn"
                onClick={() => shareOnWhatsApp(article)}
                aria-label="Share on WhatsApp"
              >
                <WhatsAppIcon size={14} color="#25D366" />
                <span>WhatsApp</span>
              </button>
              <div className="btn-callout bottom">
                <span className="btn-callout-title">Share on WhatsApp</span>
                <span className="btn-callout-desc">Send story to WhatsApp contacts and status</span>
              </div>
            </div>

            {/* Share / Copy button */}
            <div className="btn-callout-wrapper">
              <button
                type="button"
                className="drawer-toolbar-btn"
                onClick={handleShare}
                aria-label={copied ? 'Link copied' : 'Copy link or Share'}
              >
                <Share2 size={14} />
                <span>{copied ? 'Copied!' : 'Share'}</span>
              </button>
              <div className="btn-callout bottom">
                <span className="btn-callout-title">{copied ? 'Link Copied!' : 'Share Story'}</span>
                <span className="btn-callout-desc">
                  {copied ? 'Preview link copied to clipboard' : 'Copy link or trigger device share menu'}
                </span>
              </div>
            </div>

            {/* Close button */}
            <button
              type="button"
              className="icon-btn reader-close-btn"
              onClick={onClose}
              title="Close reader (Esc)"
              aria-label="Close reader"
            >
              <X size={18} />
            </button>
          </div>
        </div>

        {/* Reader Body */}
        <div className="reader-content-body">
          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8, marginBottom: 12 }}>
            <span className="card-category-badge" style={{ position: 'static' }}>
              {article.category || 'National'}
            </span>
            <span style={{ color: 'var(--emerald-bright)', fontWeight: 700, fontSize: '0.85rem' }}>
              {article.source || 'News Stand NG'}
            </span>

            {/* Recency Tag */}
            {(() => {
              const rec = getRecencyTag(article.publishedAt);
              return rec ? (
                <span className={`recency-pill ${rec.badgeClass}`}>
                  {rec.label}
                </span>
              ) : null;
            })()}

            {/* Quick Read Badge */}
            {(enrichment || article.hasAiSummary || article.aiSummary) && (
              <span className="recency-pill mini quick-read-pill" title="AI Quick Read available">
                ⚡ Quick Read
              </span>
            )}

            {/* Fire Badge on hot discussion */}
            {isHotStory(article) && (
              <span className="story-fire-badge reader-fire-badge" title={`${article.commentCount} comments`}>
                <Flame size={12} className="fire-pulse-icon" />
                <span>{article.commentCount} comments</span>
              </span>
            )}

            {article.isSponsored && (
              <span className="sponsored-badge-pill">📢 Sponsored · {article.sponsorName || 'Partner'}</span>
            )}
          </div>

          {article.isSponsored && (
            <div className="sponsored-editorial-banner">
              <Megaphone size={18} className="sponsored-banner-icon" />
              <div className="sponsored-banner-text">
                <span className="sponsored-banner-title">SPONSORED EDITORIAL</span>
                <span className="sponsored-banner-subtitle">
                  Presented by <strong>{article.sponsorName || 'Corporate Partner'}</strong> · Partner Studio Content
                </span>
              </div>
            </div>
          )}

          {/* In-Article Placement 1: Top Header Sponsor Bar ("Presented by") */}
          {!article.isSponsored && headerSponsor && (
            <div className="in-article-header-sponsor-bar">
              <div className="header-sponsor-left">
                <span className="header-sponsor-badge">PRESENTED BY</span>
                <span className="header-sponsor-name">{headerSponsor.sponsorName || 'Partner'}</span>
              </div>
              <div className="header-sponsor-right">
                {headerSponsor.title && (
                  <span className="header-sponsor-title-snippet">{headerSponsor.title}</span>
                )}
                {headerSponsor.sponsorUrl && (
                  <a
                    href={headerSponsor.sponsorUrl}
                    target="_blank"
                    rel="sponsored noopener noreferrer"
                    className="header-sponsor-cta-btn"
                    onClick={() => api.trackSponsoredClick(headerSponsor.id)}
                  >
                    <span>Visit Partner</span>
                    <ExternalLink size={12} />
                  </a>
                )}
              </div>
            </div>
          )}

          <h1 className="reader-title">{article.title}</h1>

          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 10, fontSize: '0.82rem', color: 'var(--text-muted)', marginBottom: 20 }}>
            {article.author && <span>By {article.author}</span>}
            {article.author && <span>·</span>}
            <span>{formatFullDate(article.publishedAt)}</span>
            {article.url && (
              <>
                <span>·</span>
                <a
                  href={article.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{ color: 'var(--emerald-bright)', display: 'inline-flex', alignItems: 'center', gap: 3, fontWeight: 600 }}
                  title={`Read original article on ${article.source || 'Publisher'}`}
                  onClick={() => trackOutboundClick(article, article.url)}
                >
                  <span>Read on {article.source || 'Publisher'}</span>
                  <ExternalLink size={12} />
                </a>
              </>
            )}
          </div>

          {/* Featured Image */}
          {article.imageUrl && (
            <img
              src={getOptimizedImageUrl(article.imageUrl, 1000)}
              alt={article.title}
              className="reader-hero-image"
              loading="lazy"
              decoding="async"
              width="1000"
              height="562"
              onError={(e) => { e.target.style.display = 'none'; }}
            />
          )}

          {/* ⚡ AI Quick Read & Executive Briefing */}
          {enrichment?.fullSummary ? (
            <div className="ai-executive-briefing-box" aria-label="AI Quick Read Executive Briefing">
              <div className="briefing-header">
                <div className="briefing-header-left">
                  <div className="briefing-badge">
                    <Sparkles size={16} className="briefing-icon" />
                    <span>⚡ Quick Read</span>
                  </div>
                  <span className="briefing-engine-tag">
                    {enrichment.engine?.toLowerCase().includes("gemini")
                      ? "Gemini Flash AI"
                      : "Verified AI Summary"}
                  </span>
                  <span className="briefing-reading-time">
                    <Clock size={12} />
                    <span>~{enrichment.fullSummary.readingTimeSeconds || 45}s read</span>
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

              <div className="briefing-overview" style={{ fontSize: 'var(--reader-font-size)', lineHeight: 1.75 }}>
                {enrichment.fullSummary.executiveOverview}
              </div>

              {isSummaryExpanded && (
                <div className="briefing-subsections">
                  {enrichment.fullSummary.contextAndBackground &&
                    enrichment.fullSummary.contextAndBackground.length > 0 && (
                      <div className="briefing-section">
                        <div className="briefing-section-title">Context & Background</div>
                        <ul className="briefing-bullet-list">
                          {enrichment.fullSummary.contextAndBackground.map((item, idx) => (
                            <li key={idx} className="briefing-bullet-item">
                              <span className="briefing-bullet-dot">✦</span>
                              <span>{item}</span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                  {enrichment.fullSummary.keyFiguresAndEntities &&
                    enrichment.fullSummary.keyFiguresAndEntities.length > 0 && (
                      <div className="briefing-section">
                        <div className="briefing-section-title">Key Figures & Named Entities</div>
                        <div className="briefing-figures-grid">
                          {enrichment.fullSummary.keyFiguresAndEntities.map((fig, idx) => (
                            <span key={idx} className="briefing-figure-chip">
                              <strong>{fig.includes(":") ? fig.split(":")[0] + ":" : "•"}</strong>
                              <span>{fig.includes(":") ? fig.substring(fig.indexOf(":") + 1).trim() : fig}</span>
                            </span>
                          ))}
                        </div>
                      </div>
                    )}

                  {enrichment.fullSummary.outlookOrNextSteps && (
                    <div className="briefing-section">
                      <div className="briefing-section-title">Outlook & Next Steps</div>
                      <div className="briefing-outlook-box">
                        {enrichment.fullSummary.outlookOrNextSteps}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          ) : article.aiSummary ? (
            /* Standalone AI Quick Read Summary */
            <div className="ai-executive-briefing-box" aria-label="AI Quick Read">
              <div className="briefing-header">
                <div className="briefing-header-left">
                  <div className="briefing-badge">
                    <Sparkles size={16} className="briefing-icon" />
                    <span>⚡ Quick Read</span>
                  </div>
                  <span className="briefing-reading-time">
                    <Clock size={12} />
                    <span>~45s read</span>
                  </span>
                </div>
              </div>
              <div className="briefing-overview" style={{ fontSize: 'var(--reader-font-size)', lineHeight: 1.75 }}>
                {article.aiSummary}
              </div>
            </div>
          ) : (
            /* Prompt to generate Quick Read on demand */
            <div className="reader-generate-quickread-banner">
              <div className="generate-banner-content">
                <div className="generate-banner-title">
                  <Sparkles size={16} className="generate-sparkle-icon" />
                  <span>Want a 45-second synthesis?</span>
                </div>
                <p className="generate-banner-desc">
                  Generate an instant AI Executive Briefing with key takeaways, named entities, and context.
                </p>
              </div>
              <button
                type="button"
                className="generate-quickread-btn"
                onClick={handleGenerateAiSummary}
                disabled={isGenerating}
              >
                {isGenerating ? (
                  <>
                    <span className="spinner-icon" />
                    <span>Synthesizing...</span>
                  </>
                ) : (
                  <>
                    <Zap size={14} />
                    <span>Generate Quick Read</span>
                  </>
                )}
              </button>
            </div>
          )}

          {/* Key Takeaways Box */}
          {enrichment?.keyTakeaways && enrichment.keyTakeaways.length > 0 && (
            <div className="key-takeaways-box" aria-label="Key Takeaways">
              <div className="takeaways-header">
                <div className="takeaways-badge">
                  <CheckCircle size={15} className="takeaways-icon" />
                  <span>Key Takeaways</span>
                </div>
                <span className="takeaways-engine-tag">
                  {enrichment.engine?.toLowerCase().includes("gemini") ? "AI Verified" : "Key Facts"}
                </span>
              </div>
              <ul className="takeaways-list">
                {enrichment.keyTakeaways.map((bullet, idx) => (
                  <li key={idx} className="takeaways-item">
                    <span className="takeaways-bullet-icon">▸</span>
                    <span className="takeaways-text">{bullet}</span>
                  </li>
                ))}
              </ul>
              {enrichment.timelineFacts && enrichment.timelineFacts.length > 0 && (
                <div className="timeline-facts-strip">
                  <span className="timeline-strip-label">Timeline:</span>
                  {enrichment.timelineFacts.slice(0, 4).map((fact, idx) => (
                    <span key={idx} className="timeline-fact-chip">
                      <strong>{fact.dateOrTime || fact.dateText || "•"}:</strong> {fact.fact}
                    </span>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Editorial Lead & Publisher Excerpt */}
          <div className="reader-editorial-snippet" style={{ whiteSpace: 'pre-line', lineHeight: 1.7, fontSize: 'var(--reader-font-size)', marginTop: 24, marginBottom: 16 }}>
            <div className="reader-snippet-heading">Publisher Lead Coverage:</div>
            <p style={{ margin: 0 }}>
              {cleanEditorialSummary(article.excerpt) || cleanEditorialSummary(article.summary) || 'Read full coverage at the original publisher.'}
            </p>
          </div>

          {/* In-Article Placement 2: Mid-Article Inline Breakout Box */}
          {!article.isSponsored && midContentSponsor && (
            <div
              className="in-article-mid-sponsor-card"
              onClick={() => {
                api.trackSponsoredClick(midContentSponsor.id);
                if (onSelectStory) onSelectStory(midContentSponsor);
              }}
              role="button"
              tabIndex={0}
            >
              <div className="mid-sponsor-header">
                <span className="mid-sponsor-badge">
                  <Megaphone size={12} />
                  <span>SPONSORED INSIGHT</span>
                </span>
                <span className="mid-sponsor-by">presented by <strong>{midContentSponsor.sponsorName}</strong></span>
              </div>
              <div className="mid-sponsor-body">
                {midContentSponsor.imageUrl && (
                  <img
                    src={midContentSponsor.imageUrl}
                    alt={midContentSponsor.title}
                    className="mid-sponsor-img"
                    onError={(e) => { e.target.style.display = 'none'; }}
                  />
                )}
                <div className="mid-sponsor-text">
                  <h4 className="mid-sponsor-title">{midContentSponsor.title}</h4>
                  <p className="mid-sponsor-snippet">{midContentSponsor.summary || midContentSponsor.excerpt}</p>
                </div>
              </div>
              <div className="mid-sponsor-actions">
                <button
                  type="button"
                  className="mid-sponsor-read-btn"
                  onClick={(e) => {
                    e.stopPropagation();
                    api.trackSponsoredClick(midContentSponsor.id);
                    if (onSelectStory) onSelectStory(midContentSponsor);
                  }}
                >
                  <span>Read Sponsored Story</span>
                  <ArrowRight size={13} />
                </button>
                {midContentSponsor.sponsorUrl && (
                  <a
                    href={midContentSponsor.sponsorUrl}
                    target="_blank"
                    rel="sponsored noopener noreferrer"
                    className="mid-sponsor-link-btn"
                    onClick={(e) => {
                      e.stopPropagation();
                      api.trackSponsoredClick(midContentSponsor.id);
                    }}
                  >
                    <span>Visit Partner</span>
                    <ExternalLink size={12} />
                  </a>
                )}
              </div>
            </div>
          )}

          {/* Actions & Attribution */}
          <div className="reader-actions-bar" style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center' }}>
            {article.isSponsored && article.sponsorUrl && (
              <a
                href={article.sponsorUrl}
                target="_blank"
                rel="sponsored noopener"
                className="primary-btn sponsored-partner-cta-btn"
                onClick={() => {
                  api.trackSponsoredClick(article.id);
                  trackOutboundClick(article, article.sponsorUrl);
                }}
              >
                <span>Visit {article.sponsorName || 'Partner'} Portal</span>
                <ExternalLink size={15} />
              </a>
            )}

            {article.url && (
              <a
                href={article.url}
                target="_blank"
                rel={article.isSponsored ? "sponsored noopener" : "noopener noreferrer"}
                className="primary-btn"
                onClick={() => trackOutboundClick(article, article.url)}
              >
                <span>Read full story on {article.source || 'Publisher'}</span>
                <ExternalLink size={15} />
              </a>
            )}

            <div className="btn-callout-wrapper">
              <button
                type="button"
                className="share-social-pill share-pill-x"
                onClick={() => shareOnX(article)}
                aria-label="Post this story to X"
              >
                <XIcon size={14} />
                <span>Post to X</span>
              </button>
              <div className="btn-callout top">
                <span className="btn-callout-title">Post to X</span>
                <span className="btn-callout-desc">Broadcast story to your X followers</span>
              </div>
            </div>

            <div className="btn-callout-wrapper">
              <button
                type="button"
                className="share-social-pill share-pill-wa"
                onClick={() => shareOnWhatsApp(article)}
                aria-label="Share this story on WhatsApp"
              >
                <WhatsAppIcon size={15} color="#25D366" />
                <span>WhatsApp</span>
              </button>
              <div className="btn-callout top">
                <span className="btn-callout-title">Share to WhatsApp</span>
                <span className="btn-callout-desc">Forward article to WhatsApp chats</span>
              </div>
            </div>

            {copied && (
              <span style={{ fontSize: '0.82rem', color: 'var(--emerald-bright)', fontWeight: 600 }}>
                ✓ Link copied to clipboard
              </span>
            )}
          </div>

          {/* Related Content & Coverage Section */}
          {relatedStories.length > 0 && (
            <section className="reader-related-section" aria-label="Related Coverage">
              <div className="reader-related-header">
                <div className="reader-related-header-left">
                  <div className="reader-related-badge">
                    <Layers size={14} className="related-badge-icon" />
                    <span>RELATED COVERAGE</span>
                  </div>
                  <h3 className="reader-related-title">
                    More Stories on This Topic
                  </h3>
                </div>
                {article.category && (
                  <span className="reader-related-category-pill">
                    {article.category}
                  </span>
                )}
              </div>

              <div className="reader-related-grid">
                {relatedStories.map((relStory) => (
                  <article
                    key={relStory.id}
                    className="reader-related-card"
                    onClick={() => handleSelectRelated(relStory)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        handleSelectRelated(relStory);
                      }
                    }}
                    aria-label={`Read related story: ${relStory.title}`}
                  >
                    <div className="related-card-media">
                      <img
                        src={getOptimizedImageUrl(relStory.imageUrl, 400)}
                        alt={relStory.title}
                        className="related-card-img"
                        loading="lazy"
                        decoding="async"
                        onError={(e) => {
                          e.target.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=400&q=80';
                        }}
                      />
                      {(relStory.aiSummary || relStory.hasAiSummary) && (
                        <span className="related-card-ai-badge">
                          ⚡ Quick read
                        </span>
                      )}
                    </div>
                    <div className="related-card-body">
                      <div className="related-card-meta">
                        <span className="related-card-source">{relStory.source || 'Publisher'}</span>
                        <span className="related-card-dot">•</span>
                        <span className="related-card-time">{formatFullDate(relStory.publishedAt) || 'Recent'}</span>
                      </div>
                      <h4 className="related-card-heading">
                        {relStory.title}
                      </h4>
                      {(relStory.summary || relStory.excerpt) && (
                        <p className="related-card-snippet">
                          {cleanEditorialSummary(relStory.summary || relStory.excerpt)}
                        </p>
                      )}
                      <div className="related-card-footer">
                        <span className="related-read-action">
                          <span>Read story</span>
                          <ArrowRight size={13} className="related-arrow-icon" />
                        </span>
                      </div>
                    </div>
                  </article>
                ))}
              </div>
            </section>
          )}

          {/* In-Article Placement 3: Sponsored Story Callout Box (Bottom Editorial Climax) */}
          {!article.isSponsored && (footerSponsor || featuredSponsoredStory) && (() => {
            const activeFoot = footerSponsor || featuredSponsoredStory;
            return (
              <div className="sponsored-perspective-callout">
                <div className="callout-header">
                  <span className="callout-badge">
                    <Megaphone size={13} />
                    PARTNER PERSPECTIVE
                  </span>
                  <span className="callout-sponsor">{activeFoot.sponsorName || 'Corporate Partner'}</span>
                </div>
                <div className="callout-content">
                  {activeFoot.imageUrl && (
                    <img
                      src={activeFoot.imageUrl}
                      alt={activeFoot.title}
                      className="callout-image"
                      onError={(e) => { e.target.style.display = 'none'; }}
                    />
                  )}
                  <div className="callout-text">
                    <h4 className="callout-title">{activeFoot.title}</h4>
                    <p className="callout-summary">{activeFoot.excerpt || activeFoot.summary}</p>
                  </div>
                </div>
                <div className="callout-actions">
                  <button
                    type="button"
                    className="callout-read-btn"
                    onClick={() => {
                      if (onSelectStory) onSelectStory(activeFoot);
                    }}
                  >
                    <span>Read Partner Story</span>
                    <ArrowRight size={14} />
                  </button>
                  {activeFoot.sponsorUrl && (
                    <a
                      href={activeFoot.sponsorUrl}
                      target="_blank"
                      rel="sponsored nofollow noopener"
                      className="callout-visit-link"
                      onClick={() => api.trackSponsoredClick(activeFoot.id)}
                    >
                      <span>Visit Official Portal</span>
                      <ExternalLink size={13} />
                    </a>
                  )}
                </div>
              </div>
            );
          })()}

          {/* Interactive Community Comments & Discussion */}
          <Suspense fallback={<div className="skeleton" style={{ minHeight: 180, borderRadius: 12, margin: '2rem 0' }} />}>
            <CommentSection
              articleId={article.id}
              articleTitle={article.title}
              currentUser={currentUser}
              onOpenAuth={onOpenAuth}
            />
          </Suspense>
        </div>
      </aside>
    </>
  );
}
