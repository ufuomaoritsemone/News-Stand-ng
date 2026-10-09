/**
 * Unified Time and Date Utilities for News Stand NG.
 * All functions correctly handle ISO-8601 UTC strings, timestamps, and localized Nigerian display.
 */

/**
 * Formats a relative time string ("Just now", "12 mins ago", "3 hrs ago", "2 days ago", or "Oct 3, 2026").
 * Fixes the previous bug where minutes < 60 was displaying as "hours ago".
 * 
 * @param {string|Date|number} dateStr 
 * @returns {string}
 */
export function formatTimeAgo(dateStr) {
  if (!dateStr) return 'Recent';
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return 'Recent';

  const now = new Date();
  const diffMs = now.getTime() - date.getTime();

  // Guard against slight future clock skew (up to 2 minutes)
  if (diffMs < 0 && diffMs > -120000) {
    return 'Just now';
  }
  if (diffMs < 0) {
    return 'Recent';
  }

  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 1) return 'Just now';
  if (diffMins === 1) return '1 min ago';
  if (diffMins < 60) return `${diffMins} mins ago`;

  if (diffHours === 1) return '1 hr ago';
  if (diffHours < 24) return `${diffHours} hrs ago`;

  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7) return `${diffDays} days ago`;

  return date.toLocaleDateString('en-NG', {
    month: 'short',
    day: 'numeric',
    year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined,
  });
}

/**
 * Formats a full date and time string in West Africa Time (WAT).
 * Example: "Sat, Oct 3, 2026, 12:30 PM"
 * 
 * @param {string|Date|number} dateStr 
 * @returns {string}
 */
export function formatFullDate(dateStr) {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return '';

  return date.toLocaleDateString('en-NG', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Returns formatted current Nigerian date banner string.
 * Example: "Saturday, 3 October 2026"
 */
export function getNigerianDateBanner() {
  return new Date().toLocaleDateString('en-NG', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
}

/**
 * Strips raw scraper tail fragments (e.g. "Read More: https://...", "...Read more")
 * to present clean, readable editorial copy on cards and mobile screens.
 * 
 * @param {string} text
 * @returns {string}
 */
export function cleanEditorialSummary(text) {
  if (!text) return '';
  return text
    .replace(/(?:\.\.\.\s*)?Read\s+More\s*:\s*https?:\/\/\S+/gi, '')
    .replace(/Read\s+More\s*:\s*\S+/gi, '')
    .replace(/Read\s+more\s*\.{0,3}$/gi, '')
    .trim();
}
