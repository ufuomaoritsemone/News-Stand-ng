import React, { useState, useEffect, useCallback, useMemo, useRef, Suspense } from 'react';
import Navbar from './components/Navbar';
import WelcomeBanner from './components/WelcomeBanner';
import BreakingTicker from './components/BreakingTicker';
import AudioBriefingBar from './components/AudioBriefingBar';
import HeroCarousel from './components/HeroCarousel';
import ArticleCard from './components/ArticleCard';
import CategoryTabs from './components/CategoryTabs';
import SearchModal from './components/SearchModal';
import ReaderDrawer from './components/ReaderDrawer';
import VideoReel from './components/VideoReel';
import ForumFeed from './components/ForumFeed';
import PartnerPerspectives from './components/PartnerPerspectives';
import NotificationModal from './components/NotificationModal';
import Footer from './components/Footer';
import { usePwaInstall } from './hooks/usePwaInstall';
import IosInstallModal from './components/IosInstallModal';
import PwaQuickPill from './components/PwaQuickPill';
import PwaInstallFeedCard from './components/PwaInstallFeedCard';

const VideoSection = React.lazy(() => import('./components/VideoSection'));
const AuthModal = React.lazy(() => import('./components/AuthModal'));
import { api } from './services/api';
import { getVerifiedUser } from './services/forumUser';
import { getRecencyTag } from './services/articleMetadata';
import { RefreshCw, BookmarkCheck, ArrowRight, Filter, AlertTriangle, Radio } from 'lucide-react';

// Authentic Nigerian baseline fallback stories (used only when offline and cache is empty)
const AUTHENTIC_NIGERIAN_FALLBACKS = [
  {
    id: 'nng-fallback-cbn',
    title: 'Central Bank Expands Foreign Exchange Liquidity Benchmarks for SME Import Windows',
    source: 'BusinessDay',
    publishedAt: new Date(Date.now() - 25 * 60 * 1000).toISOString(),
    category: 'Business',
    summary:
      'The Central Bank of Nigeria has issued revised operational guidelines intended to improve dollar liquidity access for manufacturing and agricultural raw materials importers.',
    content:
      'The Central Bank of Nigeria has issued revised operational guidelines intended to improve dollar liquidity access for manufacturing and agricultural raw materials importers.\n\nFinancial sector analysts in Lagos note that the transparent pricing framework aims to reduce parallel market volatility while supporting local manufacturing output across key commercial hubs.',
    imageUrl: 'https://images.unsplash.com/photo-1526304640581-d334cdbbf45e?auto=format&fit=crop&w=800&q=80',
    author: 'Finance Bureau',
    commentCount: 4,
  },
  {
    id: 'nng-fallback-grid',
    title: 'Transmission Company Completes Upgrades on Northern Regional High-Voltage Grid Lines',
    source: 'Channels Television',
    publishedAt: new Date(Date.now() - 40 * 60 * 1000).toISOString(),
    category: 'Technology',
    summary:
      'Engineering teams have energized new 330kV substations designed to boost power distribution stability and prevent frequent national grid collapses.',
    content:
      'Engineering teams have energized new 330kV substations designed to boost power distribution stability and prevent frequent national grid collapses.\n\nThe project integrates SCADA monitoring systems enabling real-time fault detection and remote load balancing across regional distribution centers.',
    imageUrl: 'https://images.unsplash.com/photo-1509391365360-2e959784a276?auto=format&fit=crop&w=800&q=80',
    author: 'Energy Reporter',
    commentCount: 2,
  },
  {
    id: 'nng-fallback-tech',
    title: 'Lagos Tech Hubs Secure New Seed & Series A Tranches Amid African Fintech Expansion',
    source: 'TheCable',
    publishedAt: new Date(Date.now() - 75 * 60 * 1000).toISOString(),
    category: 'Technology',
    summary:
      'Nigerian software engineers and enterprise platforms continue to attract institutional backing for cross-border payment rails and identity verification tools.',
    content:
      'Nigerian software engineers and enterprise platforms continue to attract institutional backing for cross-border payment rails and identity verification tools.\n\nIndustry leaders at the Yaba Innovation Summit emphasized that domestic engineering talent is powering financial inclusion across West Africa.',
    imageUrl: 'https://images.unsplash.com/photo-1531482615713-2afd69097998?auto=format&fit=crop&w=800&q=80',
    author: 'Technology Desk',
    commentCount: 6,
  },
  {
    id: 'nng-fallback-sports',
    title: 'Super Eagles Technical Committee Names Provisional Roster for African Cup Qualifiers',
    source: 'Vanguard News',
    publishedAt: new Date(Date.now() - 110 * 60 * 1000).toISOString(),
    category: 'Sports',
    summary:
      'The national team coaching staff has invited a balanced squad of domestic league stars and European-based regulars ahead of next month’s international fixtures.',
    content:
      'The national team coaching staff has invited a balanced squad of domestic league stars and European-based regulars ahead of next month’s international fixtures.\n\nPreparations will open with intensive tactical camps in Uyo before the squad departs for the away leg.',
    imageUrl: 'https://images.unsplash.com/photo-1508098682722-e99c43a406b2?auto=format&fit=crop&w=800&q=80',
    author: 'Sports Editor',
    commentCount: 3,
  },
];

export default function App({ initialData = null, initialCategory = 'All' }) {
  // Theme State
  const [theme, setTheme] = useState(() => {
    try {
      return localStorage.getItem('nng_theme') || 'light';
    } catch {
      return 'light';
    }
  });

  // PWA Installation Hook
  const {
    isInstallable,
    isInstalled,
    isIos,
    hasEngaged,
    isDismissed,
    showIosModal,
    setShowIosModal,
    promptInstall,
    dismissPrompt,
    recordEngagement,
  } = usePwaInstall();

  // Data States
  const [articles, setArticles] = useState(() => initialData?.initialArticles || []);
  const [isOfflineCached, setIsOfflineCached] = useState(false);
  const [cachedTime, setCachedTime] = useState('');
  const [hasNewStoriesAlert, setHasNewStoriesAlert] = useState(false);
  const [sponsoredArticles, setSponsoredArticles] = useState(() => initialData?.initialSponsored || []);
  const [videoStories, setVideoStories] = useState([]);
  const [topVideos, setTopVideos] = useState([]);
  const [newsVideos, setNewsVideos] = useState([]);
  const [loadingVideos, setLoadingVideos] = useState(false);
  const [categories, setCategories] = useState(() => initialData?.initialCategories || [
    'Politics',
    'Business',
    'Technology',
    'Sports',
    'Entertainment',
    'Crime',
    'International',
    'General',
    'Opinion',
  ]);
  const [activeCategory, setActiveCategory] = useState(initialCategory || 'All');
  const [selectedSource, setSelectedSource] = useState('All');
  const [sources, setSources] = useState(() => initialData?.initialSources || []);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);

  // Reader, Search, Notification, and Auth Modal States
  const [selectedArticle, setSelectedArticle] = useState(null);
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [isNotificationOpen, setIsNotificationOpen] = useState(false);
  const [isAuthOpen, setIsAuthOpen] = useState(false);
  const [verifiedUser, setVerifiedUser] = useState(() => getVerifiedUser());

  // Bookmarks State
  const [bookmarks, setBookmarks] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem('nng_saved_stories') || '[]');
    } catch {
      return [];
    }
  });

  // Apply Theme to document root
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('nng_theme', theme);
  }, [theme]);

  const toggleTheme = () => {
    setTheme((prev) => (prev === 'dark' ? 'light' : 'dark'));
  };

  // Keyboard shortcut for Cmd/Ctrl + K or "/"
  useEffect(() => {
    const handleKeyDown = (e) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setIsSearchOpen(true);
      } else if (e.key === '/' && !['INPUT', 'TEXTAREA'].includes(document.activeElement?.tagName)) {
        e.preventDefault();
        setIsSearchOpen(true);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  const [initialScrollToComments, setInitialScrollToComments] = useState(false);

  // Open & Close Article with Browser History (Back-button & deep linking support)
  const openArticle = useCallback((story, options = {}) => {
    if (!story) return;
    recordEngagement();
    setSelectedArticle(story);
    setInitialScrollToComments(Boolean(options?.scrollToComments));
    if (story.id) {
      const url = new URL(window.location.href);
      url.searchParams.set('story', story.id);
      window.history.pushState({ storyId: story.id }, '', url.toString());
    }
  }, []);

  const closeArticle = useCallback(() => {
    setSelectedArticle(null);
    const url = new URL(window.location.href);
    if (url.searchParams.has('story')) {
      url.searchParams.delete('story');
      const cleanPath = url.pathname + (url.search ? url.search : '');
      window.history.pushState({}, '', cleanPath);
    }
  }, []);

  // Listen to popstate for browser back/forward navigation
  useEffect(() => {
    const handlePopState = () => {
      const params = new URLSearchParams(window.location.search);
      const storyId = params.get('story');
      if (!storyId) {
        setSelectedArticle(null);
      } else {
        const found = articles.find((a) => a.id === storyId);
        if (found) {
          setSelectedArticle(found);
        } else {
          api.getArticleById(storyId)
            .then((story) => setSelectedArticle(story))
            .catch(() => {});
        }
      }
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, [articles]);

  // Initial Deep-Link check on mount (e.g. user opens ?story=123 from a shared link)
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const initialStoryId = params.get('story');
    if (initialStoryId) {
      api.getArticleById(initialStoryId)
        .then((story) => {
          if (story) setSelectedArticle(story);
        })
        .catch(() => {});
    }
  }, []);

  // Fetch Initial Categories, Sources, Videos, and Sponsored Campaigns
  useEffect(() => {
    api.getCategories()
      .then((data) => setCategories(data))
      .catch(() => {});

    api.getSources()
      .then((data) => setSources(data))
      .catch(() => {});

    api.getSponsoredArticles()
      .then((data) => {
        const list = Array.isArray(data) && data.length > 0 ? data : [];
        setSponsoredArticles(list);
      })
      .catch((err) => console.warn('Sponsored stories load notice:', err.message));

    setLoadingVideos(true);
    Promise.all([
      api.getTopVideos().catch(() => []),
      api.getNewsVideos().catch(() => []),
    ])
      .then(([top, news]) => {
        const topList = Array.isArray(top) && top.length > 0 ? top : [];
        const newsList = Array.isArray(news) && news.length > 0 ? news : [];
        setTopVideos(topList);
        setNewsVideos(newsList);
        setVideoStories(newsList.length > 0 ? newsList : topList);
      })
      .finally(() => {
        setLoadingVideos(false);
      });
  }, []);

  const isTopVideos = activeCategory === 'Top Videos' || activeCategory === 'Top Video';
  const isNewsVideos = activeCategory === 'News Videos' || activeCategory === 'News Video';
  const isForum = activeCategory === 'Forum' || activeCategory === 'Discussions';
  const isVideoCategory = isTopVideos || isNewsVideos || activeCategory === 'Videos';

  // Fetch Articles with Offline Cache Resilience
  const loadArticles = useCallback(async (reset = false, targetPage = 1) => {
    if (activeCategory === 'Saved' || isVideoCategory || isForum) {
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const data = await api.getArticles({
        page: targetPage,
        pageSize: 32,
        category: activeCategory,
        source: selectedSource,
      });

      const list = Array.isArray(data) ? data : [];
      setArticles((prev) => (reset ? list : [...prev, ...list]));
      setHasMore(list.length >= 32);
      setPage(targetPage);
      setIsOfflineCached(false);

      // Cache homepage stories in localStorage for offline reading
      if (activeCategory === 'All' && selectedSource === 'All' && reset && list.length > 0) {
        try {
          localStorage.setItem('nng_cached_articles', JSON.stringify(list));
          localStorage.setItem('nng_cached_articles_time', new Date().toISOString());
        } catch {}
      }
    } catch (err) {
      console.warn('Backend API connection notice:', err.message);

      // Attempt loading from cache if first page
      if (reset && activeCategory === 'All' && selectedSource === 'All') {
        try {
          const cachedJson = localStorage.getItem('nng_cached_articles');
          const cachedSavedTime = localStorage.getItem('nng_cached_articles_time');
          if (cachedJson) {
            const cachedList = JSON.parse(cachedJson);
            if (Array.isArray(cachedList) && cachedList.length > 0) {
              setArticles(cachedList);
              setIsOfflineCached(true);
              setCachedTime(cachedSavedTime ? new Date(cachedSavedTime).toLocaleTimeString('en-NG', { hour: '2-digit', minute: '2-digit' }) : '');
              return;
            }
          }
        } catch {}
      }

      if (reset) {
        setArticles([]);
      }
    } finally {
      setLoading(false);
    }
  }, [activeCategory, selectedSource, isVideoCategory, isForum]);

  const hasInitialData = useRef(Boolean(initialData?.initialArticles?.length));

  useEffect(() => {
    if (hasInitialData.current) {
      hasInitialData.current = false;
      return;
    }
    loadArticles(true, 1);
  }, [loadArticles]);

  const [newStoriesCount, setNewStoriesCount] = useState(0);

  // Periodic background check & focus check for live news updates (every 45 seconds when page is visible)
  const checkForNewStories = useCallback(async () => {
    if (activeCategory !== 'All' || selectedSource !== 'All' || articles.length === 0) return;
    try {
      const freshData = await api.getArticles({ page: 1, pageSize: 12 });
      const freshList = Array.isArray(freshData) ? freshData : (freshData?.articles || []);
      if (freshList.length > 0) {
        const currentTopId = articles[0]?.id;
        const currentIds = new Set(articles.slice(0, 15).map(a => a.id));
        const newItems = freshList.filter(a => !currentIds.has(a.id));
        if (newItems.length > 0 && freshList[0]?.id !== currentTopId) {
          setNewStoriesCount(newItems.length);
          setHasNewStoriesAlert(true);
        }
      }
    } catch {}
  }, [articles, activeCategory, selectedSource]);

  useEffect(() => {
    const interval = setInterval(() => {
      if (document.visibilityState === 'visible') {
        checkForNewStories();
      }
    }, 45000);

    const handleFocus = () => {
      checkForNewStories();
    };

    window.addEventListener('focus', handleFocus);
    return () => {
      clearInterval(interval);
      window.removeEventListener('focus', handleFocus);
    };
  }, [checkForNewStories]);

  // Bookmarking Handlers
  const toggleBookmark = (article) => {
    if (!article) return;
    setBookmarks((prev) => {
      const exists = prev.some((item) => item.id === article.id);
      const next = exists
        ? prev.filter((item) => item.id !== article.id)
        : [article, ...prev];
      try {
        localStorage.setItem('nng_saved_stories', JSON.stringify(next));
      } catch {}
      return next;
    });
  };

  const isArticleBookmarked = (id) => {
    return bookmarks.some((item) => item.id === id);
  };

  // Determine what list to display
  let displayedArticles = [];
  if (activeCategory === 'Saved') {
    displayedArticles = bookmarks;
  } else {
    // If backend returns articles, use them; if entirely empty and on home with no articles, use authentic fallback
    if (articles.length > 0) {
      displayedArticles = articles;
    } else if (activeCategory === 'All' && selectedSource === 'All') {
      displayedArticles = AUTHENTIC_NIGERIAN_FALLBACKS;
    } else {
      displayedArticles = [];
    }

    // Filter by source if selected
    if (selectedSource !== 'All' && displayedArticles.length > 0) {
      displayedArticles = displayedArticles.filter(
        (a) => (a.source || '').toLowerCase().includes(selectedSource.toLowerCase())
      );
    }
  }

  const allStories = articles.length > 0 ? articles : (activeCategory === 'All' ? AUTHENTIC_NIGERIAN_FALLBACKS : []);

  // Hero Carousel Stories (Top 6 most recent verified stories)
  const carouselStories = allStories.slice(0, 6);

  // Latest News Grid Stories (remaining stories)
  const latestNewsStories =
    activeCategory === 'All' && selectedSource === 'All' && displayedArticles.length > 6
      ? displayedArticles.slice(6)
      : displayedArticles;

  // Active Sponsored Feature (Partner Studio)
  const featuredSponsoredStory = sponsoredArticles.find((s) => s.isSponsored) || null;

  // Placement 1: Native In-Feed Insertion ("The 3rd / 4th Card Sweet Spot")
  const gridStoriesWithSponsor = useMemo(() => {
    if (!featuredSponsoredStory || activeCategory === 'Saved') return latestNewsStories;
    if (latestNewsStories.some((s) => s.id === featuredSponsoredStory.id)) return latestNewsStories;

    const copy = [...latestNewsStories];
    const targetIdx = Math.min(2, copy.length);
    copy.splice(targetIdx, 0, featuredSponsoredStory);
    return copy;
  }, [latestNewsStories, featuredSponsoredStory, activeCategory]);

  // Breaking News stories for the top ticker
  const breakingNewsItems = useMemo(() => {
    return allStories.filter((s) => {
      const tag = getRecencyTag(s.publishedAt);
      return tag && tag.type === 'breaking';
    });
  }, [allStories]);

  return (
    <div className="app-shell" style={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      {/* 1. Header & Navigation */}
      <Navbar
        theme={theme}
        toggleTheme={toggleTheme}
        onOpenSearch={() => setIsSearchOpen(true)}
        bookmarkCount={bookmarks.length}
        onSelectBookmarks={() => setActiveCategory('Saved')}
        activeCategory={activeCategory}
        onSelectNavTab={(tab) => {
          if (tab === 'Stories') setActiveCategory('All');
          else if (tab === 'Forum') setActiveCategory('Forum');
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }}
        onOpenNotifications={() => setIsNotificationOpen(true)}
        verifiedUser={verifiedUser}
        onOpenAuth={() => setIsAuthOpen(true)}
        onPromptInstall={promptInstall}
        isInstallable={isInstallable && !isInstalled}
      />

      {/* Breaking News Live Ticker (Appears when breaking stories are active) */}
      {breakingNewsItems.length > 0 && activeCategory !== 'Saved' && (
        <BreakingTicker
          items={breakingNewsItems}
          onSelectStory={openArticle}
        />
      )}

      {/* Offline Cache Status Notification */}
      {isOfflineCached && (
        <div className="offline-notice-bar app-container" role="status">
          <AlertTriangle size={15} style={{ marginRight: 6 }} />
          <span>You are browsing cached news from {cachedTime || 'earlier'}. Connect to network to refresh live feeds.</span>
        </div>
      )}

      {/* Floating Live News Update Pill */}
      {hasNewStoriesAlert && (
        <div className="floating-news-update-pill-wrap">
          <button
            type="button"
            className="floating-news-update-pill"
            onClick={() => {
              setHasNewStoriesAlert(false);
              setNewStoriesCount(0);
              loadArticles(true, 1);
              window.scrollTo({ top: 0, behavior: 'smooth' });
            }}
            aria-label="Refresh news feed with latest stories"
          >
            <span className="pulse-indicator-dot" />
            <Radio size={14} className="pulse-icon" />
            <span className="pill-text-content">
              {newStoriesCount > 0 ? `${newStoriesCount} New Stories Available` : 'Fresh News Published'} — Click to update
            </span>
            <RefreshCw size={12} className="pill-refresh-icon" />
          </button>
        </div>
      )}

      {/* 2. Editorial Masthead & Date Strip */}
      {activeCategory !== 'Saved' && !isForum && (
        <WelcomeBanner
          brandName="NEWS STAND NG"
          storyCount={articles.length}
        />
      )}

      {/* Tri-Tier Neural Audio Daily Briefing Player */}
      {activeCategory === 'All' && !isForum && (
        <AudioBriefingBar />
      )}

      {/* Main Content Area */}
      <main id="main-content" style={{ flex: 1 }}>
        {/* 3. Hero Section: Carousel of Top Verified Nigerian Stories */}
        {activeCategory !== 'Saved' && !isTopVideos && !isNewsVideos && !isForum && carouselStories.length > 0 && (
          <HeroCarousel
            stories={carouselStories}
            onSelectStory={openArticle}
            isBookmarked={isArticleBookmarked}
            onToggleBookmark={toggleBookmark}
            autoPlayInterval={6500}
          />
        )}

        {/* Category Filter Pills Bar */}
        {!isForum && (
          <CategoryTabs
            categories={categories}
            activeCategory={activeCategory}
            onSelectCategory={(cat) => {
              setActiveCategory(cat);
              setSelectedSource('All');
            }}
            bookmarkCount={bookmarks.length}
          />
        )}

        {/* Views: Top Videos vs News Videos vs Forum vs Articles */}
        {isTopVideos ? (
          <Suspense fallback={<div className="skeleton app-container" style={{ minHeight: 350, margin: '2rem auto', borderRadius: 16 }} />}>
            <VideoSection
              title="Top Trending YouTube News"
              subtitle="Viral reports, breaking updates, and high-velocity investigative videos trending across YouTube Nigeria"
              type="trending"
              videos={topVideos}
              loading={loadingVideos}
            />
          </Suspense>
        ) : isNewsVideos ? (
          <Suspense fallback={<div className="skeleton app-container" style={{ minHeight: 350, margin: '2rem auto', borderRadius: 16 }} />}>
            <VideoSection
              title="Broadcast News Videos & Live Bulletins"
              subtitle="Curated broadcast dispatches and newsroom packages from Channels TV, TVC News, Arise News, and TheCable"
              type="news"
              videos={newsVideos}
              loading={loadingVideos}
            />
          </Suspense>
        ) : isForum ? (
          <div className="app-container">
            <ForumFeed
              onSelectStory={openArticle}
              stories={allStories}
            />
          </div>
        ) : (
          <>
            {/* 4. Latest News Section with Clean Grid */}
            <div className="latest-news-section app-container">
              <div className="latest-news-header">
                <div>
                  <h2 className="latest-news-title">
                    {activeCategory === 'Saved'
                      ? 'Saved Stories'
                      : activeCategory === 'All'
                      ? 'Latest National Stories'
                      : `${activeCategory} Stories`}
                  </h2>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
                  {/* Source Filter Dropdown */}
                  {activeCategory !== 'Saved' && sources.length > 0 && (
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <Filter size={14} style={{ color: 'var(--text-muted)' }} />
                      <select
                        id="source-filter-select"
                        value={selectedSource}
                        onChange={(e) => setSelectedSource(e.target.value)}
                        style={{
                          padding: '4px 10px',
                          borderRadius: 'var(--radius-pill)',
                          background: 'var(--bg-elevated)',
                          border: '1px solid var(--border-subtle)',
                          color: 'var(--text-primary)',
                          fontSize: '0.8rem',
                          cursor: 'pointer',
                        }}
                        aria-label="Filter stories by source"
                      >
                        <option value="All">All Sources</option>
                        {sources.map((src) => (
                          <option key={src} value={src}>
                            {src}
                          </option>
                        ))}
                      </select>
                    </div>
                  )}

                  {activeCategory !== 'Saved' && (
                    <button
                      type="button"
                      className="latest-news-see-all"
                      onClick={() => {
                        setActiveCategory('All');
                        setSelectedSource('All');
                      }}
                    >
                      See all <ArrowRight size={16} />
                    </button>
                  )}
                </div>
              </div>

              {/* Empty Category Notice (Honest empty state, no substituted foreign stories) */}
              {activeCategory !== 'Saved' && displayedArticles.length === 0 && !loading && (
                <div
                  style={{
                    padding: '60px 20px',
                    textAlign: 'center',
                    background: 'var(--bg-card)',
                    borderRadius: 'var(--radius-md)',
                    border: '1px solid var(--border-card)',
                    margin: '24px 0 48px 0',
                  }}
                >
                  <h3 style={{ fontSize: '1.2rem', marginBottom: 8 }}>
                    No {activeCategory} stories currently in the feed
                  </h3>
                  <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem', maxWidth: 420, margin: '0 auto 20px auto' }}>
                    New articles are continuously aggregated from national newsrooms. Try browsing All Stories or select another category.
                  </p>
                  <button
                    type="button"
                    className="primary-btn"
                    onClick={() => {
                      setActiveCategory('All');
                      setSelectedSource('All');
                    }}
                  >
                    Browse All Stories
                  </button>
                </div>
              )}

              {/* Empty Saved Stories Notice */}
              {activeCategory === 'Saved' && bookmarks.length === 0 && (
                <div
                  style={{
                    padding: '80px 20px',
                    textAlign: 'center',
                    background: 'var(--bg-card)',
                    borderRadius: 'var(--radius-md)',
                    border: '1px solid var(--border-card)',
                    margin: '32px 0 60px 0',
                  }}
                >
                  <BookmarkCheck size={48} style={{ color: 'var(--text-muted)', marginBottom: 16 }} />
                  <h3 style={{ fontSize: '1.25rem', marginBottom: 8 }}>No saved stories yet</h3>
                  <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem', maxWidth: 400, margin: '0 auto 20px auto' }}>
                    Click the bookmark icon on any article card to save stories for offline reading.
                  </p>
                  <button
                    type="button"
                    className="primary-btn"
                    onClick={() => setActiveCategory('All')}
                  >
                    Browse Latest News
                  </button>
                </div>
              )}

              {/* 4-Column Cards Grid with Placement 1 Sponsored Insertion */}
              {gridStoriesWithSponsor.length > 0 && (
                <div className="buletin-cards-grid">
                  {gridStoriesWithSponsor.map((item, index) => (
                    <React.Fragment key={item.id}>
                      <ArticleCard
                        article={item}
                        onSelectStory={openArticle}
                        isBookmarked={isArticleBookmarked(item.id)}
                        onToggleBookmark={toggleBookmark}
                      />
                      {!isInstalled && isInstallable && index === 3 && activeCategory !== 'Saved' && (
                        <PwaInstallFeedCard onInstall={promptInstall} />
                      )}
                    </React.Fragment>
                  ))}
                </div>
              )}

              {/* Load More Button */}
              {activeCategory !== 'Saved' && hasMore && !loading && articles.length > 0 && (
                <div style={{ display: 'flex', justifyContent: 'center', margin: '40px 0 20px 0' }}>
                  <button
                    id="load-more-stories-button"
                    type="button"
                    className="primary-btn"
                    onClick={() => loadArticles(false, page + 1)}
                  >
                    <RefreshCw size={15} />
                    Load More Stories
                  </button>
                </div>
              )}
            </div>

            {/* Placement 2: Dedicated Partner Perspectives / Corporate Briefings Shelf (Positioned naturally below the primary news grid) */}
            {activeCategory === 'All' && selectedSource === 'All' && sponsoredArticles.length > 0 && (
              <PartnerPerspectives
                stories={sponsoredArticles}
                onSelectStory={openArticle}
              />
            )}

            {/* Video Stories Section (Featured on Home) */}
            {activeCategory === 'All' && videoStories.length > 0 && (
              <VideoReel videos={videoStories} />
            )}
          </>
        )}
      </main>

      {/* 5. Live Spotlight Search Modal (Ctrl+K) */}
      <SearchModal
        isOpen={isSearchOpen}
        onClose={() => setIsSearchOpen(false)}
        onSelectStory={openArticle}
      />

      {/* Breaking News Push Notification Modal */}
      <NotificationModal
        isOpen={isNotificationOpen}
        onClose={() => setIsNotificationOpen(false)}
      />

      {/* 6. Distraction-Free Reader Drawer with Placement 3 Contextual Sponsored Callout */}
      <ReaderDrawer
        article={selectedArticle}
        isOpen={Boolean(selectedArticle)}
        onClose={closeArticle}
        isBookmarked={selectedArticle ? isArticleBookmarked(selectedArticle.id) : false}
        onToggleBookmark={toggleBookmark}
        onSelectRelatedStory={openArticle}
        currentUser={verifiedUser}
        initialScrollToComments={initialScrollToComments}
        onOpenAuth={() => setIsAuthOpen(true)}
        featuredSponsoredStory={featuredSponsoredStory}
        onSelectStory={openArticle}
      />

      {/* 7. Reader Verification Dialog (Google OAuth & Email OTP) */}
      {isAuthOpen && (
        <Suspense fallback={null}>
          <AuthModal
            isOpen={isAuthOpen}
            onClose={() => setIsAuthOpen(false)}
            onSuccess={(user) => {
              setVerifiedUser(user);
              setIsAuthOpen(false);
            }}
            initialDisplayName={verifiedUser?.displayName || ''}
          />
        </Suspense>
      )}

      {/* 8. Footer */}
      <Footer
        onSelectCategory={(cat) => {
          setActiveCategory(cat);
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }}
      />

      {/* Floating PWA Quick Install Pill (Engaged users who haven't dismissed) */}
      {isInstallable && !isInstalled && !isDismissed && hasEngaged && (
        <PwaQuickPill onInstall={promptInstall} onDismiss={dismissPrompt} />
      )}

      {/* iOS Safari Step-by-Step Installation Modal */}
      <IosInstallModal
        isOpen={showIosModal}
        onClose={() => setShowIosModal(false)}
      />
    </div>
  );
}
