import React from 'react';
import { PenSquare, Bell, Search, Bookmark, Sun, Moon } from 'lucide-react';

export default function Navbar({
  theme,
  toggleTheme,
  onOpenSearch,
  bookmarkCount,
  onSelectBookmarks,
  activeCategory,
  onSelectNavTab,
}) {
  return (
    <header className="buletin-header" role="banner">
      <div className="app-container">
        <div className="buletin-header-inner">
          {/* Left: Brand + Navigation Links */}
          <div className="buletin-nav-left">
            <a href="/" className="buletin-brand-logo" aria-label="Buletin Homepage">
              <span className="brand-buletin">Buletin</span>
            </a>

            <div className="buletin-brand-divider" aria-hidden="true" />

            <nav className="buletin-nav-links" aria-label="Main Navigation">
              <button
                type="button"
                className={`buletin-nav-item ${activeCategory === 'All' ? 'active' : ''}`}
                onClick={() => onSelectNavTab && onSelectNavTab('Stories')}
              >
                Stories
              </button>
              <button
                type="button"
                className="buletin-nav-item"
                onClick={() => onSelectNavTab && onSelectNavTab('Creator')}
              >
                Creator
              </button>
              <button
                type="button"
                className="buletin-nav-item"
                onClick={() => onSelectNavTab && onSelectNavTab('Community')}
              >
                Community
              </button>
              <button
                type="button"
                className="buletin-nav-item"
                onClick={() => onSelectNavTab && onSelectNavTab('Subscribe')}
              >
                Subscribe
              </button>
            </nav>
          </div>

          {/* Right: Write Button + Bell + Avatar + Search + Theme Toggle */}
          <div className="buletin-nav-right">
            {/* Quick Search Shortcut */}
            <button
              type="button"
              className="buletin-search-trigger"
              onClick={onOpenSearch}
              aria-label="Search stories (Ctrl+K)"
              title="Search stories (Ctrl+K)"
            >
              <Search size={16} />
              <span className="search-text">Search...</span>
              <kbd className="search-kbd">⌘K</kbd>
            </button>

            {/* Write Button */}
            <button
              type="button"
              className="buletin-write-btn"
              onClick={() => {
                alert('The Buletin Creator Studio will be available shortly! You can submit drafts or story pitches.');
              }}
              aria-label="Write a story"
            >
              <PenSquare size={16} />
              <span>Write</span>
            </button>

            {/* Notifications Bell */}
            <button
              type="button"
              className="buletin-icon-btn"
              title="Notifications"
              aria-label="Notifications"
              onClick={() => alert('No new notifications right now.')}
            >
              <Bell size={18} />
              <span className="notification-unread-dot" />
            </button>

            {/* Saved Bookmarks */}
            <button
              type="button"
              className={`buletin-icon-btn ${activeCategory === 'Saved' ? 'active' : ''}`}
              onClick={onSelectBookmarks}
              title="Saved Stories"
              aria-label="View saved stories"
            >
              <Bookmark size={18} />
              {bookmarkCount > 0 && <span className="buletin-badge-counter">{bookmarkCount}</span>}
            </button>

            {/* Theme Toggle (Dark/Light) */}
            <button
              type="button"
              className="buletin-icon-btn"
              onClick={toggleTheme}
              title={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
              aria-label="Toggle theme"
            >
              {theme === 'dark' ? <Sun size={18} /> : <Moon size={18} />}
            </button>

            {/* User Profile Avatar */}
            <div className="buletin-avatar-wrap" title="User Profile">
              <img
                src="https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=120&h=120&q=80"
                alt="User Profile"
                className="buletin-avatar-img"
              />
            </div>
          </div>
        </div>
      </div>
    </header>
  );
}
