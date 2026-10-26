import React, { useState, useEffect, useCallback, useMemo, useRef, Suspense } from 'react';
import Navbar from './components/Navbar';
import WelcomeBanner from './components/WelcomeBanner';
import BreakingTicker from './components/BreakingTicker';
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
import TrendingStoriesSection from './components/TrendingStoriesSection';
import HeroSpotlightBanner from './components/HeroSpotlightBanner';

const VideoSection = React.lazy(() => import('./components/VideoSection'));
const AuthModal = React.lazy(() => import('./components/AuthModal'));
import { api } from './services/api';
import { getVerifiedUser } from './services/forumUser';
import { getRecencyTag } from './services/articleMetadata';
import { trackPageView, trackCategorySelect } from './services/analytics';
import { RefreshCw, BookmarkCheck, ArrowRight, Filter, AlertTriangle, Radio } from 'lucide-react';

function getUrlForCategory(category) {
  if (!category || category === 'All') return '/';
  if (category === 'Forum' || category === 'Discussions') return '/?tab=forum';
  if (category === 'Saved') return '/?tab=saved';
  if (category === 'Top Videos' || category === 'Top Video') return '/?tab=top-videos';
  if (category === 'News Videos' || category === 'News Video') return '/?tab=news-videos';
  return `/category/${encodeURIComponent(category.toLowerCase())}`;
}

function resolveCategoryFromUrl(pathname, search, fallbackCategory = 'All') {
  const params = new URLSearchParams(search || '');
  const tab = (params.get('tab') || '').toLowerCase();
  if (tab === 'forum' || tab === 'discussions') return 'Forum';
  if (tab === 'saved' || tab === 'bookmarks') return 'Saved';
  if (tab === 'top-videos') return 'Top Videos';
  if (tab === 'news-videos') return 'News Videos';

  const catMatch = (pathname || '').match(/^\/category\/([^/?#]+)/i);
  if (catMatch && catMatch[1]) {
    const raw = decodeURIComponent(catMatch[1]).trim();
    if (raw) {
      return raw.charAt(0).toUpperCase() + raw.slice(1).toLowerCase();
    }
  }
  return fallbackCategory || 'All';
}

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
  const [trendingStories, setTrendingStories] = useState(() => initialData?.initialTrendingStories || []);
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
  const previousUrlRef = useRef(getUrlForCategory(initialCategory || 'All'));

  // Select Category or View Tab and synchronize with browser URL & History
  const handleSelectCategory = useCallback((cat, options = {}) => {
    const nextCat = cat || 'All';
    setActiveCategory(nextCat);
    setSelectedSource('All');
    setSelectedArticle(null);

    const targetUrl = getUrlForCategory(nextCat);
    previousUrlRef.current = targetUrl;

    if (typeof window !== 'undefined') {
      const currentUrl = window.location.pathname + window.location.search;
      if (currentUrl !== targetUrl) {
        window.history.pushState({ category: nextCat }, '', targetUrl);
      }
      trackCategorySelect(nextCat);
      trackPageView(
        targetUrl,
        nextCat === 'All' ? 'News Stand NG — Real-Time Nigerian News' : `${nextCat} News — News Stand NG`
      );
      if (options.scroll) {
        window.scrollTo({ top: 0, behavior: 'smooth' });
      }
    }
  }, []);

  // Open & Close Article with Canonical /news/{slug} Browser History (Back-button, SEO & deep-link support)
  const openArticle = useCallback((story, options = {}) => {
    if (!story) return;
    recordEngagement();

    if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/news/')) {
      const cleanUrl = new URL(window.location.href);
      cleanUrl.searchParams.delete('story');
      previousUrlRef.current = cleanUrl.pathname + (cleanUrl.search ? cleanUrl.search : '');
    }

    setSelectedArticle(story);
    setInitialScrollToComments(Boolean(options?.scrollToComments));

    if (typeof window !== 'undefined') {
      if (story.slug) {
        const canonicalPath = `/news/${encodeURIComponent(story.slug)}`;
        if (window.location.pathname !== canonicalPath) {
          window.history.pushState(
            {
              readerDrawer: true,
              storyId: story.id,
              slug: story.slug,
              returnUrl: previousUrlRef.current,
            },
            '',
            canonicalPath
          );
        }
        trackPageView(canonicalPath, story.title);
      } else if (story.id) {
        const url = new URL(window.location.href);
        url.searchParams.set('story', story.id);
        window.history.pushState(
          {
            readerDrawer: true,
            storyId: story.id,
            returnUrl: previousUrlRef.current,
          },
          '',
          url.toString()
        );
      }
    }
  }, [recordEngagement]);

  const closeArticle = useCallback(() => {
    setSelectedArticle(null);
    if (typeof window === 'undefined') return;

    if (window.location.pathname.startsWith('/news/')) {
      const restoreUrl = previousUrlRef.current || getUrlForCategory(activeCategory);
      window.history.pushState({ category: activeCategory }, '', restoreUrl);
      return;
    }

    const url = new URL(window.location.href);
    if (url.searchParams.has('story')) {
      url.searchParams.delete('story');
      const cleanPath = url.pathname + (url.search ? url.search : '');
      window.history.pushState({ category: activeCategory }, '', cleanPath);
    }
  }, [activeCategory]);

  // Listen to popstate for browser back/forward navigation across articles and category hubs
  useEffect(() => {
    const handlePopState = (event) => {
      const pathname = window.location.pathname;
      const search = window.location.search;
      const params = new URLSearchParams(search);
      const storyId = params.get('story');

      if (pathname.startsWith('/news/')) {
        const slug = decodeURIComponent(pathname.slice('/news/'.length).replace(/\/$/, ''));
        const stateStoryId = event.state?.storyId;
        const found = articles.find(
          (a) => (slug && a.slug === slug) || (stateStoryId && String(a.id) === String(stateStoryId))
        );
        if (found) {
          setSelectedArticle(found);
        } else if (slug) {
          api.getArticleBySlug(slug)
            .then((story) => {
              if (story) setSelectedArticle(story);
            })
            .catch(() => {
              if (stateStoryId) {
                api.getArticleById(stateStoryId)
                  .then((story) => {
                    if (story) setSelectedArticle(story);
                  })
                  .catch(() => {});
              }
            });
        }
        return;
      }

      if (storyId) {
        const found = articles.find((a) => String(a.id) === String(storyId));
        if (found) {
          setSelectedArticle(found);
        } else {
          api.getArticleById(storyId)
            .then((story) => setSelectedArticle(story))
            .catch(() => {});
        }
        return;
      }

      setSelectedArticle(null);
      const resolvedCat =
        event.state?.category || resolveCategoryFromUrl(pathname, search, initialCategory || 'All');
      setActiveCategory(resolvedCat);
      previousUrlRef.current = pathname + search;
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, [articles, initialCategory]);

  // Initial Deep-Link & URL Tab Hydration on mount (e.g. ?story=123 or ?tab=forum or /category/politics)
  useEffect(() => {
    const pathname = window.location.pathname;
    const search = window.location.search;
    const resolvedCat = resolveCategoryFromUrl(pathname, search, initialCategory || 'All');
    if (resolvedCat !== activeCategory) {
      setActiveCategory(resolvedCat);
    }
    previousUrlRef.current = getUrlForCategory(resolvedCat);

    const params = new URLSearchParams(search);
    const initialStoryId = params.get('story');
    if (initialStoryId) {
      api.getArticleById(initialStoryId)
        .then((story) => {
          if (story) setSelectedArticle(story);
        })
        .catch(() => {});
    }
  }, []);

  // Synchronize state when route props change (e.g. Navigating between /category/politics and /category/business)
  useEffect(() => {
    if (initialCategory && initialCategory !== activeCategory) {
      setActiveCategory(initialCategory);
    }
    if (initialData?.initialArticles?.length) {
      setArticles(initialData.initialArticles);
    }
  }, [initialCategory, initialData]);

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

    if (trendingStories.length === 0) {
      api.getTrendingStories()
        .then((data) => {
          const list = Array.isArray(data?.stories) ? data.stories : (Array.isArray(data) ? data : []);
          if (list.length > 0) setTrendingStories(list);
        })
        .catch(() => {});
    }

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

  // Determine what list to display with immediate client-side category filtering
  const displayedArticles = useMemo(() => {
    if (activeCategory === 'Saved') {
      return bookmarks;
    }

    let list = articles;

    // If on home with no articles loaded yet, use authentic fallbacks
    if (list.length === 0 && activeCategory === 'All' && selectedSource === 'All') {
      list = AUTHENTIC_NIGERIAN_FALLBACKS;
    }

    // If a specific category is selected, ensure displayed articles strictly match that category
    if (activeCategory && activeCategory !== 'All' && list.length > 0) {
      const normalizedCat = activeCategory.trim().toLowerCase();
      const filtered = list.filter((a) => {
        const itemCat = (a.category || '').trim().toLowerCase();
        return (
          itemCat === normalizedCat ||
          (normalizedCat === 'sports' && itemCat === 'sport') ||
          (normalizedCat === 'sport' && itemCat === 'sports')
        );
      });
      // If we have matching articles in memory, use them immediately
      if (filtered.length > 0) {
        list = filtered;
      }
    }

    // Filter by source if selected
    if (selectedSource !== 'All' && list.length > 0) {
      list = list.filter(
        (a) => (a.source || '').toLowerCase().includes(selectedSource.toLowerCase())
      );
    }

    return list;
  }, [articles, activeCategory, selectedSource, bookmarks]);

  const allStories = useMemo(() => {
    if (activeCategory === 'All') {
      return articles.length > 0 ? articles : AUTHENTIC_NIGERIAN_FALLBACKS;
    }
    return displayedArticles;
  }, [articles, displayedArticles, activeCategory]);

  // Hero Carousel Stories: Curate up to 6 top verified, visually compelling, source-diverse stories
  const carouselStories = useMemo(() => {
    if (!allStories || allStories.length === 0) return [];

    const candidates = [...allStories];
    const selected = [];
    const usedSources = new Map();

    // Pass 1: Prioritize hard breaking news (not pure opinion) with valid images and diverse sources
    for (const story of candidates) {
      if (selected.length >= 6) break;
      const isOpinion = story.contentType === 'Opinion' || (story.category || '').toLowerCase() === 'opinion';
      if (isOpinion) continue;

      const src = (story.source || 'Unknown').trim().toLowerCase();
      const count = usedSources.get(src) || 0;
      if (count < 2) {
        selected.push(story);
        usedSources.set(src, count + 1);
      }
    }

    // Pass 2: If we still have slots (< 6), fill from remaining candidate stories
    if (selected.length < 6) {
      for (const story of candidates) {
        if (selected.length >= 6) break;
        if (!selected.some((s) => s.id === story.id)) {
          selected.push(story);
        }
      }
    }

    return selected.slice(0, 6);
  }, [allStories]);

  const heroStoryIds = useMemo(() => new Set(carouselStories.map((s) => s.id)), [carouselStories]);

  // Latest News Grid Stories (excluding items already prominently featured in the top hero carousel)
  const latestNewsStories = useMemo(() => {
    if (activeCategory === 'All' && selectedSource === 'All' && carouselStories.length > 0) {
      return displayedArticles.filter((s) => !heroStoryIds.has(s.id));
    }
    return displayedArticles;
  }, [displayedArticles, activeCategory, selectedSource, carouselStories, heroStoryIds]);

  // Ensure Trending Developing Stories always has items even when offline or before server clusters populate
  const effectiveTrendingStories = useMemo(() => {
    if (trendingStories && trendingStories.length > 0) {
      return trendingStories;
    }
    if (!allStories || allStories.length === 0) return [];

    const byCategory = new Map();
    for (const art of allStories) {
      const cat = (art.category || 'General').trim();
      if (!byCategory.has(cat)) byCategory.set(cat, []);
      byCategory.get(cat).push(art);
    }

    const fallback = [];
    for (const [cat, items] of byCategory.entries()) {
      if (fallback.length >= 6) break;
      const lead = items[0];
      const sources = Array.from(new Set(items.map((i) => i.source).filter(Boolean)));
      if (sources.length < 2) {
        sources.push('Verified Nigerian Desk');
      }
      const catSlug = cat.toLowerCase().replace(/\s+/g, '-');
      fallback.push({
        id: `trend-${catSlug}`,
        slug: `trend-${catSlug}`,
        title: lead.title,
        summary: lead.summary || lead.excerpt,
        imageUrl: lead.imageUrl,
        category: cat,
        articleCount: Math.max(2, items.length),
        sourceCount: Math.max(2, sources.length),
        sources,
        updatedAt: lead.publishedAt || new Date().toISOString(),
        leadArticle: lead
      });
    }
    return fallback;
  }, [trendingStories, allStories]);

  // Active Sponsored Feature (Partner Studio)
  const featuredSponsoredStory = sponsoredArticles.find((s) => s.isSponsored) || null;

  // Main Page - Zone 1: Top Hero / Masthead Spotlight Banner
  const heroSpotlightCampaign = useMemo(() => {
    return sponsoredArticles.find((s) => s.isSponsored && s.placementZone === 'MainPage_HeroSpotlight') || null;
  }, [sponsoredArticles]);

  // Main Page - Zone 2: Native In-Feed Stories
  const inFeedSponsoredStories = useMemo(() => {
    return sponsoredArticles.filter((s) => s.isSponsored && (s.placementZone === 'MainPage_InFeed' || !s.placementZone));
  }, [sponsoredArticles]);

  // Main Page - Zone 3: Partner Perspectives Shelf Stories
  const partnerShelfStories = useMemo(() => {
    return sponsoredArticles.filter((s) => s.isSponsored && s.placementZone === 'MainPage_PartnerShelf');
  }, [sponsoredArticles]);

  // In-Article Placements
  const inArticleHeaderSponsor = useMemo(() => {
    return sponsoredArticles.find((s) => s.isSponsored && s.placementZone === 'InArticle_Header') || null;
  }, [sponsoredArticles]);

  const inArticleMidSponsor = useMemo(() => {
    return sponsoredArticles.find((s) => s.isSponsored && s.placementZone === 'InArticle_MidContent') || null;
  }, [sponsoredArticles]);

  const inArticleFooterSponsor = useMemo(() => {
    return sponsoredArticles.find((s) => s.isSponsored && s.placementZone === 'InArticle_Footer') || null;
  }, [sponsoredArticles]);

  // Placement: Native In-Feed Insertion (Configurable slots)
  const gridStoriesWithSponsor = useMemo(() => {
    if (activeCategory === 'Saved') return latestNewsStories;
    let list = [...latestNewsStories];
    const storiesToInsert = inFeedSponsoredStories.length > 0
      ? inFeedSponsoredStories
      : (featuredSponsoredStory ? [featuredSponsoredStory] : []);

    for (const sponsor of storiesToInsert) {
      if (list.some((s) => s.id === sponsor.id)) continue;
      const targetPos = sponsor.targetPosition && sponsor.targetPosition > 0
        ? sponsor.targetPosition - 1
        : 2;
      const insertIdx = Math.min(Math.max(0, targetPos), list.length);
      list.splice(insertIdx, 0, sponsor);
    }
    return list;
  }, [latestNewsStories, inFeedSponsoredStories, featuredSponsoredStory, activeCategory]);

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
        onSelectBookmarks={() => handleSelectCategory('Saved', { scroll: true })}
        activeCategory={activeCategory}
        onSelectNavTab={(tab) => {
          if (tab === 'Stories') handleSelectCategory('All', { scroll: true });
          else if (tab === 'Forum') handleSelectCategory('Forum', { scroll: true });
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

      {/* 2. Compact Editorial Masthead & Date Strip */}
      {activeCategory !== 'Saved' && !isForum && (
        <WelcomeBanner
          brandName="NEWS STAND NG"
          storyCount={articles.length}
          activeCategory={activeCategory}
        />
      )}

      {/* Main Content Area */}
      <main id="main-content" style={{ flex: 1 }}>
        {/* Main Page Zone 1: Top Hero / Masthead Spotlight Banner */}
        {activeCategory === 'All' && !isForum && heroSpotlightCampaign && (
          <HeroSpotlightBanner
            campaign={heroSpotlightCampaign}
            onSelectStory={openArticle}
          />
        )}

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

        {/* Real-time Multi-Source Trending Stories */}
        {activeCategory === 'All' && !isForum && !isTopVideos && !isNewsVideos && effectiveTrendingStories.length > 0 && (
          <TrendingStoriesSection
            stories={effectiveTrendingStories}
            onSelectStory={openArticle}
          />
        )}

        {/* Category Filter Pills Bar */}
        {!isForum && (
          <CategoryTabs
            categories={categories}
            activeCategory={activeCategory}
            onSelectCategory={(cat) => handleSelectCategory(cat)}
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
                      onClick={() => handleSelectCategory('All')}
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
                    onClick={() => handleSelectCategory('All')}
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
                    onClick={() => handleSelectCategory('All')}
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

            {/* Placement 2: Dedicated Partner Perspectives / Corporate Briefings Shelf */}
            {activeCategory === 'All' && selectedSource === 'All' && (partnerShelfStories.length > 0 || (sponsoredArticles.length > 0 && partnerShelfStories.length === 0)) && (
              <PartnerPerspectives
                stories={partnerShelfStories.length > 0 ? partnerShelfStories : sponsoredArticles}
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

      {/* 6. Distraction-Free Reader Drawer with Placement Contextual Sponsored Controls */}
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
        headerSponsor={inArticleHeaderSponsor}
        midContentSponsor={inArticleMidSponsor}
        footerSponsor={inArticleFooterSponsor || featuredSponsoredStory}
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
        onSelectCategory={(cat) => handleSelectCategory(cat, { scroll: true })}
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
