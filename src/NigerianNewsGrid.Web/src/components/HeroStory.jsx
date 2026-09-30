import React from 'react';
import { Clock, Bookmark, Sparkles } from 'lucide-react';

export default function HeroStory({ article, onSelectStory, isBookmarked, onToggleBookmark }) {
  if (!article) {
    return (
      <section className="hero-section app-container" aria-label="Top story loading">
        <div className="hero-card skeleton" style={{ minHeight: 400 }} />
      </section>
    );
  }

  // Calculate estimated reading time
  const wordCount = (article.content || article.summary || article.title || '').split(/\s+/).length;
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
          src={article.imageUrl || '/logo.png'}
          alt={article.title}
          className="hero-image"
          loading="eager"
          onError={(e) => {
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

          {article.summary && (
            <p className="hero-summary">{article.summary}</p>
          )}

          <div className="hero-meta">
            <span className="hero-source">{article.source || 'News Stand NG'}</span>
            <span>·</span>
            <span>{formattedDate}</span>
            <span>·</span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
              <Clock size={13} />
              {readMinutes} min read
            </span>

            {/* Bookmark button */}
            <button
              type="button"
              className="card-bookmark-btn"
              style={{ position: 'static', marginLeft: 'auto' }}
              onClick={(e) => {
                e.stopPropagation();
                onToggleBookmark(article);
              }}
              title={isBookmarked ? 'Remove bookmark' : 'Bookmark story'}
              aria-label="Bookmark story"
            >
              <Bookmark
                size={16}
                fill={isBookmarked ? 'var(--gold-accent)' : 'none'}
                color={isBookmarked ? 'var(--gold-accent)' : 'currentColor'}
              />
            </button>
          </div>
        </div>
      </article>
    </section>
  );
}
