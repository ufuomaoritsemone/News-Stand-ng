import React, { useState, useEffect } from 'react';
import {
  Compass,
  MapPin,
  TrendingUp,
  MessageSquare,
  Search,
  X,
  ArrowRight,
} from 'lucide-react';

export default function MobileBottomNav({ onOpenSearch }) {
  const [showTrackersSheet, setShowTrackersSheet] = useState(false);
  const [currentPath, setCurrentPath] = useState('');
  const [currentSearch, setCurrentSearch] = useState('');

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const syncUrl = () => {
        setCurrentPath(window.location.pathname);
        setCurrentSearch(window.location.search);
      };
      syncUrl();
      window.addEventListener('popstate', syncUrl);
      return () => window.removeEventListener('popstate', syncUrl);
    }
  }, []);

  // Lock body scroll when bottom sheet is open
  useEffect(() => {
    if (typeof document !== 'undefined') {
      if (showTrackersSheet) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    }
    return () => {
      if (typeof document !== 'undefined') {
        document.body.style.overflow = '';
      }
    };
  }, [showTrackersSheet]);

  const handleSearchClick = () => {
    if (onOpenSearch) {
      onOpenSearch();
    } else if (typeof window !== 'undefined') {
      // Dispatches custom event so any active SearchModal can listen and open
      window.dispatchEvent(new CustomEvent('open-quick-search'));
    }
  };

  const isForumTab = new URLSearchParams(currentSearch).get('tab')?.toLowerCase() === 'forum';
  const isHome =
    (currentPath === '/' || currentPath === '' || currentPath.startsWith('/category/')) && !isForumTab;
  const isStates = currentPath.startsWith('/state');
  const isTracker = [
    '/naira-exchange-rate',
    '/fuel-price-nigeria',
    '/jamb-waec-news',
    '/elections',
  ].some((t) => currentPath.startsWith(t));

  return (
    <>
      <nav className="buletin-mobile-bottom-bar" aria-label="Mobile Bottom Navigation">
        <a
          href="/"
          className={`mobile-bottom-tab ${isHome && !showTrackersSheet ? 'active' : ''}`}
          aria-label="Headlines & Top Stories"
        >
          <Compass size={20} />
          <span className="mobile-tab-label">Stories</span>
        </a>

        <a
          href="/states"
          className={`mobile-bottom-tab ${isStates && !showTrackersSheet ? 'active' : ''}`}
          aria-label="36 Nigerian States & FCT"
        >
          <MapPin size={20} />
          <span className="mobile-tab-label">States</span>
        </a>

        <button
          type="button"
          className={`mobile-bottom-tab ${isTracker || showTrackersSheet ? 'active' : ''}`}
          onClick={() => setShowTrackersSheet((prev) => !prev)}
          aria-label="Civic & Market Trackers"
          aria-expanded={showTrackersSheet}
        >
          <TrendingUp size={20} />
          <span className="mobile-tab-label">Trackers</span>
        </button>

        <a
          href="/?tab=forum"
          className={`mobile-bottom-tab ${isForumTab && !showTrackersSheet ? 'active' : ''}`}
          aria-label="Forum: Town Hall & Debates"
        >
          <MessageSquare size={20} />
          <span className="mobile-tab-label">Forum</span>
        </a>

        <button
          type="button"
          className="mobile-bottom-tab"
          onClick={handleSearchClick}
          aria-label="Search Archive"
        >
          <Search size={20} />
          <span className="mobile-tab-label">Search</span>
        </button>
      </nav>

      {/* ── Mobile Trackers Quick Sheet ── */}
      {showTrackersSheet && (
        <div
          className="mobile-trackers-sheet-overlay"
          onClick={() => setShowTrackersSheet(false)}
          aria-hidden="true"
        />
      )}

      <div
        className={`mobile-trackers-sheet ${showTrackersSheet ? 'open' : ''}`}
        aria-label="Live Nigerian Data Trackers"
        aria-hidden={!showTrackersSheet}
      >
        <div className="mobile-sheet-drag-handle" />
        <div className="mobile-sheet-header">
          <div className="mobile-sheet-title">
            <TrendingUp size={18} style={{ color: '#10B981' }} />
            <span>Nigerian Data Trackers</span>
          </div>
          <button
            type="button"
            className="mobile-sheet-close"
            onClick={() => setShowTrackersSheet(false)}
            aria-label="Close trackers sheet"
          >
            <X size={18} />
          </button>
        </div>

        <div className="mobile-sheet-items">
          <a
            href="/naira-exchange-rate"
            className={`mobile-sheet-tracker-card ${currentPath === '/naira-exchange-rate' ? 'active' : ''}`}
            onClick={() => setShowTrackersSheet(false)}
          >
            <span className="tracker-icon-badge fx">₦</span>
            <div className="sheet-tracker-info">
              <div className="sheet-tracker-title">Naira FX Tracker</div>
              <div className="sheet-tracker-desc">Parallel Market vs. CBN NAFEM Rates</div>
            </div>
            <ArrowRight size={16} className="sheet-tracker-arrow" />
          </a>



          <a
            href="/elections"
            className={`mobile-sheet-tracker-card ${currentPath === '/elections' ? 'active' : ''}`}
            onClick={() => setShowTrackersSheet(false)}
          >
            <span className="tracker-icon-badge vote">🗳️</span>
            <div className="sheet-tracker-info">
              <div className="sheet-tracker-title">Elections Tracker</div>
              <div className="sheet-tracker-desc">Off-Cycle Governorship & Timelines</div>
            </div>
            <ArrowRight size={16} className="sheet-tracker-arrow" />
          </a>
        </div>
      </div>
    </>
  );
}
