import React, { useState, useEffect } from 'react';
import {
  Bell,
  Search,
  Bookmark,
  Sun,
  Moon,
  User,
  ShieldCheck,
  MessageSquare,
  MapPin,
  TrendingUp,
  ChevronDown,
  Menu,
  X,
  Radio,
  FileText,
  Compass,
  ArrowRight,
  Download,
  Smartphone,
} from 'lucide-react';

export default function Navbar({
  theme,
  toggleTheme,
  onOpenSearch,
  bookmarkCount,
  onSelectBookmarks,
  activeCategory,
  onSelectNavTab,
  onOpenNotifications,
  verifiedUser,
  onOpenAuth,
  onPromptInstall,
  isInstallable,
}) {
  const [showTrackersDropdown, setShowTrackersDropdown] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [showMobileTrackersSheet, setShowMobileTrackersSheet] = useState(false);

  // Lock body scroll when mobile menu or bottom sheet is open
  useEffect(() => {
    if (isMobileMenuOpen || showMobileTrackersSheet) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [isMobileMenuOpen, showMobileTrackersSheet]);

  const handleNavClick = (tab) => {
    if (onSelectNavTab) onSelectNavTab(tab);
    setIsMobileMenuOpen(false);
    setShowMobileTrackersSheet(false);
  };

  return (
    <>
      <header className="buletin-header" role="banner">
        <div className="app-container">
          <div className="buletin-header-inner">
            {/* Left: Mobile Hamburger Menu Toggle + Brand Logo */}
            <div className="buletin-nav-left">
              <button
                type="button"
                className="buletin-mobile-menu-btn"
                onClick={() => setIsMobileMenuOpen((prev) => !prev)}
                aria-label={isMobileMenuOpen ? 'Close navigation menu' : 'Open navigation menu'}
                aria-expanded={isMobileMenuOpen}
              >
                {isMobileMenuOpen ? <X size={22} /> : <Menu size={22} />}
              </button>

              <a href="/" className="buletin-brand-logo" aria-label="News Stand NG Homepage">
                <img
                  src="/newsstand-masthead.png"
                  alt="News Stand NG"
                  className="brand-masthead-img"
                  onError={(e) => {
                    e.currentTarget.style.display = 'none';
                    const fb = e.currentTarget.parentElement.querySelector('.brand-buletin');
                    if (fb) fb.style.display = 'inline-block';
                  }}
                />
                <span className="brand-buletin" style={{ display: 'none' }}>News Stand NG</span>
              </a>

              <div className="buletin-brand-divider" aria-hidden="true" />

              {/* Desktop Nav Links */}
              <nav className="buletin-nav-links" aria-label="Main Navigation">
                <button
                  type="button"
                  className={`buletin-nav-item ${activeCategory !== 'Forum' && activeCategory !== 'Saved' ? 'active' : ''}`}
                  onClick={() => onSelectNavTab && onSelectNavTab('Stories')}
                >
                  Top Stories
                </button>
                <a href="/states" className="buletin-nav-item" title="Local news across 36 Nigerian States & FCT">
                  <MapPin size={13} style={{ marginRight: 4, verticalAlign: 'text-bottom' }} />
                  <span>States</span>
                </a>

                {/* Trackers Dropdown Navigation */}
                <div
                  className="trackers-dropdown-wrapper"
                  onMouseEnter={() => setShowTrackersDropdown(true)}
                  onMouseLeave={() => setShowTrackersDropdown(false)}
                >
                  <button
                    type="button"
                    className={`buletin-nav-item trackers-nav-btn ${showTrackersDropdown ? 'active' : ''}`}
                    onClick={() => setShowTrackersDropdown((prev) => !prev)}
                    aria-expanded={showTrackersDropdown}
                    aria-haspopup="true"
                    title="Live Nigerian Market & Civic Trackers"
                  >
                    <TrendingUp size={13} style={{ marginRight: 4, verticalAlign: 'text-bottom' }} />
                    <span>Trackers</span>
                    <ChevronDown size={12} className={`trackers-chevron ${showTrackersDropdown ? 'rotated' : ''}`} />
                  </button>

                  {showTrackersDropdown && (
                    <div className="trackers-dropdown-menu" role="menu">
                      <div className="trackers-menu-header">Live Market & Civic Trackers</div>
                      <a href="/naira-exchange-rate" className="tracker-dropdown-item" role="menuitem">
                        <span className="tracker-icon-badge fx">₦</span>
                        <div className="tracker-item-text">
                          <div className="tracker-item-title">Naira FX Tracker</div>
                          <div className="tracker-item-desc">Parallel Market vs. CBN NAFEM Rates</div>
                        </div>
                      </a>
                      <a href="/elections" className="tracker-dropdown-item" role="menuitem">
                        <span className="tracker-icon-badge vote">🗳️</span>
                        <div className="tracker-item-text">
                          <div className="tracker-item-title">Elections Tracker</div>
                          <div className="tracker-item-desc">Off-Cycle Governorship & Electoral Timelines</div>
                        </div>
                      </a>
                    </div>
                  )}
                </div>

                <button
                  type="button"
                  className={`buletin-nav-item buletin-nav-forum ${activeCategory === 'Forum' ? 'active' : ''}`}
                  onClick={() => onSelectNavTab && onSelectNavTab('Forum')}
                  title="Community Discussions & Town Hall Debates"
                >
                  <MessageSquare size={14} style={{ marginRight: 5, verticalAlign: 'text-bottom' }} />
                  <span>Forum</span>
                  <span className="buletin-forum-nav-badge">Debates</span>
                </button>
              </nav>
            </div>

            {/* Right: Search + Bell + Saved + Theme Toggle + Sign In */}
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
                <span className="search-text">Search stories...</span>
                <kbd className="search-kbd">Ctrl K</kbd>
              </button>

              {/* Notifications Bell (Desktop/Tablet) */}
              <div className="btn-callout-wrapper buletin-desktop-action">
                <button
                  type="button"
                  className="buletin-nav-pill-btn"
                  aria-label="Breaking News Alerts"
                  onClick={onOpenNotifications}
                >
                  <Bell size={16} />
                  <span className="nav-btn-text">Alerts</span>
                </button>
                <div className="btn-callout bottom">
                  <span className="btn-callout-title">Breaking News Alerts</span>
                  <span className="btn-callout-desc">Get instant browser alerts for urgent breaking headlines</span>
                </div>
              </div>

              {/* Saved Bookmarks (Desktop/Tablet) */}
              <div className="btn-callout-wrapper buletin-desktop-action">
                <button
                  type="button"
                  className={`buletin-nav-pill-btn ${activeCategory === 'Saved' ? 'active' : ''}`}
                  onClick={onSelectBookmarks}
                  aria-label="View saved stories"
                >
                  <Bookmark
                    size={16}
                    fill={activeCategory === 'Saved' ? 'currentColor' : 'none'}
                  />
                  <span className="nav-btn-text">Saved</span>
                  {bookmarkCount > 0 && <span className="buletin-badge-counter">{bookmarkCount}</span>}
                </button>
                <div className="btn-callout bottom">
                  <span className="btn-callout-title">Saved Stories</span>
                  <span className="btn-callout-desc">Browse your bookmarked stories and offline reading collection</span>
                </div>
              </div>

              {/* Install App CTA (Desktop/Tablet) */}
              {isInstallable && (
                <div className="btn-callout-wrapper buletin-desktop-action">
                  <button
                    type="button"
                    className="buletin-nav-pill-btn pwa-nav-btn"
                    onClick={onPromptInstall}
                    aria-label="Install News Stand NG Web App"
                  >
                    <Download size={15} />
                    <span className="nav-btn-text">Install App</span>
                  </button>
                  <div className="btn-callout bottom">
                    <span className="btn-callout-title">Fast Web App</span>
                    <span className="btn-callout-desc">Install on your desktop or device for zero data lag & offline reading</span>
                  </div>
                </div>
              )}

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

              {/* User Profile / Authentication Button */}
              {verifiedUser ? (
                <div
                  className="buletin-user-profile-menu"
                  onClick={onOpenAuth}
                  title={`Signed in as ${verifiedUser.displayName || verifiedUser.name} (${verifiedUser.channel === 'google' ? 'Google' : 'Email'} Verified). Click to manage.`}
                  role="button"
                  tabIndex={0}
                >
                  {verifiedUser.avatarUrl ? (
                    <img
                      src={verifiedUser.avatarUrl}
                      alt={verifiedUser.displayName || 'Reader'}
                      className="buletin-avatar-img"
                      onError={(e) => {
                        e.currentTarget.style.display = 'none';
                      }}
                    />
                  ) : (
                    <div className="buletin-avatar-fallback">
                      {(verifiedUser.displayName || verifiedUser.name || 'U')[0].toUpperCase()}
                    </div>
                  )}
                  <span className="buletin-user-name">
                    {verifiedUser.displayName || verifiedUser.name}
                  </span>
                  <span className="buletin-verified-indicator" title="Verified Reader">
                    <ShieldCheck size={13} />
                  </span>
                </div>
              ) : (
                <button
                  type="button"
                  className="buletin-auth-btn"
                  onClick={onOpenAuth}
                  title="Sign in with Google or Email"
                  aria-label="Sign in with Google or Email"
                >
                  <User size={15} />
                  <span>Sign In</span>
                </button>
              )}
            </div>
          </div>
        </div>
      </header>

      {/* ── Slide-in Mobile Navigation Drawer ── */}
      {isMobileMenuOpen && (
        <div
          className="buletin-mobile-drawer-overlay"
          onClick={() => setIsMobileMenuOpen(false)}
          aria-hidden="true"
        />
      )}

      <aside
        className={`buletin-mobile-drawer ${isMobileMenuOpen ? 'open' : ''}`}
        aria-label="Mobile Navigation Menu"
        aria-hidden={!isMobileMenuOpen}
      >
        <div className="mobile-drawer-header">
          <a href="/" className="mobile-drawer-brand" onClick={() => setIsMobileMenuOpen(false)}>
            <img src="/newsstand-masthead.png" alt="News Stand NG" className="mobile-drawer-logo" />
          </a>
          <button
            type="button"
            className="mobile-drawer-close-btn"
            onClick={() => setIsMobileMenuOpen(false)}
            aria-label="Close menu"
          >
            <X size={20} />
          </button>
        </div>

        <div className="mobile-drawer-body">
          {/* User Status Card */}
          <div className="mobile-drawer-user-card" onClick={onOpenAuth}>
            {verifiedUser ? (
              <div className="mobile-user-info">
                {verifiedUser.avatarUrl ? (
                  <img src={verifiedUser.avatarUrl} alt="" className="mobile-user-avatar" />
                ) : (
                  <div className="mobile-user-avatar-fallback">
                    {(verifiedUser.displayName || verifiedUser.name || 'U')[0].toUpperCase()}
                  </div>
                )}
                <div>
                  <div className="mobile-user-display-name">
                    {verifiedUser.displayName || verifiedUser.name}
                    <ShieldCheck size={14} style={{ color: '#10B981', display: 'inline', marginLeft: 4 }} />
                  </div>
                  <div className="mobile-user-status-text">Verified Reader · Manage Profile</div>
                </div>
              </div>
            ) : (
              <div className="mobile-user-signin-prompt">
                <User size={18} />
                <span>Sign in for alerts & debates</span>
              </div>
            )}
          </div>

          {/* Primary Navigation Links */}
          <div className="mobile-drawer-section">
            <div className="mobile-section-heading">Navigation</div>
            <button
              type="button"
              className={`mobile-drawer-link ${activeCategory !== 'Forum' && activeCategory !== 'Saved' ? 'active' : ''}`}
              onClick={() => handleNavClick('Stories')}
            >
              <Compass size={18} />
              <span>Top Stories & Headlines</span>
            </button>

            <a
              href="/states"
              className="mobile-drawer-link"
              onClick={() => setIsMobileMenuOpen(false)}
            >
              <MapPin size={18} />
              <span>36 States & FCT Directory</span>
            </a>

            <button
              type="button"
              className={`mobile-drawer-link ${activeCategory === 'Forum' ? 'active' : ''}`}
              onClick={() => handleNavClick('Forum')}
            >
              <MessageSquare size={18} />
              <span>Town Hall & Debates</span>
              <span className="mobile-drawer-badge">Live</span>
            </button>
          </div>

          {/* Civic & Economic Trackers Section */}
          <div className="mobile-drawer-section">
            <div className="mobile-section-heading">Data Trackers</div>
            <a
              href="/naira-exchange-rate"
              className="mobile-tracker-link"
              onClick={() => setIsMobileMenuOpen(false)}
            >
              <span className="tracker-icon-badge fx">₦</span>
              <div>
                <div className="mobile-tracker-title">Naira FX Tracker</div>
                <div className="mobile-tracker-subtitle">Parallel vs. Official CBN NAFEM Rates</div>
              </div>
            </a>


            <a
              href="/elections"
              className="mobile-tracker-link"
              onClick={() => setIsMobileMenuOpen(false)}
            >
              <span className="tracker-icon-badge vote">🗳️</span>
              <div>
                <div className="mobile-tracker-title">Elections Tracker</div>
                <div className="mobile-tracker-subtitle">Off-Cycle Governorship & Electoral Timelines</div>
              </div>
            </a>
          </div>

          {/* Quick Reader Tools */}
          <div className="mobile-drawer-section">
            <div className="mobile-section-heading">Reader Tools</div>
            <button
              type="button"
              className="mobile-drawer-link"
              onClick={() => {
                setIsMobileMenuOpen(false);
                if (onOpenNotifications) onOpenNotifications();
              }}
            >
              <Bell size={18} />
              <span>Breaking News Alerts</span>
            </button>

            <button
              type="button"
              className="mobile-drawer-link"
              onClick={() => {
                setIsMobileMenuOpen(false);
                if (onSelectBookmarks) onSelectBookmarks();
              }}
            >
              <Bookmark size={18} />
              <span>Saved Stories & Reading List</span>
              {bookmarkCount > 0 && <span className="mobile-drawer-badge counter">{bookmarkCount}</span>}
            </button>

            {isInstallable && (
              <button
                type="button"
                className="mobile-drawer-link highlight-emerald"
                onClick={() => {
                  setIsMobileMenuOpen(false);
                  if (onPromptInstall) onPromptInstall();
                }}
              >
                <Download size={18} />
                <span>Install Web App (Offline & Fast)</span>
                <span className="mobile-drawer-badge new">PWA</span>
              </button>
            )}

            <button
              type="button"
              className="mobile-drawer-link"
              onClick={() => {
                toggleTheme();
              }}
            >
              {theme === 'dark' ? <Sun size={18} /> : <Moon size={18} />}
              <span>Appearance: {theme === 'dark' ? 'Dark Theme' : 'Light Theme'}</span>
            </button>
          </div>

          {/* Editorial Links Footer */}
          <div className="mobile-drawer-footer">
            <div className="mobile-footer-links">
              <a href="/editorial-policy" onClick={() => setIsMobileMenuOpen(false)}>Editorial Policy</a>
              <span>·</span>
              <a href="/corrections" onClick={() => setIsMobileMenuOpen(false)}>Corrections</a>
              <span>·</span>
              <a href="/about" onClick={() => setIsMobileMenuOpen(false)}>About Us</a>
              <span>·</span>
              <a href="/contact" onClick={() => setIsMobileMenuOpen(false)}>Contact</a>
            </div>
            <div className="mobile-footer-copy">© 2026 News Stand NG · Nigerian News Intelligence</div>
          </div>
        </div>
      </aside>
    </>
  );
}

