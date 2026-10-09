import React, { useRef, useState, useEffect, useCallback } from 'react';
import { Layers, Bookmark, Flame, Tv, ChevronLeft, ChevronRight, Globe } from 'lucide-react';
import { trackCategorySelect } from '../services/analytics';

const DEFAULT_CATEGORIES = [
  'All',
  'Politics',
  'Business',
  'Technology',
  'Sports',
  'Entertainment',
  'Crime',
  'International',
  'General',
  'Opinion',
];

export default function CategoryTabs({
  categories = DEFAULT_CATEGORIES,
  activeCategory = 'All',
  onSelectCategory,
  bookmarkCount = 0,
}) {
  const handleSelect = (cat) => {
    trackCategorySelect(cat);
    onSelectCategory?.(cat);
  };

  const listRef = useRef(null);
  const activePillRef = useRef(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const [startX, setStartX] = useState(0);
  const [scrollLeftState, setScrollLeftState] = useState(0);

  // Filter out any video or forum duplicates if present in categories from backend
  const categorySource = Array.isArray(categories) && categories.length > 0 ? categories : DEFAULT_CATEGORIES;
  const standardCategories = categorySource.filter(
    (c) => !['Videos', 'Video', 'Top Video', 'Top Videos', 'News Video', 'News Videos', 'Forum', 'Discussions'].includes(c)
  );
  const allCategories = Array.from(new Set(['All', ...standardCategories]));

  const isTopVideoActive = activeCategory === 'Top Videos' || activeCategory === 'Top Video';
  const isNewsVideoActive = activeCategory === 'News Videos' || activeCategory === 'News Video';

  const updateScrollButtons = useCallback(() => {
    if (listRef.current) {
      const { scrollLeft, scrollWidth, clientWidth } = listRef.current;
      setCanScrollLeft(scrollLeft > 6);
      setCanScrollRight(scrollLeft < scrollWidth - clientWidth - 6);
    }
  }, []);

  useEffect(() => {
    updateScrollButtons();
    const el = listRef.current;
    if (el) {
      el.addEventListener('scroll', updateScrollButtons, { passive: true });
      window.addEventListener('resize', updateScrollButtons);
      return () => {
        el.removeEventListener('scroll', updateScrollButtons);
        window.removeEventListener('resize', updateScrollButtons);
      };
    }
  }, [updateScrollButtons, categories]);

  // Auto-scroll active pill into view when activeCategory changes
  useEffect(() => {
    if (activePillRef.current) {
      activePillRef.current.scrollIntoView({
        behavior: 'smooth',
        block: 'nearest',
        inline: 'center',
      });
    }
    const timer = setTimeout(updateScrollButtons, 350);
    return () => clearTimeout(timer);
  }, [activeCategory, updateScrollButtons]);

  const handleScrollLeft = () => {
    if (listRef.current) {
      listRef.current.scrollBy({ left: -240, behavior: 'smooth' });
    }
  };

  const handleScrollRight = () => {
    if (listRef.current) {
      listRef.current.scrollBy({ left: 240, behavior: 'smooth' });
    }
  };

  // Mouse drag support for desktop/testing
  const handleMouseDown = (e) => {
    if (!listRef.current) return;
    setIsDragging(true);
    setStartX(e.pageX - listRef.current.offsetLeft);
    setScrollLeftState(listRef.current.scrollLeft);
  };

  const handleMouseMove = (e) => {
    if (!isDragging || !listRef.current) return;
    e.preventDefault();
    const x = e.pageX - listRef.current.offsetLeft;
    const walk = (x - startX) * 1.5;
    listRef.current.scrollLeft = scrollLeftState - walk;
  };

  const handleMouseUpOrLeave = () => {
    setIsDragging(false);
  };

  return (
    <nav className="category-nav-bar" aria-label="News categories">
      <div className="app-container">
        <div className="category-scroll-container">
          {canScrollLeft && (
            <button
              type="button"
              className="category-scroll-arrow category-arrow-left"
              onClick={handleScrollLeft}
              aria-label="Scroll categories left"
            >
              <ChevronLeft size={16} />
            </button>
          )}

          <div
            ref={listRef}
            className={`category-list ${isDragging ? 'is-dragging' : ''}`}
            role="tablist"
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUpOrLeave}
            onMouseLeave={handleMouseUpOrLeave}
          >
            {/* 1. All Stories */}
            <button
              ref={activeCategory === 'All' ? activePillRef : null}
              role="tab"
              id="tab-all"
              aria-selected={activeCategory === 'All'}
              type="button"
              className={`category-pill ${activeCategory === 'All' ? 'active' : ''}`}
              onClick={() => handleSelect('All')}
            >
              <Layers size={15} />
              <span>All Stories</span>
            </button>

            {/* 2. Top Video */}
            <button
              ref={isTopVideoActive ? activePillRef : null}
              role="tab"
              id="tab-top-video"
              aria-selected={isTopVideoActive}
              type="button"
              className={`category-pill pill-top-video ${isTopVideoActive ? 'active' : ''}`}
              onClick={() => handleSelect('Top Videos')}
              title="Top Trending YouTube News & Broadcasts"
            >
              <Flame size={16} className="pill-flame-icon" />
              <span>Top Videos</span>
              <span className="pill-badge-trending">Trending</span>
            </button>

            {/* 3. News Video */}
            <button
              ref={isNewsVideoActive ? activePillRef : null}
              role="tab"
              id="tab-news-video"
              aria-selected={isNewsVideoActive}
              type="button"
              className={`category-pill pill-news-video ${isNewsVideoActive ? 'active' : ''}`}
              onClick={() => handleSelect('News Videos')}
              title="Latest Broadcast News Videos from Channels TV, TVC, Arise & TheCable"
            >
              <span className="live-pulse-dot" />
              <Tv size={15} />
              <span>News Videos</span>
              <span className="pill-badge-live">Live Feeds</span>
            </button>

            {/* 4. Editorial Standard Categories */}
            {allCategories
              .filter((c) => c !== 'All')
              .map((cat) => {
                const isActive = activeCategory === cat;
                return (
                  <button
                    key={cat}
                    ref={isActive ? activePillRef : null}
                    role="tab"
                    id={`tab-${cat.toLowerCase().replace(/\s+/g, '-')}`}
                    aria-selected={isActive}
                    type="button"
                    className={`category-pill ${isActive ? 'active' : ''}`}
                    onClick={() => handleSelect(cat)}
                  >
                    {cat === 'International' && <Globe size={14} style={{ marginRight: 5, verticalAlign: 'text-bottom' }} />}
                    <span>{cat}</span>
                  </button>
                );
              })}

            {/* 5. Bookmarks Tab */}
            <button
              ref={activeCategory === 'Saved' ? activePillRef : null}
              role="tab"
              id="tab-saved"
              aria-selected={activeCategory === 'Saved'}
              type="button"
              className={`category-pill pill-saved ${activeCategory === 'Saved' ? 'active' : ''}`}
              onClick={() => handleSelect('Saved')}
            >
              <Bookmark size={15} />
              <span>Saved {bookmarkCount > 0 ? `(${bookmarkCount})` : ''}</span>
            </button>
          </div>

          {canScrollRight && (
            <button
              type="button"
              className="category-scroll-arrow category-arrow-right"
              onClick={handleScrollRight}
              aria-label="Scroll categories right"
            >
              <ChevronRight size={16} />
            </button>
          )}
        </div>
      </div>
    </nav>
  );
}
