const API_BASE = import.meta.env.VITE_API_BASE || '/api/v1';

/**
 * Robust fetch wrapper with JSON parsing and error handling.
 */
async function fetchJson(url, options = {}) {
  try {
    const res = await fetch(url, {
      headers: {
        'Accept': 'application/json',
        ...options.headers,
      },
      ...options,
    });

    if (!res.ok) {
      const errorText = await res.text().catch(() => '');
      throw new Error(`HTTP ${res.status}: ${errorText || res.statusText}`);
    }

    return await res.json();
  } catch (err) {
    console.warn(`[API] Request failed for ${url}:`, err.message);
    throw err;
  }
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
    const params = new URLSearchParams();
    if (page) params.append('page', page);
    if (pageSize) params.append('pageSize', pageSize);
    if (category && category !== 'All') params.append('category', category);
    if (source && source !== 'All') params.append('source', source);
    if (search && search.trim()) params.append('search', search.trim());
    if (contentType) params.append('contentType', contentType);
    if (todayOnly) params.append('todayOnly', 'true');
    if (days) params.append('days', days);

    return fetchJson(`${API_BASE}/articles?${params.toString()}`);
  },

  /**
   * Fetch single article details.
   */
  async getArticleById(id) {
    return fetchJson(`${API_BASE}/articles/${encodeURIComponent(id)}`);
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
   * Fetch all categories.
   */
  async getCategories() {
    return ['Politics', 'Business', 'Technology', 'Sports', 'Entertainment', 'Crime', 'General'];
  },

  /**
   * Fetch all news publishers / sources.
   */
  async getSources() {
    try {
      const list = await fetchJson(`${API_BASE}/sources`);
      if (Array.isArray(list) && list.length > 0) {
        return list.map((s) => (typeof s === 'string' ? s : s.name));
      }
    } catch {
      // Fallback
    }
    return ['Punch Newspaper', 'Premium Times', 'Vanguard News', 'Thisday', 'Business day', 'Channels Television', "Linda Ikeji's Blog", 'Arise Tv'];
  },

  /**
   * Fetch curated video stories.
   */
  async getVideoStories() {
    try {
      return await fetchJson(`${API_BASE}/videostories`);
    } catch {
      return [];
    }
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
};
