import React, { useState, useEffect, useRef } from 'react';
import { X, Bookmark, Share2, ExternalLink, Type } from 'lucide-react';
import { api } from '../services/api';

export default function ReaderDrawer({
  article,
  isOpen,
  onClose,
  isBookmarked,
  onToggleBookmark,
  onSelectRelatedStory,
}) {
  const [fontSizeLevel, setFontSizeLevel] = useState(1); // 0: small, 1: medium, 2: large
  const [scrollProgress, setScrollProgress] = useState(0);
  const [relatedStories, setRelatedStories] = useState([]);
  const [copied, setCopied] = useState(false);
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

  // Fetch related stories when an article is opened
  useEffect(() => {
    if (article && isOpen) {
      setScrollProgress(0);
      if (drawerRef.current) drawerRef.current.scrollTop = 0;

      api.getRelatedStories(article.id, article.title, article.category, 3)
        .then((res) => {
          setRelatedStories(Array.isArray(res) ? res : []);
        })
        .catch(() => setRelatedStories([]));

      // Track engagement telemetry
      api.trackEngagement(article.id, 'read_open');
    }
  }, [article, isOpen]);

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
    const shareData = {
      title: article.title,
      text: article.summary || article.title,
      url: article.url || window.location.href,
    };

    if (navigator.share) {
      try {
        await navigator.share(shareData);
      } catch {}
    } else {
      navigator.clipboard.writeText(shareData.url);
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

          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            {/* Bookmark button */}
            <button
              type="button"
              className="icon-btn"
              onClick={() => onToggleBookmark(article)}
              title={isBookmarked ? 'Remove bookmark' : 'Bookmark article'}
            >
              <Bookmark
                size={16}
                fill={isBookmarked ? 'var(--gold-accent)' : 'none'}
                color={isBookmarked ? 'var(--gold-accent)' : 'currentColor'}
              />
            </button>

            {/* Share button */}
            <button
              type="button"
              className="icon-btn"
              onClick={handleShare}
              title={copied ? 'Link copied!' : 'Share article'}
            >
              <Share2 size={16} />
            </button>

            {/* Close button */}
            <button
              type="button"
              className="icon-btn"
              onClick={onClose}
              title="Close reader (Esc)"
            >
              <X size={18} />
            </button>
          </div>
        </div>

        {/* Reader Body */}
        <div className="reader-content-body">
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
            <span className="card-category-badge" style={{ position: 'static' }}>
              {article.category || 'National'}
            </span>
            <span style={{ color: 'var(--emerald-bright)', fontWeight: 700, fontSize: '0.85rem' }}>
              {article.source || 'Nigerian News Grid'}
            </span>
            {article.isSponsored && (
              <span className="sponsored-badge">Sponsored · {article.sponsorName || 'Partner'}</span>
            )}
          </div>

          <h1 className="reader-title">{article.title}</h1>

          <div style={{ display: 'flex', alignItems: 'center', gap: 12, fontSize: '0.82rem', color: 'var(--text-muted)', marginBottom: 20 }}>
            {article.author && <span>By {article.author}</span>}
            {article.author && <span>·</span>}
            <span>{formattedDate}</span>
          </div>

          {/* Featured Image */}
          {article.imageUrl && (
            <img
              src={article.imageUrl}
              alt={article.title}
              className="reader-hero-image"
              onError={(e) => { e.target.style.display = 'none'; }}
            />
          )}

          {/* Lead / Summary */}
          {article.summary && (
            <div className="reader-lead">
              {article.summary}
            </div>
          )}

          {/* Main Content paragraphs */}
          <div style={{ whiteSpace: 'pre-line' }}>
            {article.content || article.summary || 'Read full coverage at the original publisher.'}
          </div>

          {/* Actions & Attribution */}
          <div className="reader-actions-bar">
            {article.url && (
              <a
                href={article.url}
                target="_blank"
                rel="noopener noreferrer"
                className="primary-btn"
              >
                <span>Read on {article.source || 'Publisher'}</span>
                <ExternalLink size={15} />
              </a>
            )}

            {copied && (
              <span style={{ fontSize: '0.82rem', color: 'var(--emerald-bright)', fontWeight: 600 }}>
                ✓ Link copied to clipboard
              </span>
            )}
          </div>

          {/* Related Stories */}
          {relatedStories.length > 0 && (
            <div style={{ marginTop: 48, paddingTop: 32, borderTop: '1px solid var(--border-subtle)' }}>
              <div style={{ fontSize: '1rem', fontWeight: 800, marginBottom: 16 }}>
                RELATED COVERAGE
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                {relatedStories.map((rel) => (
                  <div
                    key={rel.id}
                    onClick={() => onSelectRelatedStory(rel)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 12,
                      padding: 12,
                      borderRadius: 8,
                      background: 'var(--bg-card)',
                      border: '1px solid var(--border-card)',
                      cursor: 'pointer',
                    }}
                  >
                    <img
                      src={rel.imageUrl || '/logo.png'}
                      alt=""
                      style={{ width: 60, height: 45, borderRadius: 6, objectFit: 'cover' }}
                      onError={(e) => { e.target.src = '/logo.png'; }}
                    />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: '0.88rem', fontWeight: 700, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        {rel.title}
                      </div>
                      <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', marginTop: 2 }}>
                        {rel.source} · {rel.category}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </aside>
    </>
  );
}
