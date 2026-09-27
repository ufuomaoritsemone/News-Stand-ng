import React, { useState, useEffect, useRef, useCallback } from 'react';
import { ChevronLeft, ChevronRight, Bookmark, Clock, Pause, Play } from 'lucide-react';

export default function HeroCarousel({
  stories = [],
  onSelectStory,
  isBookmarked,
  onToggleBookmark,
  autoPlayInterval = 5000,
}) {
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isPaused, setIsPaused] = useState(false);
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

  // Helper for human-readable relative time
  const formatTimeAgo = (dateStr) => {
    if (!dateStr) return 'Recently';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins} minutes ago`;
    if (diffHours < 24) return `${diffHours} ${diffHours === 1 ? 'hour' : 'hours'} ago`;
    if (diffDays < 7) return `${diffDays} ${diffDays === 1 ? 'day' : 'days'} ago`;
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  };

  // Estimated reading time
  const wordCount = (current.content || current.summary || current.title || '').split(/\s+/).length;
  const readTime = Math.max(3, Math.round(wordCount / 180));

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
        <article
          className="hero-split-card"
          onClick={() => onSelectStory && onSelectStory(current)}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => e.key === 'Enter' && onSelectStory && onSelectStory(current)}
          aria-label={`Featured: ${current.title}`}
        >
          {/* Left Column: Media Thumbnail */}
          <div className="hero-media-col">
            <div className="hero-image-frame">
              <img
                key={current.id || currentIndex}
                src={current.imageUrl || '/logo.png'}
                alt={current.title}
                className="hero-media-img"
                loading="eager"
                onError={(e) => {
                  e.target.src = 'https://images.unsplash.com/photo-1536440136628-849c177e76a1?auto=format&fit=crop&w=1000&q=80';
                }}
              />
            </div>
          </div>

          {/* Right Column: Editorial Metadata & Copy */}
          <div className="hero-info-col">
            {/* Publisher Row: Icon / Name / Timestamp */}
            <div className="hero-source-row">
              <div className="hero-source-badge">
                {current.sourceLogo ? (
                  <img src={current.sourceLogo} alt="" className="source-avatar-img" />
                ) : (
                  <span className="source-avatar-fallback">
                    {(current.source || 'N').charAt(0).toUpperCase()}
                  </span>
                )}
                <span className="hero-source-name">{current.source || 'Buletin Editorial'}</span>
              </div>
              <span className="meta-dot">•</span>
              <span className="hero-timestamp">{formatTimeAgo(current.publishedAt)}</span>
            </div>

            {/* Serif Headline */}
            <h2 className="hero-title-serif">{current.title}</h2>

            {/* Sans-Serif Body Excerpt */}
            <p className="hero-excerpt-sans">
              {current.summary ||
                current.content ||
                "There's been no official announcement regarding streaming release. However, this coverage explores the full background, critical reception, and latest development updates."}
            </p>

            {/* Category Tag & Reading Time */}
            <div className="hero-footer-meta">
              <span className="hero-category-label">
                {current.category || 'Stories'}
              </span>
              <span className="meta-dot">•</span>
              <span className="hero-read-time">
                <Clock size={13} style={{ marginRight: 4, verticalAlign: 'text-bottom' }} />
                {readTime} min read
              </span>

              {/* Bookmark Action */}
              <button
                type="button"
                className={`hero-bookmark-btn ${bookmarked ? 'bookmarked' : ''}`}
                onClick={(e) => {
                  e.stopPropagation();
                  if (onToggleBookmark) onToggleBookmark(current);
                }}
                title={bookmarked ? 'Remove bookmark' : 'Bookmark story'}
                aria-label="Bookmark story"
              >
                <Bookmark
                  size={18}
                  fill={bookmarked ? 'var(--brand-red)' : 'none'}
                  color={bookmarked ? 'var(--brand-red)' : 'currentColor'}
                />
              </button>
            </div>
          </div>
        </article>

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
