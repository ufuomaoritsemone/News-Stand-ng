import React, { useState, useEffect, useRef } from 'react';
import { Search, X, Clock, ArrowRight } from 'lucide-react';
import { api } from '../services/api';

export default function SearchModal({ isOpen, onClose, onSelectStory }) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [recentSearches, setRecentSearches] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem('nng_recent_searches') || '[]');
    } catch {
      return [];
    }
  });

  const inputRef = useRef(null);

  // Focus input when opened & listen for Escape key
  useEffect(() => {
    if (isOpen) {
      setTimeout(() => inputRef.current?.focus(), 50);
      const handleKeyDown = (e) => {
        if (e.key === 'Escape') onClose();
      };
      window.addEventListener('keydown', handleKeyDown);
      return () => window.removeEventListener('keydown', handleKeyDown);
    }
  }, [isOpen, onClose]);

  // Debounced live search
  useEffect(() => {
    if (!query.trim()) {
      setResults([]);
      setLoading(false);
      return;
    }

    const timer = setTimeout(async () => {
      setLoading(true);
      try {
        const data = await api.getArticles({ search: query.trim(), pageSize: 12 });
        setResults(Array.isArray(data) ? data : []);
      } catch (err) {
        console.error('Search error:', err);
        setResults([]);
      } finally {
        setLoading(false);
      }
    }, 280);

    return () => clearTimeout(timer);
  }, [query]);

  const handleSelect = (article) => {
    // Save to recent searches
    if (query.trim()) {
      const updated = [query.trim(), ...recentSearches.filter((s) => s.toLowerCase() !== query.trim().toLowerCase())].slice(0, 5);
      setRecentSearches(updated);
      try {
        localStorage.setItem('nng_recent_searches', JSON.stringify(updated));
      } catch {}
    }
    onSelectStory(article);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div
      className="search-overlay"
      onClick={(e) => e.target === e.currentTarget && onClose()}
      role="dialog"
      aria-modal="true"
      aria-label="Search stories"
    >
      <div className="search-modal">
        {/* Search Input Bar */}
        <div className="search-input-wrapper">
          <Search size={20} style={{ color: 'var(--emerald-bright)' }} />
          <input
            ref={inputRef}
            id="search-input-field"
            type="text"
            className="search-input"
            placeholder="Search breaking stories, politics, tech, finance..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          {query && (
            <button
              type="button"
              onClick={() => setQuery('')}
              className="icon-btn"
              style={{ width: 28, height: 28 }}
              aria-label="Clear search input"
            >
              <X size={14} />
            </button>
          )}
          <button
            type="button"
            onClick={onClose}
            className="icon-btn"
            style={{ width: 32, height: 32 }}
            aria-label="Close search"
          >
            <X size={18} />
          </button>
        </div>

        {/* Recent Searches Tags */}
        {!query && recentSearches.length > 0 && (
          <div style={{ padding: '16px 22px', borderBottom: '1px solid var(--border-subtle)' }}>
            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginBottom: 8, fontWeight: 600 }}>
              RECENT SEARCHES
            </div>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              {recentSearches.map((term) => (
                <button
                  key={term}
                  type="button"
                  onClick={() => setQuery(term)}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    padding: '4px 10px',
                    borderRadius: 9999,
                    background: 'var(--bg-elevated)',
                    border: '1px solid var(--border-subtle)',
                    fontSize: '0.8rem',
                    color: 'var(--text-secondary)',
                  }}
                >
                  <Clock size={12} />
                  {term}
                </button>
              ))}
            </div>
          </div>
        )}

        {/* Results Container */}
        <div className="search-results-list">
          {loading && (
            <div style={{ padding: '24px', textAlign: 'center', color: 'var(--text-muted)' }}>
              <div className="pulse-dot" style={{ display: 'inline-block', margin: '0 auto 8px auto', background: 'var(--emerald-bright)' }} />
              <div>Searching national archive...</div>
            </div>
          )}

          {!loading && query && results.length === 0 && (
            <div style={{ padding: '36px 20px', textAlign: 'center', color: 'var(--text-muted)' }}>
              No stories found for "{query}". Try checking keywords or exploring categories.
            </div>
          )}

          {!loading && results.map((item) => (
            <div
              key={item.id}
              className="search-result-item"
              onClick={() => handleSelect(item)}
            >
              <img
                src={item.imageUrl || '/logo.png'}
                alt=""
                className="search-result-thumb"
                onError={(e) => {
                  e.target.src = 'https://images.unsplash.com/photo-1504711434969-e33886168f5c?auto=format&fit=crop&w=150&q=80';
                }}
              />
              <div className="search-result-info">
                <div className="search-result-title">{item.title}</div>
                <div className="search-result-meta">
                  <span style={{ color: 'var(--emerald-bright)', fontWeight: 700 }}>{item.source}</span>
                  <span>·</span>
                  <span>{item.category || 'National'}</span>
                  {item.publishedAt && (
                    <>
                      <span>·</span>
                      <span>{new Date(item.publishedAt).toLocaleDateString('en-NG', { month: 'short', day: 'numeric' })}</span>
                    </>
                  )}
                </div>
              </div>
              <ArrowRight size={16} style={{ color: 'var(--text-muted)', flexShrink: 0 }} />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
