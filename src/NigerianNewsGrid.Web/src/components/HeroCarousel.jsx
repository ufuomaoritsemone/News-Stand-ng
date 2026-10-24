import React, { useState, useEffect, useRef, useCallback } from 'react';
import { ChevronLeft, ChevronRight, Bookmark, Clock, Pause, Play, Flame, Share2, Check, Sparkles, MessageSquare, Newspaper } from 'lucide-react';
import { XIcon, WhatsAppIcon } from './SocialIcons';
import { shareOnX, shareOnWhatsApp, shareGeneric } from '../services/share';
import { getRecencyTag, isHotStory } from '../services/articleMetadata';
import { formatTimeAgo, cleanEditorialSummary } from '../services/timeUtils';
import { getOptimizedImageUrl, DEFAULT_EDITORIAL_IMAGE } from '../services/api';

export default function HeroCarousel({
  stories = [],
  onSelectStory,
  isBookmarked,
  onToggleBookmark,
  autoPlayInterval = 5000,
}) {
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isPaused, setIsPaused] = useState(false);
  const [heroShareCopied, setHeroShareCopied] = useState(false);
  const timerRef = useRef(null);

  const total = stories.length;

  const nextSlide = useCallback(() => {
    if (total > 1) {
      setCurrentIndex((prev) => (prev + 1) % total);
    }
  }, [total]);

  const prevSlide = useCallback(() => {
    if (total > 1) {
      setCurrentIndex((prev) => (prev - 1 + total) % total);
    }
  }, [total]);

  // Self-scrolling autoplay timer
  useEffect(() => {
    if (total <= 1 || isPaused) {
      if (timerRef.current) clearInterval(timerRef.current);
      return;
    }

    timerRef.current = setInterval(() => {
      nextSlide();
    }, autoPlayInterval);

    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
    };
  }, [total, isPaused, nextSlide, autoPlayInterval]);

  // Keyboard navigation for accessibility
  const handleKeyDown = (e) => {
    if (e.key === 'ArrowRight') {
      nextSlide();
    } else if (e.key === 'ArrowLeft') {
      prevSlide();
    }
  };

  if (!stories || stories.length === 0) {
    return (
      <section className="hero-carousel-section app-container" aria-label="Featured Stories Loading">
        <div className="hero-split-card skeleton" style={{ minHeight: 460 }} />
      </section>
    );
  }

  const current = stories[currentIndex] || stories[0];
  const recency = getRecencyTag(current.publishedAt);
  const hotStory = isHotStory(current);

  // Estimated reading time & Quick Read status
  const hasAi = Boolean(current.hasAiSummary || current.aiSummary);
  const wordCount = (current.content || current.summary || current.title || '').split(/\s+/).length;
  const readMinutes = Math.round(wordCount / 180);
  const readTimeLabel = hasAi ? '⚡ Quick read' : (wordCount < 50 ? 'Quick read' : `${Math.max(1, readMinutes)} min read`);
  const displaySummary = current.aiSummary || current.summary;

  const bookmarked = isBookmarked ? isBookmarked(current.id) : false;

  return (
    <section
      className="hero-carousel-section app-container"
      aria-label="Featured latest stories carousel"
      onMouseEnter={() => setIsPaused(true)}
      onMouseLeave={() => setIsPaused(false)}
      onFocus={() => setIsPaused(true)}
      onBlur={() => setIsPaused(false)}
      onKeyDown={handleKeyDown}
      tabIndex={0}
    >
      <div className="hero-carousel-wrapper">
        <div
          className="hero-split-card"
          onClick={() => onSelectStory && onSelectStory(current)}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && onSelectStory && onSelectStory(current)}
          aria-label={`Featured: ${current.title}`}
        >
          {/* Left Column: Media Thumbnail */}
          <div className="hero-media-col">
            <div className="hero-image-frame">
              <img
                key={current.id || currentIndex}
                src={getOptimizedImageUrl(current.imageUrl, 800)}
                srcSet={`${getOptimizedImageUrl(current.imageUrl, 640)} 640w, ${getOptimizedImageUrl(current.imageUrl, 800)} 800w, ${getOptimizedImageUrl(current.imageUrl, 1200)} 1200w`}
                sizes="(max-width: 768px) 100vw, 50vw"
                alt={current.title}
                className="hero-media-img"
                loading="eager"
                fetchPriority="high"
                width="800"
                height="450"
                decoding="async"
                onError={(e) => {
                  e.target.onerror = null;
                  e.target.srcset = '';
                  e.target.src = DEFAULT_EDITORIAL_IMAGE;
                }}
              />
              {/* Hot Story Badge on Hero Image */}
              {hotStory && (
                <div className="story-fire-badge hero-img-fire-badge" title={`${current.commentCount} comments on this story`}>
                  <Flame size={13} className="fire-pulse-icon" />
                  <span>{current.commentCount} comments</span>
                </div>
              )}
            </div>
          </div>

          {/* Right Column: Editorial Metadata & Copy */}
          <div className="hero-info-col">
            {/* Publisher Row: Icon / Name / Recency / Timestamp */}
            <div className="hero-source-row">
              <div className="hero-source-badge">
                {current.sourceLogo ? (
                  <img src={current.sourceLogo} alt="" className="source-avatar-img" />
                ) : (
                  <span className="source-avatar-fallback">
                    {(current.source || 'N').charAt(0).toUpperCase()}
                  </span>
                )}
                <span className="hero-source-name">Source: {current.source || 'News Stand NG'}</span>
              </div>

              {/* Sponsored Badge */}
              {current.isSponsored && (
                <span
                  className="badge-sponsored"
                  style={{
                    fontSize: '0.72rem',
                    fontWeight: 700,
                    textTransform: 'uppercase',
                    letterSpacing: '0.05em',
                    padding: '2px 8px',
                    borderRadius: '4px',
                    background: 'rgba(217, 119, 6, 0.15)',
                    color: '#b45309',
                    border: '1px solid rgba(217, 119, 6, 0.3)'
                  }}
                >
                  Sponsored
                </span>
              )}

              {/* Recency Tag */}
              {recency && (
                <span className={`recency-pill ${recency.badgeClass}`}>
                  {recency.label}
                </span>
              )}

              <span className="meta-dot">•</span>
              <span className="hero-timestamp">{formatTimeAgo(current.publishedAt)}</span>
            </div>

            {/* Serif Headline */}
            <h2 className="hero-title-serif">{current.title}</h2>

            {/* Sans-Serif Body Excerpt (Never full content) */}
            <p className="hero-excerpt-sans">
              {current.aiSummary && <span className="card-summary-ai-tag">Quick Read: </span>}
              {cleanEditorialSummary(displaySummary) ||
                cleanEditorialSummary(current.excerpt) ||
                "Latest coverage and updates across Nigeria's premier editorial and investigative desks."}
            </p>

            {/* Category Tag & Reading Time */}
            <div className="hero-category-meta-strip">
              <span className="hero-category-label">
                {current.category || 'National'}
              </span>
              <span className="meta-dot">•</span>
              <span className="hero-read-time">
                <Clock size={13} style={{ marginRight: 4, verticalAlign: 'text-bottom' }} />
                {readTimeLabel}
              </span>
            </div>

            {/* Hero Action Buttons: Row 1 (Quick read, Discuss, Full read) | Row 2 (Save, Post, WhatsApp) */}
            <div className="buletin-card-footer hero-card-footer">
              {/* Row 1: Quick read, Discuss, Full read */}
              <div className="card-footer-row card-footer-row-primary">
                {/* Quick read button */}
                <div className="btn-callout-wrapper action-flex-item">
                  <button
                    type="button"
                    className="card-action-btn card-action-quickread"
                    onClick={(e) => {
                      e.stopPropagation();
                      if (onSelectStory) onSelectStory(current);
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
                    className={`card-action-btn card-action-discuss ${current.commentCount > 0 ? 'has-comments' : ''}`}
                    onClick={(e) => {
                      e.stopPropagation();
                      if (onSelectStory) {
                        onSelectStory(current, { scrollToComments: true });
                      }
                    }}
                    aria-label={
                      current.commentCount > 0
                        ? `${current.commentCount} Discuss: Reader comments`
                        : 'Discuss: Join reader debate'
                    }
                  >
                    <MessageSquare size={13} className="btn-action-icon" />
                    <span>{current.commentCount > 0 ? `${current.commentCount} Discuss` : 'Discuss'}</span>
                  </button>
                  <div className="btn-callout top">
                    <span className="btn-callout-title">Reader Community</span>
                    <span className="btn-callout-desc">
                      {current.commentCount > 0
                        ? `${current.commentCount} reader ${current.commentCount === 1 ? 'comment' : 'comments'} · Join debate`
                        : 'Join or start reader discussion'}
                    </span>
                  </div>
                </div>

                {/* Full read button with Newspaper icon */}
                <div className="btn-callout-wrapper action-flex-item">
                  {current.url ? (
                    <a
                      href={current.url}
                      target="_blank"
                      rel={current.isSponsored ? "sponsored noopener" : "noopener noreferrer"}
                      className="card-action-btn card-action-fullread"
                      onClick={(e) => {
                        e.stopPropagation();
                      }}
                      aria-label={`Full read: Open story on ${current.source || 'Publisher'}`}
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
                        if (onSelectStory) onSelectStory(current);
                      }}
                      aria-label="Full read: Open full story"
                    >
                      <Newspaper size={13} className="btn-action-icon" />
                      <span>Full read</span>
                    </button>
                  )}
                  <div className="btn-callout top">
                    <span className="btn-callout-title">Source: {current.source || 'Publisher'}</span>
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
                    className={`card-action-btn card-action-save ${bookmarked ? 'bookmarked' : ''}`}
                    onClick={(e) => {
                      e.stopPropagation();
                      if (onToggleBookmark) onToggleBookmark(current);
                    }}
                    aria-label={bookmarked ? 'Remove bookmark' : 'Save story'}
                  >
                    <Bookmark
                      size={12}
                      fill={bookmarked ? 'var(--brand-green)' : 'none'}
                      color={bookmarked ? 'var(--brand-green)' : 'currentColor'}
                    />
                    <span>{bookmarked ? 'Saved' : 'Save'}</span>
                  </button>
                  <div className="btn-callout top">
                    <span className="btn-callout-title">{bookmarked ? 'Saved Story' : 'Save Story'}</span>
                    <span className="btn-callout-desc">
                      {bookmarked ? 'Click to remove from saved' : 'Bookmark to read offline later'}
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
                      shareOnX(current);
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
                      shareOnWhatsApp(current);
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
        </div>

        {/* Carousel Navigation Controls (Arrows & Indicators) */}
        {total > 1 && (
          <div className="hero-carousel-controls" onClick={(e) => e.stopPropagation()}>
            <div className="carousel-nav-arrows">
              <button
                type="button"
                className="carousel-arrow-btn"
                onClick={prevSlide}
                aria-label="Previous slide"
                title="Previous slide"
              >
                <ChevronLeft size={20} />
              </button>

              <button
                type="button"
                className="carousel-arrow-btn"
                onClick={nextSlide}
                aria-label="Next slide"
                title="Next slide"
              >
                <ChevronRight size={20} />
              </button>

              {/* Autoplay Pause / Play Toggle */}
              <button
                type="button"
                className="carousel-pause-btn"
                onClick={() => setIsPaused((prev) => !prev)}
                aria-label={isPaused ? 'Resume auto-scroll' : 'Pause auto-scroll'}
                title={isPaused ? 'Resume auto-scroll' : 'Pause auto-scroll'}
              >
                {isPaused ? <Play size={14} /> : <Pause size={14} />}
              </button>
            </div>

            {/* Slide Indicators */}
            <div className="carousel-indicators" role="tablist">
              {stories.map((item, idx) => (
                <button
                  key={item.id || idx}
                  type="button"
                  role="tab"
                  aria-selected={idx === currentIndex}
                  className={`indicator-dot ${idx === currentIndex ? 'active' : ''}`}
                  onClick={() => setCurrentIndex(idx)}
                  aria-label={`Go to slide ${idx + 1}: ${item.title}`}
                />
              ))}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
