import React from 'react';
import { Bookmark } from 'lucide-react';

export default function ArticleCard({
  article,
  onSelectStory,
  isBookmarked,
  onToggleBookmark,
}) {
  if (!article) return null;

  // Format relative time or clean date
  const getDisplayTime = (dateStr) => {
    if (!dateStr) return 'Recent';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 60 && diffMins >= 0) return `${Math.max(1, diffMins)} hours ago`;
    if (diffHours < 24 && diffHours >= 0) return `${diffHours} hours ago`;
    if (diffDays < 7 && diffDays > 0) return `${diffDays} days ago`;
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  };

  // Estimate reading time
  const wordCount = (article.content || article.summary || article.title || '').split(/\s+/).length;
  const readTime = Math.max(3, Math.round(wordCount / 180));

  return (
    <article
      className="buletin-card"
      onClick={() => onSelectStory && onSelectStory(article)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onSelectStory && onSelectStory(article)}
      aria-label={article.title}
    >
      {/* 1. Media Thumbnail */}
      <div className="buletin-card-media">
        <img
          src={article.imageUrl || '/logo.png'}
          alt={article.title}
          className="buletin-card-img"
          loading="lazy"
          onError={(e) => {
            e.target.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=600&q=80';
          }}
        />
        <button
          type="button"
          className={`buletin-bookmark-btn ${isBookmarked ? 'bookmarked' : ''}`}
          onClick={(e) => {
            e.stopPropagation();
            if (onToggleBookmark) onToggleBookmark(article);
          }}
          title={isBookmarked ? 'Remove bookmark' : 'Save article'}
          aria-label="Bookmark article"
        >
          <Bookmark
            size={14}
            fill={isBookmarked ? 'var(--brand-red)' : 'none'}
            color={isBookmarked ? 'var(--brand-red)' : 'currentColor'}
          />
        </button>
      </div>

      {/* 2. Card Content */}
      <div className="buletin-card-body">
        {/* Source Row */}
        <div className="buletin-source-row">
          <div className="buletin-source-badge">
            {article.sourceLogo ? (
              <img src={article.sourceLogo} alt="" className="source-mini-img" />
            ) : (
              <span className="source-mini-fallback">
                {(article.source || 'N').charAt(0).toUpperCase()}
              </span>
            )}
            <span className="buletin-source-name">{article.source || 'Buletin'}</span>
          </div>
          <span className="meta-dot">•</span>
          <span className="buletin-card-time">{getDisplayTime(article.publishedAt)}</span>
        </div>

        {/* Serif Card Title */}
        <h3 className="buletin-card-title">{article.title}</h3>

        {/* Sans-serif Summary */}
        {article.summary && (
          <p className="buletin-card-summary">{article.summary}</p>
        )}

        {/* Card Footer: Category (Red) + Read Time */}
        <div className="buletin-card-footer">
          <span className="buletin-card-category">{article.category || 'General'}</span>
          <span className="meta-dot">•</span>
          <span className="buletin-card-readtime">{readTime} min read</span>
        </div>
      </div>
    </article>
  );
}
