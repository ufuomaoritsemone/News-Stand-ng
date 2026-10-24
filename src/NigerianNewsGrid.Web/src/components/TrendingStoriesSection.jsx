import React, { useRef } from 'react';
import { Link } from 'react-router';
import { Flame, Layers, ChevronRight, ChevronLeft } from 'lucide-react';
import { formatTimeAgo } from '../services/timeUtils';

export default function TrendingStoriesSection({ stories = [], onSelectStory }) {
  const railRef = useRef(null);

  // Prefer developing stories from the last 36 hours, falling back to all provided stories so the rail never disappears when clusters are slightly older
  const recentStories = (stories || []).filter((s) => {
    const timestamp = s.updatedAt || s.createdAt;
    if (!timestamp) return true;
    const date = new Date(timestamp);
    if (isNaN(date.getTime())) return true;
    const ageHours = (Date.now() - date.getTime()) / (1000 * 60 * 60);
    return ageHours <= 36;
  });

  const activeStories = recentStories.length > 0 ? recentStories : (stories || []);

  if (!activeStories || activeStories.length === 0) {
    return null;
  }

  const scroll = (direction) => {
    if (railRef.current) {
      const scrollAmount = 340;
      railRef.current.scrollBy({
        left: direction === 'left' ? -scrollAmount : scrollAmount,
        behavior: 'smooth'
      });
    }
  };

  return (
    <section className="trending-stories-section app-container" aria-label="Trending Developing Stories">
      {/* Header Strip */}
      <div className="trending-section-header">
        <div className="trending-header-left">
          <div className="trending-badge-live">
            <span className="live-pulse-dot" />
            <Flame size={14} className="trending-flame-icon" />
            <span>DEVELOPING STORIES</span>
          </div>
          <h2 className="trending-section-title">
            What Nigeria Is Reading Right Now
          </h2>
          <span className="trending-subtitle">
            Cross-publisher verification & multi-source coverage
          </span>
        </div>
        <div className="trending-header-right">
          <div className="trending-rail-controls">
            <button
              type="button"
              className="trending-rail-nav-btn"
              onClick={() => scroll('left')}
              aria-label="Scroll left"
            >
              <ChevronLeft size={16} />
            </button>
            <button
              type="button"
              className="trending-rail-nav-btn"
              onClick={() => scroll('right')}
              aria-label="Scroll right"
            >
              <ChevronRight size={16} />
            </button>
          </div>
          <span className="trending-verified-pill">
            <Layers size={13} style={{ marginRight: 4 }} />
            Multi-Source Aggregation
          </span>
        </div>
      </div>

      {/* Horizontal Rail of Compact Cards */}
      <div className="trending-rail-container">
        <div className="trending-rail-track" ref={railRef}>
          {activeStories.map((story, idx) => {
            const handleStoryClick = (e) => {
              if (story.leadArticle && typeof onSelectStory === 'function') {
                e.preventDefault();
                onSelectStory(story.leadArticle);
              }
            };

            return (
              <article
                key={story.id || story.slug}
                className={`trending-rail-card ${idx === 0 ? 'trending-rail-card-top' : ''}`}
                onClick={story.leadArticle && typeof onSelectStory === 'function' ? handleStoryClick : undefined}
              >
                <div className="trending-rail-card-header">
                  <div className="trending-rail-badges">
                    <span className={`trending-rail-rank ${idx === 0 ? 'rank-gold' : ''}`}>
                      {idx === 0 && <Flame size={12} style={{ marginRight: 2 }} />}
                      #{idx + 1}
                    </span>
                    <span className="trending-rail-sources-badge">
                      <Layers size={11} style={{ marginRight: 3 }} />
                      {story.sourceCount || 2} Sources
                    </span>
                  </div>
                  <div className="trending-rail-meta-right">
                    <span className="trending-rail-category">{story.category || 'News'}</span>
                    <span className="meta-bullet">•</span>
                    <span className="trending-rail-time">{formatTimeAgo(story.updatedAt)}</span>
                  </div>
                </div>

                <h3 className="trending-rail-title">
                  <Link to={`/story/${story.slug}`} onClick={handleStoryClick}>
                    {story.title}
                  </Link>
                </h3>

                <div className="trending-rail-card-footer">
                  {story.sources && story.sources.length > 0 && (
                    <span className="trending-rail-sources-text">
                      {story.sources.slice(0, 3).join(', ')}
                      {story.sources.length > 3 && ` +${story.sources.length - 3}`}
                    </span>
                  )}
                  <Link
                    to={`/story/${story.slug}`}
                    onClick={handleStoryClick}
                    className="trending-rail-arrow"
                    aria-label={`Read story: ${story.title}`}
                  >
                    <ChevronRight size={15} />
                  </Link>
                </div>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}
