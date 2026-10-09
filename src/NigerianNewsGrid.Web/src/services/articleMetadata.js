/**
 * Article metadata utilities: Recency Tags and Hot Discussion Detection.
 */

/**
 * Computes recency tag based on publication timestamp.
 * - < 45m: Breaking
 * - < 3h: Just In
 * - < 24h: Today
 * @param {string|Date} publishedAt 
 * @returns {{ type: string, label: string, badgeClass: string } | null}
 */
export function getRecencyTag(publishedAt) {
  if (!publishedAt) return null;
  const date = new Date(publishedAt);
  if (isNaN(date.getTime())) return null;

  const now = new Date();
  const diffMs = now.getTime() - date.getTime();

  // Guard against invalid date or far future timestamp (> 5 minutes)
  if (diffMs < -300000) {
    return null;
  }

  const diffMins = Math.floor(diffMs / 60000);

  // Breaking only for stories within the last 45 minutes
  if (diffMins <= 45 && diffMins >= -5) {
    return {
      type: 'breaking',
      label: '⚡ Breaking',
      badgeClass: 'recency-breaking',
    };
  }

  // Just In for stories between 45 mins and 3 hours
  if (diffMins <= 180 && diffMins > 45) {
    return {
      type: 'just-in',
      label: 'Just In',
      badgeClass: 'recency-just-in',
    };
  }

  // Today for stories between 3 hours and 24 hours
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours <= 24 && diffMins > 180) {
    return {
      type: 'today',
      label: 'Today',
      badgeClass: 'recency-today',
    };
  }

  return null;
}

/**
 * Checks if a story qualifies as a hot story (> 2 comments).
 * @param {Object} article 
 * @returns {boolean}
 */
export function isHotStory(article) {
  if (!article) return false;
  const count = Number(article.commentCount || 0);
  return count > 2;
}
