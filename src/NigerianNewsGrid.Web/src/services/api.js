import { trackPwaAnalytics } from './analytics';

export function getApiBaseUrl() {
  if (typeof window !== 'undefined') {
    return import.meta.env?.VITE_API_BASE || '/api/v1';
  }
  const serverBackend = (typeof process !== 'undefined' && (process.env?.API_BASE_URL || process.env?.NEWS_API_URL || process.env?.VITE_API_URL)) || 'http://127.0.0.1:56193';
  return `${serverBackend.replace(/\/$/, '')}/api/v1`;
}

const API_BASE = '/api/v1';

/**
 * Robust fetch wrapper with JSON parsing and error handling for both browser and SSR Node runtime.
 */
async function fetchJson(url, options = {}) {
  let targetUrl = url;
  if (typeof window === 'undefined' && targetUrl.startsWith('/')) {
    const serverBackend = (typeof process !== 'undefined' && (process.env?.API_BASE_URL || process.env?.NEWS_API_URL || process.env?.VITE_API_URL)) || 'http://127.0.0.1:56193';
    targetUrl = `${serverBackend.replace(/\/$/, '')}${targetUrl}`;
  }

  try {
    const { headers, ...restOptions } = options;
    const res = await fetch(targetUrl, {
      headers: {
        'Accept': 'application/json',
        ...headers,
      },
      ...restOptions,
    });

    if (!res.ok) {
      const errorText = await res.text().catch(() => '');
      throw new Error(`HTTP ${res.status}: ${errorText || res.statusText}`);
    }

    return await res.json();
  } catch (err) {
    console.warn(`[API] Request failed for ${targetUrl}:`, err.message);
    throw err;
  }
}

export const DEFAULT_EDITORIAL_IMAGE = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=800&q=80';

/**
 * Generates an optimized WebP image URL via the NewsApi image proxy.
 * Falls back to raw URL if already local, data URI, or already proxied.
 */
export function getOptimizedImageUrl(rawUrl, width = 800) {
  if (!rawUrl) return DEFAULT_EDITORIAL_IMAGE;
  if (rawUrl.startsWith('/') || rawUrl.startsWith('data:')) return rawUrl;
  if (rawUrl.includes('/img?url=')) return rawUrl;
  const base = getApiBaseUrl();
  return `${base}/img?url=${encodeURIComponent(rawUrl)}&w=${width}`;
}

export const api = {
  /**
   * Fetch paginated articles with filtering.
   */
  async getArticles({
    page = 1,
    pageSize = 24,
    category = '',
    source = '',
    search = '',
    contentType = '',
    todayOnly = false,
    days = null,
  } = {}) {
    const base = getApiBaseUrl();
    const params = new URLSearchParams();
    if (page) params.append('page', page);
    if (pageSize) params.append('pageSize', pageSize);
    if (category && category !== 'All') params.append('category', category);
    if (source && source !== 'All') params.append('source', source);
    if (search && search.trim()) params.append('search', search.trim());
    if (contentType) params.append('contentType', contentType);
    if (todayOnly) params.append('todayOnly', 'true');
    if (days) params.append('days', days);

    return fetchJson(`${base}/articles?${params.toString()}`);
  },

  /**
   * Fetch single article details by ID.
   */
  async getArticleById(id) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/articles/${encodeURIComponent(id)}`);
  },

  /**
   * Fetch article details by slug (Public SEO endpoint).
   */
  async getArticleBySlug(slug) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/articles/by-slug/${encodeURIComponent(slug)}`);
  },

  /**
   * Generates or fetches the AI Executive Briefing summary of an article.
   */
  async generateAiSummary(idOrSlug) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/articles/${encodeURIComponent(idOrSlug)}/ai-summary`, {
      method: 'POST'
    });
  },

  /**
   * Fetches the AI/ML enrichment data for an article (key takeaways, full summary, timeline, FAQs).
   */
  async getArticleEnrichment(idOrSlug) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/articles/${encodeURIComponent(idOrSlug)}/enrichment`);
  },


  /**
   * Fetch topic metadata by slug.
   */
  async getTopicBySlug(slug) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/topics/${encodeURIComponent(slug)}`);
  },

  /**
   * Fetch articles for a topic by slug.
   */
  async getTopicArticles(slug, { page = 1, pageSize = 20 } = {}) {
    const base = getApiBaseUrl();
    const params = new URLSearchParams();
    if (page) params.append('page', page);
    if (pageSize) params.append('pageSize', pageSize);
    return fetchJson(`${base}/topics/${encodeURIComponent(slug)}/articles?${params.toString()}`);
  },

  /**
   * Fetch stories for an election party or candidate.
   * Leverages topic articles if available, with resilient politics search fallback.
   */
  async getElectionStories({ topicSlug, searchKeyword, page = 1, pageSize = 12 } = {}) {
    const base = getApiBaseUrl();
    if (topicSlug) {
      try {
        const topicRes = await fetchJson(`${base}/topics/${encodeURIComponent(topicSlug)}/articles?page=${page}&pageSize=${pageSize}`);
        if (topicRes && topicRes.articles && topicRes.articles.length > 0) {
          return topicRes;
        }
      } catch {
        // Fallback to keyword search in Politics category
      }
    }

    const search = searchKeyword || (topicSlug ? topicSlug.replace(/-/g, ' ') : 'election');
    return this.getArticles({
      search,
      category: 'Politics',
      page,
      pageSize
    });
  },

  /**
   * Fetch active story clusters.
   */
  async getStories({ page = 1, pageSize = 20 } = {}) {
    const base = getApiBaseUrl();
    const params = new URLSearchParams();
    if (page) params.append('page', page);
    if (pageSize) params.append('pageSize', pageSize);
    return fetchJson(`${base}/stories?${params.toString()}`);
  },

  /**
   * Fetch a single story cluster by slug.
   */
  async getStoryBySlug(slug) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/stories/${encodeURIComponent(slug)}`);
  },

  /**
   * Fetch real-time trending story clusters (multi-source events).
   */
  async getTrendingStories({ limit = 6, windowHours = 24 } = {}) {
    const base = getApiBaseUrl();
    const params = new URLSearchParams();
    if (limit) params.append('limit', limit);
    if (windowHours) params.append('windowHours', windowHours);
    return fetchJson(`${base}/stories/trending?${params.toString()}`).catch(() => ({
      totalCount: 0,
      stories: []
    }));
  },

  /**
   * Fetch paginated active multi-source story clusters.
   */
  async getStories({ page = 1, pageSize = 20 } = {}) {
    const base = getApiBaseUrl();
    const params = new URLSearchParams({ page, pageSize });
    return fetchJson(`${base}/stories?${params.toString()}`).catch(() => ({
      page: 1,
      pageSize: 20,
      totalCount: 0,
      stories: []
    }));
  },

  /**
   * Fetch summary of all evergreen data trackers (Phase 8).
   */
  async getMarketMetrics() {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/data/metrics`).catch(() => []);
  },

  /**
   * Fetch a single data tracker by key or alias (e.g. naira-fx, fuel-prices, jamb-waec, elections).
   */
  async getMarketMetric(key) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/data/${encodeURIComponent(key)}`);
  },

  /**
   * Fetch all 36 states + FCT grouped by geopolitical zone (Phase 8).
   */
  async getStates() {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/states`).catch(() => []);
  },

  /**
   * Fetch a state by slug (e.g. lagos, kano, fct, rivers) with latest articles and FAQs.
   */
  async getState(slug) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/states/${encodeURIComponent(slug)}`);
  },

  /**
   * Fetch standardized SEO sitemap entries for crawlers.
   * @param {string} type - news | articles | topics | categories | sources
   * @param {number} page
   * @param {number} pageSize
   */
  async getSitemapEntries(type = 'news', page = 1, pageSize = 1000) {
    const base = getApiBaseUrl();
    const params = new URLSearchParams({
      type,
      page: String(page),
      pageSize: String(pageSize),
    });
    return fetchJson(`${base}/seo/sitemap-entries?${params.toString()}`);
  },

  /**
   * Fetch IndexNow configuration from backend.
   */
  async getIndexNowKey() {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/seo/indexnow-key`);
  },

  /**
   * Fetch related stories for an article.
   */
  async getRelatedStories(id, title, category, limit = 4) {
    const params = new URLSearchParams();
    if (id) params.append('id', id);
    if (title) params.append('title', title);
    if (category) params.append('category', category);
    params.append('limit', limit);

    return fetchJson(`${API_BASE}/articles/related?${params.toString()}`).catch(() => []);
  },

  /**
   * Fetch breaking news / top stories for live ticker.
   */
  async getBreakingNews() {
    try {
      const list = await this.getArticles({ pageSize: 6 });
      return Array.isArray(list) ? list : [];
    } catch {
      return [];
    }
  },

  /**
   * Fetch the latest neural audio briefing from NewsApi.
   */
  async getLatestAudioBriefing() {
    try {
      return await fetchJson(`${API_BASE}/audio/briefings/latest`);
    } catch {
      return null;
    }
  },

  /**
   * Fetch all categories.
   */
  async getCategories() {
    return ['Politics', 'Business', 'Technology', 'Sports', 'Entertainment', 'Crime', 'International', 'General', 'Opinion'];
  },

  /**
   * Fetch all news publishers / sources (names only for backwards compatibility).
   */
  async getSources() {
    try {
      const list = await fetchJson(`${getApiBaseUrl()}/sources`);
      if (Array.isArray(list) && list.length > 0) {
        return list.map((s) => (typeof s === 'string' ? s : s.name));
      }
    } catch {
      // Fallback
    }
    return ['Punch Newspaper', 'Premium Times', 'Vanguard News', 'Thisday', 'Business day', 'Channels Television', "Linda Ikeji's Blog", 'Arise Tv'];
  },

  /**
   * Fetch all news publishers / sources with rich metadata.
   */
  async getSourcesList() {
    try {
      const list = await fetchJson(`${getApiBaseUrl()}/sources`);
      if (Array.isArray(list) && list.length > 0) {
        return list;
      }
    } catch {
      // Fallback
    }
    return [
      { id: 'punch', name: 'Punch Newspaper', slug: 'punch', description: "The Punch is Nigeria's most widely read national daily newspaper.", websiteUrl: 'https://punchng.com' },
      { id: 'vanguard', name: 'Vanguard', slug: 'vanguard', description: 'Vanguard Newspapers is a leading Nigerian national daily established in 1984.', websiteUrl: 'https://www.vanguardngr.com' },
      { id: 'premiumtimes', name: 'Premium Times', slug: 'premiumtimes', description: 'Premium Times is a Pulitzer-recognized Nigerian investigative online newspaper.', websiteUrl: 'https://www.premiumtimesng.com' },
      { id: 'thecable', name: 'TheCable', slug: 'thecable', description: 'TheCable is an independent Nigerian digital newspaper committed to fast-breaking verified news.', websiteUrl: 'https://www.thecable.ng' },
      { id: 'dailypost', name: 'Daily Post Nigeria', slug: 'dailypost', description: 'Daily Post Nigeria is a premier digital news organization delivering breaking news.', websiteUrl: 'https://dailypost.ng' }
    ];
  },

  /**
   * Fetch single publisher profile by slug or ID.
   */
  async getSourceBySlug(slug) {
    const base = getApiBaseUrl();
    return fetchJson(`${base}/sources/${encodeURIComponent(slug)}`);
  },

  /**
   * Fetch latest news broadcast videos from monitored YouTube channels.
   * Matches MAUI app's NewsApiClient.GetVideoStoriesAsync -> /api/v1/video-stories
   */
  async getNewsVideos({ limit = 24, category = '' } = {}) {
    try {
      const params = new URLSearchParams();
      if (limit) params.append('limit', limit);
      if (category && category !== 'All') params.append('category', category);
      const data = await fetchJson(`${API_BASE}/video-stories?${params.toString()}`);
      if (Array.isArray(data) && data.length > 0) return data;
    } catch (err) {
      console.warn('[API] Could not fetch news video stories, using fallback:', err.message);
    }
    return FALLBACK_NEWS_VIDEOS;
  },

  /**
   * Fetch top trending YouTube news videos in Nigeria.
   * Matches MAUI app's NewsApiClient.GetTrendingVideoStoriesAsync -> /api/v1/video-stories/trending
   */
  async getTopVideos({ limit = 24, category = '' } = {}) {
    try {
      const params = new URLSearchParams();
      if (limit) params.append('limit', limit);
      if (category && category !== 'All') params.append('category', category);
      const data = await fetchJson(`${API_BASE}/video-stories/trending?${params.toString()}`);
      if (Array.isArray(data) && data.length > 0) return data;
    } catch (err) {
      console.warn('[API] Could not fetch trending video stories, using fallback:', err.message);
    }
    return FALLBACK_TOP_VIDEOS;
  },

  /**
   * Legacy alias for news videos.
   */
  async getVideoStories() {
    return this.getNewsVideos();
  },

  /**
   * Fetch comments for a story (hierarchical tree).
   */
  async getArticleComments(articleId, userId = '', sort = 'top') {
    const params = new URLSearchParams();
    if (userId) params.append('userId', userId);
    if (sort) params.append('sort', sort);
    return fetchJson(`${API_BASE}/articles/${encodeURIComponent(articleId)}/comments?${params.toString()}`)
      .catch(() => ({ articleId, totalCount: 0, comments: [] }));
  },

  /**
   * Post a new comment or reply.
   */
  async postComment(articleId, { authorName, content, parentCommentId = null, authorId = '', authorAvatar = '' }) {
    return fetchJson(`${API_BASE}/articles/${encodeURIComponent(articleId)}/comments`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ authorName, content, parentCommentId, authorId, authorAvatar }),
    });
  },

  /**
   * Vote on a comment (+1 upvote, -1 downvote, 0 remove).
   */
  async voteComment(commentId, userId, voteType) {
    return fetchJson(`${API_BASE}/comments/${encodeURIComponent(commentId)}/vote`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userId, voteType }),
    });
  },

  /**
   * Report an inappropriate or spam comment.
   */
  async reportComment(commentId, reportedBy = '', reason = 'Spam / Abuse') {
    return fetchJson(`${API_BASE}/comments/${encodeURIComponent(commentId)}/report`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reportedBy, reason }),
    });
  },

  /**
   * Fetch trending discussion stories for the Community Forum.
   */
  async getTrendingForum(limit = 20) {
    try {
      const list = await fetchJson(`${API_BASE}/forum/trending?limit=${limit}`);
      return Array.isArray(list) ? list : [];
    } catch {
      return [];
    }
  },

  /**
   * Fetch public authentication configuration.
   */
  async getAuthConfig() {
    return fetchJson(`${API_BASE}/auth/config`).catch(() => ({
      googleClientId: '434931027697-0ofh3nptkv409vju55od3fhrfbkccu15.apps.googleusercontent.com',
      enabledMethods: ['Google', 'Email'],
    }));
  },

  /**
   * Authenticate reader with Google ID Token.
   */
  async verifyGoogleToken(idToken, customDisplayName = null) {
    return fetchJson(`${API_BASE}/auth/google`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ idToken, customDisplayName }),
    });
  },

  /**
   * Request a 6-digit OTP code sent to the reader's email.
   */
  async requestEmailOtp(email, displayName = null) {
    return fetchJson(`${API_BASE}/auth/email/request-otp`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, displayName }),
    });
  },

  /**
   * Validate 6-digit email OTP and establish verified session.
   */
  async verifyEmailOtp(email, code, customDisplayName = null) {
    return fetchJson(`${API_BASE}/auth/email/verify-otp`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, code, customDisplayName }),
    });
  },

  /**
   * Track article click or engagement.
   */
  async trackEngagement(articleId, eventType = 'click') {
    try {
      await fetch(`${API_BASE}/analytics/track`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          articleId,
          eventType,
          timestampUtc: new Date().toISOString(),
        }),
      });
    } catch {
      // Non-blocking telemetry
    }
  },

  /**
   * Track PWA installation or app lifecycle event.
   */
  async trackPwaEvent(eventType = 'pwa_install', metadata = {}) {
    try {
      trackPwaAnalytics(eventType, metadata);

      let deviceId = '';
      if (typeof window !== 'undefined') {
        deviceId = localStorage.getItem('nng_pwa_device_id');
        if (!deviceId) {
          deviceId = 'pwa_' + Math.random().toString(36).substring(2, 10) + Date.now().toString(36);
          localStorage.setItem('nng_pwa_device_id', deviceId);
        }
      }

      await fetch(`${getApiBaseUrl()}/analytics/install`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          deviceId: deviceId || 'anonymous_web',
          eventType: eventType || 'pwa_install',
          articleTitle: metadata.source || 'PWA Web App',
          platform: metadata.platform || (typeof navigator !== 'undefined' ? navigator.platform || 'Web' : 'Web'),
          appVersion: 'PWA-1.0',
          occurredAt: new Date().toISOString(),
        }),
      });
    } catch {
      // Non-blocking telemetry
    }
  },

  /**
   * Fetch active direct sponsored campaigns from News API.
   */
  async getSponsoredArticles({ platform = 'Web', zone } = {}) {
    try {
      const params = new URLSearchParams();
      if (platform) params.append('platform', platform);
      if (zone) params.append('zone', zone);
      const queryStr = params.toString() ? `?${params.toString()}` : '';
      const res = await fetchJson(`${API_BASE}/articles/sponsored${queryStr}`);
      let list = [];
      if (Array.isArray(res)) list = res;
      else if (res && Array.isArray(res.value)) list = res.value;
      else if (res && Array.isArray(res.items)) list = res.items;

      const now = new Date();
      return list.filter((s) => s.isSponsored && (!s.campaignExpiresAt || new Date(s.campaignExpiresAt) > now));
    } catch {
      return [];
    }
  },

  /**
   * Track impression for a sponsored article (increments impression counter & emits telemetry).
   */
  async trackSponsoredImpression(articleId) {
    if (!articleId) return;
    try {
      await Promise.allSettled([
        fetch(`${API_BASE}/articles/${encodeURIComponent(articleId)}/track-impression`, {
          method: 'POST',
        }),
        this.trackEngagement(articleId, 'sponsored_impression'),
      ]);
    } catch {
      // Non-blocking telemetry
    }
  },

  /**
   * Track click for a sponsored article (increments click counter & emits telemetry).
   */
  async trackSponsoredClick(articleId) {
    if (!articleId) return;
    try {
      await Promise.allSettled([
        fetch(`${API_BASE}/articles/${encodeURIComponent(articleId)}/track-click`, {
          method: 'POST',
        }),
        this.trackEngagement(articleId, 'sponsored_click'),
      ]);
    } catch {
      // Non-blocking telemetry
    }
  },
};

export const FALLBACK_NEWS_VIDEOS = [
  {
    id: 'channels_vid_1',
    videoId: 'r8FW2pqYrq4',
    title: "WCQ Play-Off: 'It’s A Do Or Die Match', South Africa Coach On Nigeria Clash",
    summary: 'South Africa national team coach speaks ahead of the crucial world cup qualifying match against Super Eagles.',
    videoUrl: 'https://www.youtube.com/watch?v=r8FW2pqYrq4',
    thumbnailUrl: 'https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg',
    channelName: 'Channels Television',
    duration: 'LIVE / RECENT',
    category: 'Sports',
    publishedAt: new Date(Date.now() - 3600 * 1000).toISOString(),
    viewCount: '142K views',
  },
  {
    id: 'tvc_vid_2',
    videoId: '2nrElTSJE58',
    title: 'TVC News Headline News: National Economy, Markets & Policy Review',
    summary: 'Comprehensive news coverage and analysis of fiscal reforms and key governance developments across the federation.',
    videoUrl: 'https://www.youtube.com/watch?v=2nrElTSJE58',
    thumbnailUrl: 'https://i.ytimg.com/vi/2nrElTSJE58/hqdefault.jpg',
    channelName: 'TVC News Nigeria',
    duration: '12:10',
    category: 'News',
    publishedAt: new Date(Date.now() - 3 * 3600 * 1000).toISOString(),
    viewCount: '89K views',
  },
  {
    id: 'thecable_vid_3',
    videoId: 'hmjB9EQ66V8',
    title: 'TheCable Special Feature: Investigative Spotlight and Governance Insights',
    summary: 'In-depth investigative documentary exploring key policy implementation and socio-economic milestones.',
    videoUrl: 'https://www.youtube.com/watch?v=hmjB9EQ66V8',
    thumbnailUrl: 'https://i.ytimg.com/vi/hmjB9EQ66V8/hqdefault.jpg',
    channelName: 'TheCable',
    duration: '09:30',
    category: 'Politics',
    publishedAt: new Date(Date.now() - 5 * 3600 * 1000).toISOString(),
    viewCount: '64K views',
  },
  {
    id: 'sahara_vid_4',
    videoId: '7LA4EfXNXzc',
    title: 'SaharaTV Report: National Assembly and Legal Developments',
    summary: 'Coverage of recent legislative proceedings, public hearings, and civic accountability discussions.',
    videoUrl: 'https://www.youtube.com/watch?v=7LA4EfXNXzc',
    thumbnailUrl: 'https://i.ytimg.com/vi/7LA4EfXNXzc/hqdefault.jpg',
    channelName: 'SaharaTV',
    duration: '15:20',
    category: 'Politics',
    publishedAt: new Date(Date.now() - 7 * 3600 * 1000).toISOString(),
    viewCount: '115K views',
  },
  {
    id: 'arise_vid_5',
    videoId: 'j3W8y_0cO7o',
    title: 'ARISE News Prime Time: National Security, Fiscal Policy & Sub-national Growth',
    summary: 'Key analysts discuss national security architecture, foreign exchange volatility, and macroeconomic outlook.',
    videoUrl: 'https://www.youtube.com/watch?v=j3W8y_0cO7o',
    thumbnailUrl: 'https://i.ytimg.com/vi/j3W8y_0cO7o/hqdefault.jpg',
    channelName: 'ARISE TV',
    duration: '24:45',
    category: 'Business',
    publishedAt: new Date(Date.now() - 9 * 3600 * 1000).toISOString(),
    viewCount: '210K views',
  },
  {
    id: 'punch_vid_6',
    videoId: 'tQ0uB7kZJ3w',
    title: 'Punch Online Special: Infrastructure Boom & Coastal Highway Progress',
    summary: 'Field report examining engineering phases, coastal bridge constructions, and economic impact.',
    videoUrl: 'https://www.youtube.com/watch?v=tQ0uB7kZJ3w',
    thumbnailUrl: 'https://i.ytimg.com/vi/tQ0uB7kZJ3w/hqdefault.jpg',
    channelName: 'Punch Newspapers',
    duration: '08:15',
    category: 'Technology',
    publishedAt: new Date(Date.now() - 11 * 3600 * 1000).toISOString(),
    viewCount: '98K views',
  },
];

export const FALLBACK_TOP_VIDEOS = [
  {
    id: 'top_yt_1',
    videoId: '2nrElTSJE58',
    title: 'Trending #1: Central Bank Monetary Policy & Naira Stabilization Summit',
    summary: 'The Governors panel reviews inflation trajectory and consumer price index metrics in a live town hall.',
    videoUrl: 'https://www.youtube.com/watch?v=2nrElTSJE58',
    thumbnailUrl: 'https://i.ytimg.com/vi/2nrElTSJE58/hqdefault.jpg',
    channelName: 'TVC News Nigeria',
    duration: '18:40',
    category: 'Business',
    publishedAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
    viewCount: '340K views',
    isTrending: true,
    trendingRank: 1,
  },
  {
    id: 'top_yt_2',
    videoId: 'r8FW2pqYrq4',
    title: 'Trending #2: Super Eagles Camp Update: Tactical Breakdown & Press Conference',
    summary: 'Exclusive interview from the training pitch in Uyo ahead of the high-stakes continental qualifier.',
    videoUrl: 'https://www.youtube.com/watch?v=r8FW2pqYrq4',
    thumbnailUrl: 'https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg',
    channelName: 'Channels Television',
    duration: '14:22',
    category: 'Sports',
    publishedAt: new Date(Date.now() - 4 * 3600 * 1000).toISOString(),
    viewCount: '280K views',
    isTrending: true,
    trendingRank: 2,
  },
  {
    id: 'top_yt_3',
    videoId: 'j3W8y_0cO7o',
    title: 'Trending #3: Tech In Africa: Why Silicon Valley Venture Firms Are Doubling Down',
    summary: 'Roundtable discussion on payments rails, AI engineering hubs in Yaba, and foreign direct investment.',
    videoUrl: 'https://www.youtube.com/watch?v=j3W8y_0cO7o',
    thumbnailUrl: 'https://i.ytimg.com/vi/j3W8y_0cO7o/hqdefault.jpg',
    channelName: 'ARISE TV',
    duration: '22:15',
    category: 'Technology',
    publishedAt: new Date(Date.now() - 6 * 3600 * 1000).toISOString(),
    viewCount: '195K views',
    isTrending: true,
    trendingRank: 3,
  },
  {
    id: 'top_yt_4',
    videoId: 'hmjB9EQ66V8',
    title: 'Trending #4: Oil & Gas Output Rises as Deepwater Drilling Projects Come Online',
    summary: 'NNPC and international energy consortiums report production surge past 1.7 million barrels per day.',
    videoUrl: 'https://www.youtube.com/watch?v=hmjB9EQ66V8',
    thumbnailUrl: 'https://i.ytimg.com/vi/hmjB9EQ66V8/hqdefault.jpg',
    channelName: 'TheCable',
    duration: '11:05',
    category: 'Economy',
    publishedAt: new Date(Date.now() - 8 * 3600 * 1000).toISOString(),
    viewCount: '145K views',
    isTrending: true,
    trendingRank: 4,
  },
];
