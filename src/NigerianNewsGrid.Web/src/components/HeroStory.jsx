import React from 'react';
import { Clock, Bookmark, Sparkles, MessageSquare, Newspaper } from 'lucide-react';
import { XIcon, WhatsAppIcon } from './SocialIcons';
import { shareOnX, shareOnWhatsApp } from '../services/share';
import { cleanEditorialSummary } from '../services/timeUtils';
import { getOptimizedImageUrl } from '../services/api';

export default function HeroStory({ article, onSelectStory, isBookmarked, onToggleBookmark }) {
  if (!article) {
    return (
      <section className="hero-section app-container" aria-label="Top story loading">
        <div className="hero-card skeleton" style={{ minHeight: 400 }} />
      </section>
    );
  }

  // Calculate estimated reading time
  const wordCount = (article.excerpt || article.summary || article.title || '').split(/\s+/).length;
  const readMinutes = Math.max(1, Math.round(wordCount / 180));

  const formattedDate = article.publishedAt
    ? new Date(article.publishedAt).toLocaleDateString('en-NG', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      })
    : 'Recent';

  return (
    <section className="hero-section app-container" aria-label="Story of the day">
      <article
        className="hero-card"
        onClick={() => onSelectStory(article)}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === 'Enter' && onSelectStory(article)}
        aria-label={`Featured Story: ${article.title}`}
      >
        {/* Background Media */}
        <img
          src={getOptimizedImageUrl(article.imageUrl, 1200)}
          srcSet={`${getOptimizedImageUrl(article.imageUrl, 640)} 640w, ${getOptimizedImageUrl(article.imageUrl, 960)} 960w, ${getOptimizedImageUrl(article.imageUrl, 1200)} 1200w`}
          sizes="(max-width: 768px) 100vw, 1200px"
          alt={article.title}
          className="hero-image"
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

        <div className="hero-backdrop" />

        {/* Content Details */}
        <div className="hero-content">
          <div className="hero-badge-row">
            <span className="hero-featured-tag">
              <Sparkles size={12} style={{ marginRight: 4, verticalAlign: 'middle' }} />
              Story of the Day
            </span>
            <span className="category-tag">{article.category || 'National'}</span>
            {article.isSponsored && (
              <span className="sponsored-badge">Sponsored · {article.sponsorName || 'Partner'}</span>
            )}
          </div>

          <h1 className="hero-title">{article.title}</h1>

          {(article.excerpt || article.summary) && (
            <p className="hero-summary">{cleanEditorialSummary(article.excerpt || article.summary)}</p>
          )}

          <div className="hero-meta">
            <span className="hero-source">Source: {article.source || 'News Stand NG'}</span>
            <span>·</span>
            <span>{formattedDate}</span>
            <span>·</span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
              <Clock size={13} />
              {readMinutes} min read
            </span>
          </div>

          {/* Action Buttons: Row 1 (Quick read, Discuss, Full read) | Row 2 (Save, Post, WhatsApp) */}
          <div className="buletin-card-footer hero-card-footer" style={{ marginTop: 16 }}>
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
                {article.url ? (
                  <a
                    href={article.url}
                    target="_blank"
                    rel={article.isSponsored ? "sponsored noopener" : "noopener noreferrer"}
                    className="card-action-btn card-action-fullread"
                    onClick={(e) => {
                      e.stopPropagation();
                    }}
                    aria-label={`Full read: Open story on ${article.source || 'Publisher'}`}
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
                  <span className="btn-callout-title">Source: {article.source || 'Publisher'}</span>
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
                  <span className="btn-callout-desc">Post this top story to X (Twitter)</span>
                </div>
              </div>

              {/* WhatsApp */}
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
                  <span className="btn-callout-title">Share to WhatsApp</span>
                  <span className="btn-callout-desc">Forward headline and preview to WhatsApp</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </article>
    </section>
  );
}
