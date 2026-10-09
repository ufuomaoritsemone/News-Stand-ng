// Google Analytics 4 (GA4) Telemetry & Extra Analytics Service for News Stand NG

export const GA_MEASUREMENT_ID =
  (typeof import.meta !== 'undefined' && import.meta.env?.VITE_GA_MEASUREMENT_ID) ||
  'G-7GBS0F0WZQ';

/**
 * Safely dispatches a GA4 event if gtag is initialized on the window.
 *
 * @param {string} eventName
 * @param {Record<string, any>} [params={}]
 */
export function trackEvent(eventName, params = {}) {
  if (typeof window === 'undefined') return;

  if (typeof window.gtag === 'function') {
    window.gtag('event', eventName, {
      send_to: GA_MEASUREMENT_ID,
      ...params,
    });
  } else if (import.meta.env?.DEV) {
    console.debug(`[GA4 Event (mock)] ${eventName}:`, params);
  }
}

/**
 * Track client-side SPA route transitions (virtual page views).
 *
 * @param {string} pagePath
 * @param {string} [pageTitle]
 */
export function trackPageView(pagePath, pageTitle) {
  if (typeof window === 'undefined') return;

  trackEvent('page_view', {
    page_path: pagePath || window.location.pathname,
    page_title: pageTitle || document.title,
    page_location: window.location.href,
  });
}

/**
 * Extra Analytics: Track when a reader views a curated article.
 *
 * @param {object} article
 */
export function trackArticleView(article) {
  if (!article) return;
  trackEvent('article_view', {
    article_id: article.id || article.slug || '',
    article_title: article.title || '',
    publisher_name: article.source || article.sourceName || 'Unknown',
    category: article.category || 'General',
    content_type: 'news_report',
  });
}

/**
 * Extra Analytics: Track outbound referral clicks to primary news publishers
 * (e.g. Punch, Premium Times, Vanguard, TheCable, Channels TV).
 *
 * @param {object} article
 * @param {string} [destinationUrl]
 */
export function trackOutboundClick(article, destinationUrl) {
  const targetUrl = destinationUrl || article?.url || article?.sourceUrl || '';
  const publisher = article?.source || article?.sourceName || 'Publisher';

  trackEvent('outbound_click', {
    publisher_name: publisher,
    article_title: article?.title || '',
    article_id: article?.id || article?.slug || '',
    destination_url: targetUrl,
    outbound: true,
  });
}

/**
 * Extra Analytics: Track headline search queries.
 *
 * @param {string} query
 * @param {number} [resultCount=0]
 */
export function trackSearch(query, resultCount = 0) {
  if (!query || !query.trim()) return;
  trackEvent('search', {
    search_term: query.trim(),
    results_count: resultCount,
  });
}

/**
 * Extra Analytics: Track category and topic hub navigation.
 *
 * @param {string} category
 */
export function trackCategorySelect(category) {
  if (!category) return;
  trackEvent('select_content', {
    content_type: 'news_category',
    item_id: category.toLowerCase(),
    item_category: category,
  });
}

/**
 * Extra Analytics: Track social media sharing (WhatsApp, X, Native share).
 *
 * @param {string} method - 'whatsapp' | 'x_twitter' | 'clipboard' | 'native_share'
 * @param {string} articleTitle
 * @param {string|number} articleId
 */
export function trackArticleShare(method, articleTitle, articleId) {
  trackEvent('share', {
    method: method || 'web_share',
    content_type: 'article',
    item_id: String(articleId || ''),
    item_title: articleTitle || '',
  });
}

/**
 * Extra Analytics: Track article bookmarks / offline saves.
 *
 * @param {'add'|'remove'} action
 * @param {string|number} articleId
 * @param {string} [articleTitle]
 */
export function trackBookmark(action, articleId, articleTitle = '') {
  trackEvent('bookmark_toggle', {
    action, // 'add' or 'remove'
    item_id: String(articleId || ''),
    item_title: articleTitle,
  });
}

/**
 * Extra Analytics: Track neural audio briefings playback.
 *
 * @param {'play'|'pause'|'completed'} action
 * @param {string} [briefingTitle]
 */
export function trackAudioBriefing(action, briefingTitle = 'Daily News Briefing') {
  trackEvent('audio_briefing', {
    audio_action: action,
    briefing_title: briefingTitle,
  });
}

/**
 * Extra Analytics: Track PWA installations & launch adoption.
 *
 * @param {string} eventType - 'pwa_install' | 'pwa_open' | 'pwa_prompt_shown' | 'pwa_prompt_dismissed'
 * @param {Record<string, any>} [details={}]
 */
export function trackPwaAnalytics(eventType, details = {}) {
  trackEvent(eventType, {
    platform: details.platform || 'Web',
    source: details.source || 'browser',
    ...details,
  });
}
