import { trackArticleShare } from './analytics';

const PRODUCTION_SITE_URL = 'https://newsstand.ng';

/**
 * Resolves the canonical public URL for an article.
 * Always resolves to the public https://newsstand.ng domain for social scrapers
 * (WhatsApp, X, Facebook, LinkedIn, Telegram) so they can fetch OpenGraph image tags
 * even when testing on local dev or staging.
 */
export function getStoryUrl(article) {
  if (!article) return PRODUCTION_SITE_URL;
  if (article.slug) {
    return `${PRODUCTION_SITE_URL}/news/${article.slug}`;
  }
  if (article.title) {
    const slug = article.title
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '');
    return `${PRODUCTION_SITE_URL}/news/${slug}`;
  }
  if (article.id) {
    return `${PRODUCTION_SITE_URL}/news/${encodeURIComponent(article.id)}`;
  }
  return article.url || PRODUCTION_SITE_URL;
}

/**
 * Post an article directly to 𝕏 (Twitter)
 */
export function shareOnX(article) {
  if (!article) return;
  trackArticleShare('x_twitter', article.title, article.id || article.slug);
  const title = article.title || '';
  const storyUrl = getStoryUrl(article);
  const sourceAttribution = article.source ? ` (${article.source})` : '';
  const maxTitleLength = 160;
  const displayTitle = title.length > maxTitleLength ? `${title.slice(0, maxTitleLength - 3)}...` : title;

  const text = `📰 ${displayTitle}${sourceAttribution}\n\n⚡ Follow live Nigerian news on News Stand NG`;
  const targetUrl = `https://twitter.com/intent/tweet?text=${encodeURIComponent(text)}&url=${encodeURIComponent(storyUrl)}`;
  window.open(targetUrl, '_blank', 'noopener,noreferrer,width=600,height=480');
}

/**
 * Dedicated helper to share an article directly on WhatsApp.
 * Opens WhatsApp directly (mobile app or WhatsApp Web) without opening the generic OS share sheet.
 * Formats the headline, publisher source, and canonical public HTTPS link cleanly so WhatsApp's
 * scraper immediately unfurls and renders the high-resolution OpenGraph image preview card.
 */
export function shareOnWhatsApp(article) {
  if (!article) return;
  trackArticleShare('whatsapp', article.title, article.id || article.slug);
  const title = article.title || 'Breaking News';
  const storyUrl = getStoryUrl(article);
  const sourceAttribution = article.source ? ` | ${article.source}` : '';

  // WhatsApp's crawler parses the URL at the end of the text to display the rich image card
  const text = `📰 *${title}*${sourceAttribution}\n\n${storyUrl}`;
  const targetUrl = `https://api.whatsapp.com/send?text=${encodeURIComponent(text)}`;
  window.open(targetUrl, '_blank', 'noopener,noreferrer');
}

/**
 * Generic sharing for all other platforms / device share menu.
 * Uses Web Share API Level 2 (with image file attachment where supported) so users can
 * share the story WITH THE ACTUAL IMAGE to any installed app (WhatsApp, Telegram, Messages, etc.).
 * Falls back to clipboard copying if Web Share is not supported or declined.
 */
export async function shareGeneric(article) {
  if (!article) return { success: false };
  trackArticleShare('generic_share', article.title, article.id || article.slug);
  const title = article.title || 'Breaking News';
  const storyUrl = getStoryUrl(article);
  const sourceAttribution = article.source ? ` | ${article.source}` : '';

  // If native Web Share is supported, try attaching the image file
  if (typeof navigator !== 'undefined' && navigator.share) {
    let files = [];
    if (article.imageUrl && typeof navigator.canShare === 'function') {
      try {
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 2500);
        const res = await fetch(article.imageUrl, { mode: 'cors', signal: controller.signal });
        clearTimeout(timeout);
        if (res.ok) {
          const blob = await res.blob();
          const mime = blob.type && blob.type.startsWith('image/') ? blob.type : 'image/jpeg';
          const ext = mime.split('/')[1]?.replace('jpeg', 'jpg') || 'jpg';
          const file = new File([blob], `news-${article.slug || 'story'}.${ext}`, { type: mime });
          if (navigator.canShare({ files: [file] })) {
            files = [file];
          }
        }
      } catch {
        // Fall back gracefully to sharing title and url without file attachment
      }
    }

    try {
      const shareData = {
        title,
        text: `📰 ${title}${sourceAttribution}`,
        url: storyUrl,
      };
      if (files.length > 0) {
        shareData.files = files;
      }
      await navigator.share(shareData);
      return { success: true, method: 'native' };
    } catch (err) {
      if (err.name === 'AbortError') {
        return { success: false, aborted: true };
      }
      // Other error: proceed to clipboard fallback
    }
  }

  // Fallback: Copy link to clipboard
  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(storyUrl);
      return { success: true, method: 'clipboard' };
    } catch {
      try {
        const textArea = document.createElement('textarea');
        textArea.value = storyUrl;
        textArea.style.position = 'fixed';
        textArea.style.left = '-9999px';
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        document.execCommand('copy');
        document.body.removeChild(textArea);
        return { success: true, method: 'clipboard' };
      } catch {}
    }
  }

  return { success: false };
}


