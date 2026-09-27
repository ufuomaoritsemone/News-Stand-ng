import React, { useState, useEffect, useCallback } from 'react';
import Navbar from './components/Navbar';
import WelcomeBanner from './components/WelcomeBanner';
import HeroCarousel from './components/HeroCarousel';
import ArticleCard from './components/ArticleCard';
import CategoryTabs from './components/CategoryTabs';
import SearchModal from './components/SearchModal';
import ReaderDrawer from './components/ReaderDrawer';
import VideoReel from './components/VideoReel';
import Footer from './components/Footer';
import { api } from './services/api';
import { RefreshCw, BookmarkCheck, ArrowRight, Filter } from 'lucide-react';

// Curated high-fidelity fallback stories matching the editorial screenshot
const FALLBACK_ARTICLES = [
  {
    id: 'story-john-wick',
    title: "Where To Watch 'John Wick: Chapter 4'",
    source: 'Netflix',
    publishedAt: new Date(Date.now() - 12 * 60 * 1000).toISOString(),
    category: 'Movies',
    summary:
      "There's been no official announcement regarding John Wick: Chapter 4's streaming release. However, given it's a Lionsgate film, John Wick: Chapter 4 will eventually be released on Starz, before expanding across major global streaming catalogs.",
    content:
      "There's been no official announcement regarding John Wick: Chapter 4's streaming release. However, given it's a Lionsgate film, John Wick: Chapter 4 will eventually be released on Starz, before arriving on other platforms.\n\nDirected by Chad Stahelski, the neo-noir action thriller follows Keanu Reeves as the legendary hitman on his globe-trotting mission to defeat the High Table. Featuring Donnie Yen, Bill Skarsgård, and Laurence Fishburne, the installment has garnered critical acclaim for its breathtaking cinematography, Paris setting, and groundbreaking stunt choreography.",
    imageUrl: 'https://images.unsplash.com/photo-1536440136628-849c177e76a1?auto=format&fit=crop&w=1200&q=80',
    author: 'Editorial Desk',
  },
  {
    id: 'story-verstappen-alonso',
    title: "'He deserves a lot more' Verstappen backs Alonso",
    source: 'Formula 1',
    publishedAt: new Date(Date.now() - 3 * 3600 * 1000).toISOString(),
    category: 'Sport',
    summary:
      "Max Verstappen believes his fellow two-time world champion Fernando Alonso 'deserves a lot more' victories in Formula 1 and has backed the Spaniard to triumph this season.",
    content:
      "Max Verstappen believes his fellow two-time world champion Fernando Alonso 'deserves a lot more' victories in Formula 1 and has backed the Spaniard to triumph this season.\n\nAlonso has scored podiums in several races this year following his winter switch to Aston Martin, displaying remarkable racecraft and speed. Verstappen commended Alonso's dedication, stating that Formula 1 is better when legendary racers battle at the sharp end.",
    imageUrl: 'https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b7?auto=format&fit=crop&w=800&q=80',
    author: 'Motorsport Desk',
  },
  {
    id: 'story-liverpool-leeds',
    title: 'Liverpool hammer Leeds for first win in five games',
    source: 'BBC',
    publishedAt: new Date(Date.now() - 12 * 3600 * 1000).toISOString(),
    category: 'Sport',
    summary:
      'Mohamed Salah and Diogo Jota both scored twice as Liverpool claimed a first league win in five games by inflicting a second successive home hammering on Leeds United.',
    content:
      'Mohamed Salah and Diogo Jota both scored twice as Liverpool claimed a first league win in five games by inflicting a second successive home hammering on Leeds United.\n\nThe Reds turned in an irresistible attacking display at Elland Road, showcasing clinical finishing and midfield dominance to reignite their European qualification hopes.',
    imageUrl: 'https://images.unsplash.com/photo-1508098682722-e99c43a406b2?auto=format&fit=crop&w=800&q=80',
    author: 'Football Correspondent',
  },
  {
    id: 'story-papua-pilot',
    title: 'Papua: At least one killed in hunt for kidnapped NZ pilot...',
    source: 'IDN Times',
    publishedAt: new Date(Date.now() - 24 * 3600 * 1000).toISOString(),
    category: 'Crime',
    summary:
      'At least one Indonesian soldier has been killed in a rebel attack while searching for a kidnapped New Zealand pilot in the Papua region, officials say.',
    content:
      'At least one Indonesian soldier has been killed in a rebel attack while searching for a kidnapped New Zealand pilot in the remote Papua region, military officials confirm.\n\nThe pilot was abducted after landing a small commercial plane in the mountainous Nduga regency. Search and rescue operations remain active despite the challenging terrain.',
    imageUrl: 'https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?auto=format&fit=crop&w=800&q=80',
    author: 'Asia-Pacific Bureau',
  },
  {
    id: 'story-jeremy-bowen',
    title: "Jeremy Bowen: Israel's unclear road ahead",
    source: 'BBC',
    publishedAt: new Date(Date.now() - 48 * 3600 * 1000).toISOString(),
    category: 'Middle East',
    summary:
      'Tensions between Israel and the Palestinians are on the rise once more, with hopes of peace and a two-state solution as far away as ever amid regional geopolitical shifts.',
    content:
      'Tensions between Israel and the Palestinians are on the rise once more, with hopes of peace and a two-state solution as far away as ever amid regional geopolitical shifts.\n\nInternational analysts emphasize that domestic political turbulence and security confrontations continue to reshape diplomatic expectations across neighboring capitals.',
    imageUrl: 'https://images.unsplash.com/photo-1518709268805-4e9042af9f23?auto=format&fit=crop&w=800&q=80',
    author: 'Jeremy Bowen',
  },
  {
    id: 'story-tech-lagos',
    title: 'Lagos Tech Hubs Secure $120M in Series A Funding Rounds',
    source: 'BusinessDay',
    publishedAt: new Date(Date.now() - 5 * 3600 * 1000).toISOString(),
    category: 'Business',
    summary:
      'African fintech and clean-tech startups headquartered in Lagos closed major venture capital rounds this quarter, demonstrating sustained investor appetite.',
    content:
      'African fintech and clean-tech startups headquartered in Lagos closed major venture capital rounds this quarter, demonstrating sustained investor appetite across global institutional markets.',
    imageUrl: 'https://images.unsplash.com/photo-1526304640581-d334cdbbf45e?auto=format&fit=crop&w=800&q=80',
    author: 'Finance Desk',
  },
  {
    id: 'story-nollywood-cinema',
    title: 'Nollywood Shatters Global Box Office Records with International Premieres',
    source: 'The Nation',
    publishedAt: new Date(Date.now() - 18 * 3600 * 1000).toISOString(),
    category: 'Entertainment',
    summary:
      'Nigerian cinematic productions achieve milestone ticket sales across London, Toronto, and Lagos theaters, marking a new golden era for West African cinema.',
    content:
      'Nigerian cinematic productions achieve milestone ticket sales across London, Toronto, and Lagos theaters, marking a new golden era for West African cinema.',
    imageUrl: 'https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=800&q=80',
    author: 'Arts & Culture',
  },
  {
    id: 'story-clean-energy',
    title: 'Nigeria Unveils Multi-Gigawatt Solar Grid Expansion in Northern States',
    source: 'Channels TV',
    publishedAt: new Date(Date.now() - 28 * 3600 * 1000).toISOString(),
    category: 'Technology',
    summary:
      'The Federal Ministry of Power commissions new high-capacity solar installations designed to provide uninterrupted clean electricity to industrial zones.',
    content:
      'The Federal Ministry of Power commissions new high-capacity solar installations designed to provide uninterrupted clean electricity to industrial zones.',
    imageUrl: 'https://images.unsplash.com/photo-1509391365360-2e959784a276?auto=format&fit=crop&w=800&q=80',
    author: 'Energy Reporter',
  },
];

export default function App() {
  // Theme State
  const [theme, setTheme] = useState(() => {
    return localStorage.getItem('nng_theme') || 'light';
  });

  // Data States
  const [articles, setArticles] = useState([]);
  const [videoStories, setVideoStories] = useState([]);
  const [categories, setCategories] = useState([]);
  const [activeCategory, setActiveCategory] = useState('All');
  const [selectedSource, setSelectedSource] = useState('All');
  const [sources, setSources] = useState([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);

  // Reader & Search Modal States
  const [selectedArticle, setSelectedArticle] = useState(null);
  const [isSearchOpen, setIsSearchOpen] = useState(false);

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

  // Fetch Initial Data
  useEffect(() => {
    api.getCategories()
      .then((data) => setCategories(data))
      .catch(() => {});

    api.getSources()
      .then((data) => setSources(data))
      .catch(() => {});

    api.getVideoStories()
      .then((data) => setVideoStories(Array.isArray(data) ? data : []))
      .catch(() => {});
  }, []);

  // Fetch Articles based on Category, Source, and Page
  const loadArticles = useCallback(async (reset = false, targetPage = 1) => {
    if (activeCategory === 'Saved' || activeCategory === 'Videos') {
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

      const list = Array.isArray(data) && data.length > 0 ? data : [];
      setArticles((prev) => (reset ? list : [...prev, ...list]));
      setHasMore(list.length >= 32);
      setPage(targetPage);
    } catch (err) {
      console.warn('Backend API connection notice:', err.message);
      if (reset) setArticles([]);
    } finally {
      setLoading(false);
    }
  }, [activeCategory, selectedSource]);

  useEffect(() => {
    loadArticles(true, 1);
  }, [loadArticles]);

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
    // If backend returns articles, use them; otherwise use our curated fallback stories
    displayedArticles = articles.length > 0 ? articles : FALLBACK_ARTICLES;

    // Filter by category if specific category selected
    if (activeCategory !== 'All') {
      displayedArticles = displayedArticles.filter(
        (a) => (a.category || '').toLowerCase() === activeCategory.toLowerCase()
      );
    }

    // Filter by source if selected
    if (selectedSource !== 'All') {
      displayedArticles = displayedArticles.filter(
        (a) => (a.source || '').toLowerCase() === selectedSource.toLowerCase()
      );
    }
  }

  // Hero Carousel Stories (10 most recent news stories)
  const carouselStories = (articles.length > 0 ? articles : FALLBACK_ARTICLES).slice(0, 10);

  // Latest News Grid Stories (show next stories or all grid stories)
  const latestNewsStories =
    activeCategory === 'All' && selectedSource === 'All' && displayedArticles.length > 10
      ? displayedArticles.slice(10)
      : displayedArticles;

  return (
    <div className="app-shell" style={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      {/* 1. Header & Navigation matching Buletin */}
      <Navbar
        theme={theme}
        toggleTheme={toggleTheme}
        onOpenSearch={() => setIsSearchOpen(true)}
        bookmarkCount={bookmarks.length}
        onSelectBookmarks={() => setActiveCategory('Saved')}
        activeCategory={activeCategory}
        onSelectNavTab={(tab) => {
          if (tab === 'Stories') setActiveCategory('All');
        }}
      />

      {/* 2. Welcome Banner: Craft narratives that ignite inspiration, knowledge, and entertainment */}
      <WelcomeBanner brandName="BULETIN" />

      {/* Main Content Area */}
      <main id="main-content" style={{ flex: 1 }}>
        {/* Videos View */}
        {activeCategory === 'Videos' ? (
          <VideoReel videos={videoStories} />
        ) : (
          <>
            {/* 3. Hero Section: Self-Scrolling Carousel of Latest Stories */}
            {activeCategory !== 'Saved' && (
              <HeroCarousel
                stories={carouselStories}
                onSelectStory={(story) => setSelectedArticle(story)}
                isBookmarked={isArticleBookmarked}
                onToggleBookmark={toggleBookmark}
                autoPlayInterval={5000}
              />
            )}

            {/* Category Filter Pills Bar */}
            <CategoryTabs
              categories={categories}
              activeCategory={activeCategory}
              onSelectCategory={(cat) => {
                setActiveCategory(cat);
              }}
              bookmarkCount={bookmarks.length}
            />

            {/* 4. Latest News Section with 4-Column Grid */}
            <div className="latest-news-section app-container">
              <div className="latest-news-header">
                <div>
                  <h2 className="latest-news-title">
                    {activeCategory === 'Saved'
                      ? 'Saved Stories'
                      : activeCategory === 'All'
                      ? 'Latest News'
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

              {/* 4-Column Cards Grid */}
              {latestNewsStories.length > 0 && (
                <div className="buletin-cards-grid">
                  {latestNewsStories.map((item) => (
                    <ArticleCard
                      key={item.id}
                      article={item}
                      onSelectStory={(story) => setSelectedArticle(story)}
                      isBookmarked={isArticleBookmarked(item.id)}
                      onToggleBookmark={toggleBookmark}
                    />
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
        onSelectStory={(story) => setSelectedArticle(story)}
      />

      {/* 6. Distraction-Free Reader Drawer */}
      <ReaderDrawer
        article={selectedArticle}
        isOpen={Boolean(selectedArticle)}
        onClose={() => setSelectedArticle(null)}
        isBookmarked={selectedArticle ? isArticleBookmarked(selectedArticle.id) : false}
        onToggleBookmark={toggleBookmark}
        onSelectRelatedStory={(story) => setSelectedArticle(story)}
      />

      {/* 7. Footer */}
      <Footer
        onSelectCategory={(cat) => {
          setActiveCategory(cat);
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }}
      />
    </div>
  );
}
