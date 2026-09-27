import React from 'react';
import { Layers, Bookmark, Video } from 'lucide-react';

const DEFAULT_CATEGORIES = [
  'All',
  'Politics',
  'Business',
  'Technology',
  'Sports',
  'Entertainment',
  'Opinion',
  'World',
];

export default function CategoryTabs({
  categories = DEFAULT_CATEGORIES,
  activeCategory = 'All',
  onSelectCategory,
  bookmarkCount = 0,
}) {
  const allCategories = Array.from(new Set(['All', ...categories]));

  return (
    <nav className="category-nav-bar" aria-label="News categories">
      <div className="app-container">
        <div className="category-list" role="tablist">
          {allCategories.map((cat) => {
            const isActive = activeCategory === cat;
            return (
              <button
                key={cat}
                role="tab"
                id={`tab-${cat.toLowerCase().replace(/\s+/g, '-')}`}
                aria-selected={isActive}
                type="button"
                className={`category-pill ${isActive ? 'active' : ''}`}
                onClick={() => onSelectCategory(cat)}
              >
                {cat === 'All' && <Layers size={14} />}
                {cat}
              </button>
            );
          })}

          {/* Videos Shortcut */}
          <button
            role="tab"
            id="tab-videos"
            aria-selected={activeCategory === 'Videos'}
            type="button"
            className={`category-pill ${activeCategory === 'Videos' ? 'active' : ''}`}
            onClick={() => onSelectCategory('Videos')}
          >
            <Video size={14} />
            Videos
          </button>

          {/* Bookmarks Tab */}
          <button
            role="tab"
            id="tab-saved"
            aria-selected={activeCategory === 'Saved'}
            type="button"
            className={`category-pill ${activeCategory === 'Saved' ? 'active' : ''}`}
            onClick={() => onSelectCategory('Saved')}
          >
            <Bookmark size={14} />
            Saved {bookmarkCount > 0 ? `(${bookmarkCount})` : ''}
          </button>
        </div>
      </div>
    </nav>
  );
}
