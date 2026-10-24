import React, { useState, useEffect } from 'react';
import { Link } from 'react-router';
import { Bookmark, Flame, Megaphone, ExternalLink, MessageSquare, Share2, Check, Sparkles, Newspaper } from 'lucide-react';
import { XIcon, WhatsAppIcon } from './SocialIcons';
import { shareOnX, shareOnWhatsApp, shareGeneric } from '../services/share';
import { getRecencyTag, isHotStory } from '../services/articleMetadata';
import { formatTimeAgo, cleanEditorialSummary } from '../services/timeUtils';
import { api, getOptimizedImageUrl, DEFAULT_EDITORIAL_IMAGE } from '../services/api';

export default function ArticleCard({
  article,
  onSelectStory,
  isBookmarked,
  onToggleBookmark,
}) {
  const [shareCopied, setShareCopied] = useState(false);
  const isSponsored = Boolean(article?.isSponsored);
  const articleId = article?.id;

  // Track sponsored impression when card mounts
  useEffect(() => {
    if (isSponsored && articleId) {
      api.trackSponsoredImpression(articleId);
    }
  }, [isSponsored, articleId]);

  if (!article) return null;

  const recency = getRecencyTag(article.publishedAt);
  const hotStory = isHotStory(article);

  const handleCardClick = () => {
    if (isSponsored && article.id) {
      api.trackSponsoredClick(article.id);
    }
    if (onSelectStory) {
      onSelectStory(article);
    }
  };

  const handleGenericShare = async (e) => {
    e.stopPropagation();
    const res = await shareGeneric(article);
    if (res?.method === 'clipboard') {
      setShareCopied(true);
      setTimeout(() => setShareCopied(false), 2200);
    }
  };

  // Estimate reading time & Quick Read status
  const hasAi = Boolean(article.hasAiSummary || article.aiSummary);
  const wordCount = (article.content || article.summary || article.title || '').split(/\s+/).length;
  const readMinutes = Math.round(wordCount / 180);
  const readTimeLabel = hasAi ? '⚡ Quick read' : (wordCount < 50 ? 'Quick read' : `${Math.max(1, readMinutes)} min read`);
  const displaySummary = article.aiSummary || article.summary;

  const articlePermalink = article.slug ? `/news/${article.slug}` : `/news/${article.id}`;

  return (
    <article
      className={`buletin-card ${isSponsored ? 'sponsored-card' : ''}`}
      onClick={handleCardClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && handleCardClick()}
      aria-label={article.title}
    >
      {/* 1. Media Thumbnail */}
      <div className="buletin-card-media">
        <Link
          to={articlePermalink}
          onClick={(e) => {
            if (onSelectStory) {
              e.preventDefault();
              onSelectStory(article);
            }
          }}
          tabIndex={-1}
          aria-hidden="true"
        >
          <img
            src={getOptimizedImageUrl(article.imageUrl, 400)}
            srcSet={`${getOptimizedImageUrl(article.imageUrl, 320)} 320w, ${getOptimizedImageUrl(article.imageUrl, 480)} 480w, ${getOptimizedImageUrl(article.imageUrl, 640)} 640w`}
            sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 380px"
            alt={article.title}
            className="buletin-card-img"
            loading="lazy"
            decoding="async"
            width="400"
            height="225"
            onError={(e) => {
              e.target.onerror = null;
              e.target.srcset = '';
              e.target.src = DEFAULT_EDITORIAL_IMAGE;
            }}
          />
        </Link>

        {/* Sponsored Brand Badge (High-Contrast Amber/Gold) */}
        {isSponsored && (
          <div
            className="sponsored-badge-pill"
            title={`Sponsored editorial presented by ${article.sponsorName || 'Partner Studio'}`}
          >
            <Megaphone size={11} className="sponsored-badge-icon" />
            <span>Sponsored · {article.sponsorName || 'Partner'}</span>
          </div>
        )}

        {/* Hot story fire badge (> 2 comments) */}
        {!isSponsored && hotStory && (
          <div
            className="story-fire-badge card-fire-badge"
            title={`${article.commentCount} comments on this story`}
          >
            <Flame size={12} className="fire-pulse-icon" />
            <span>{article.commentCount}</span>
          </div>
        )}

        {/* Category Descriptor in Bottom-Left Corner of Image */}
        <span className={`card-media-category-badge ${isSponsored ? 'badge-sponsored' : ''}`}>
          {isSponsored ? 'Partner' : (article.category || 'General')}
        </span>
      </div>

      {/* 2. Card Content */}
      <div className="buletin-card-body">
        {/* Source Row with Recency Tag */}
        <div className="buletin-source-row">
          <div className="buletin-source-badge">
            {article.sourceLogo ? (
              <img src={article.sourceLogo} alt="" className="source-mini-img" />
            ) : (
              <span className="source-mini-fallback">
                {(article.sponsorName || article.source || 'N').charAt(0).toUpperCase()}
              </span>
            )}
            <span className="buletin-source-name">
              {isSponsored ? (article.sponsorName || 'Partner Feature') : `Source: ${article.source || 'News Stand NG'}`}
            </span>
          </div>

          {isSponsored ? (
            <span className="sponsored-chip">Sponsored</span>
          ) : (
            recency && (
              <span className={`recency-pill mini ${recency.badgeClass}`}>
                {recency.label}
              </span>
            )
          )}

          <span className="meta-dot">•</span>
          <span className="buletin-card-time">{formatTimeAgo(article.publishedAt)}</span>
        </div>

        {/* Serif Card Title */}
        <h3 className="buletin-card-title">
          <Link
            to={articlePermalink}
            onClick={(e) => {
              if (onSelectStory) {
                e.preventDefault();
                onSelectStory(article);
              }
            }}
            className="buletin-card-title-link"
          >
            {article.title}
          </Link>
        </h3>

        {/* Sans-serif Summary */}
        {displaySummary && (
          <p className="buletin-card-summary">
            {article.aiSummary && <span className="card-summary-ai-tag">Quick Read: </span>}
            {cleanEditorialSummary(displaySummary)}
          </p>
        )}

        {/* Card Footer: Row 1 (Quick read, Discuss, Full read) | Row 2 (Save, Post, WhatsApp) */}
        <div className="buletin-card-footer">
          {/* Row 1: Quick read, Discuss, Full read */}
          <div className="card-footer-row card-footer-row-primary">
            {/* Quick read button */}
            <div className="btn-callout-wrapper action-flex-item">
              <button
                type="button"
                className="card-action-btn card-action-quickread"
                onClick={(e) => {
                  e.stopPropagation();
                  if (onSelectStory) onSelectStory(article);
                }}
                aria-label="Open AI Quick Read"
              >
                <Sparkles size={13} className="btn-action-icon" />
                <span>Quick read</span>
              </button>
              <div className="btn-callout top">
                <span className="btn-callout-title">⚡ AI Quick Read</span>
                <span className="btn-callout-desc">Open executive briefing & key takeaways</span>
              </div>
            </div>

            {/* Discuss button */}
            <div className="btn-callout-wrapper action-flex-item">
              <button
                type="button"
                className={`card-action-btn card-action-discuss ${article.commentCount > 0 ? 'has-comments' : ''}`}
                onClick={(e) => {
                  e.stopPropagation();
                  if (onSelectStory) {
                    onSelectStory(article, { scrollToComments: true });
                  } else if (typeof window !== 'undefined') {
                    window.location.href = `${articlePermalink}#comments`;
                  }
                }}
                aria-label={
                  article.commentCount > 0
                    ? `${article.commentCount} Discuss: Reader comments`
                    : 'Discuss: Join reader debate'
                }
              >
                <MessageSquare size={13} className="btn-action-icon" />
                <span>{article.commentCount > 0 ? `${article.commentCount} Discuss` : 'Discuss'}</span>
              </button>
              <div className="btn-callout top">
                <span className="btn-callout-title">Reader Community</span>
                <span className="btn-callout-desc">
                  {article.commentCount > 0
                    ? `${article.commentCount} reader ${article.commentCount === 1 ? 'comment' : 'comments'} · Join debate`
                    : 'Join or start reader discussion'}
                </span>
              </div>
            </div>

            {/* Full read button with Newspaper icon */}
            <div className="btn-callout-wrapper action-flex-item">
              {(article.url || article.sponsorUrl) ? (
                <a
                  href={isSponsored ? (article.sponsorUrl || article.url) : article.url}
                  target="_blank"
                  rel={isSponsored ? "sponsored noopener" : "noopener noreferrer"}
                  className={`card-action-btn card-action-fullread ${isSponsored ? 'card-sponsored-cta' : ''}`}
                  onClick={(e) => {
                    e.stopPropagation();
                    if (isSponsored && article.id) {
                      api.trackSponsoredClick(article.id);
                    }
                  }}
                  aria-label={`Full read: Open story on ${isSponsored ? (article.sponsorName || 'Partner') : (article.source || 'Publisher')}`}
                >
                  <Newspaper size={13} className="btn-action-icon" />
                  <span>Full read</span>
                </a>
              ) : (
                <button
                  type="button"
                  className="card-action-btn card-action-fullread"
                  onClick={(e) => {
                    e.stopPropagation();
                    if (onSelectStory) onSelectStory(article);
                  }}
                  aria-label="Full read: Open full story"
                >
                  <Newspaper size={13} className="btn-action-icon" />
                  <span>Full read</span>
                </button>
              )}
              <div className="btn-callout top">
                <span className="btn-callout-title">Source: {isSponsored ? (article.sponsorName || 'Official Partner') : (article.source || 'Publisher')}</span>
                <span className="btn-callout-desc">Open unabridged reporting on original publisher website</span>
              </div>
            </div>
          </div>

          {/* Row 2: Save, Post, WhatsApp */}
          <div className="card-footer-row card-footer-row-secondary">
            {/* Save / Bookmark Button */}
            <div className="btn-callout-wrapper action-flex-item">
              <button
                type="button"
                className={`card-action-btn card-action-save ${isBookmarked ? 'bookmarked' : ''}`}
                onClick={(e) => {
                  e.stopPropagation();
                  if (onToggleBookmark) onToggleBookmark(article);
                }}
                aria-label={isBookmarked ? 'Remove bookmark' : 'Save story'}
              >
                <Bookmark
                  size={12}
                  fill={isBookmarked ? 'var(--brand-green)' : 'none'}
                  color={isBookmarked ? 'var(--brand-green)' : 'currentColor'}
                />
                <span>{isBookmarked ? 'Saved' : 'Save'}</span>
              </button>
              <div className="btn-callout top">
                <span className="btn-callout-title">{isBookmarked ? 'Saved Story' : 'Save Story'}</span>
                <span className="btn-callout-desc">
                  {isBookmarked ? 'Click to remove from saved' : 'Bookmark to read offline later'}
                </span>
              </div>
            </div>

            {/* Post on X */}
            <div className="btn-callout-wrapper action-flex-item">
              <button
                type="button"
                className="card-action-btn card-action-x"
                onClick={(e) => {
                  e.stopPropagation();
                  shareOnX(article);
                }}
                aria-label="Post to X (Twitter)"
              >
                <XIcon size={12} />
                <span>Post</span>
              </button>
              <div className="btn-callout top">
                <span className="btn-callout-title">Share on X</span>
                <span className="btn-callout-desc">Post headline & preview link to your X timeline</span>
              </div>
            </div>

            {/* Share on WhatsApp */}
            <div className="btn-callout-wrapper action-flex-item">
              <button
                type="button"
                className="card-action-btn card-action-wa"
                onClick={(e) => {
                  e.stopPropagation();
                  shareOnWhatsApp(article);
                }}
                aria-label="Share on WhatsApp"
              >
                <WhatsAppIcon size={13} color="#25D366" />
                <span>WhatsApp</span>
              </button>
              <div className="btn-callout top">
                <span className="btn-callout-title">Share on WhatsApp</span>
                <span className="btn-callout-desc">Forward headline and preview link to WhatsApp chats</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </article>
  );
}
