/**
 * XML and crawler utility functions for News Stand NG.
 * Enforces strict XML escaping, control character stripping, and response formatting.
 */

/**
 * Escapes reserved XML characters and strips invalid XML control characters.
 * @param {string|null|undefined} value
 * @returns {string} Escaped XML string
 */
export function escapeXml(value) {
  if (value === null || value === undefined) return '';
  const str = String(value);

  return str
    // Strip invalid XML control chars: 0x00-0x08, 0x0B-0x0C, 0x0E-0x1F
    .replace(/[\x00-\x08\x0B\x0C\x0E-\x1F]/g, '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

/**
 * Formats a date into a valid W3C ISO-8601 string.
 * @param {string|Date|null|undefined} dateInput
 * @returns {string} ISO-8601 timestamp string
 */
export function toIso8601(dateInput) {
  if (!dateInput) return new Date().toISOString();
  try {
    const d = new Date(dateInput);
    return isNaN(d.getTime()) ? new Date().toISOString() : d.toISOString();
  } catch {
    return new Date().toISOString();
  }
}

/**
 * Creates an HTTP Response with Content-Type application/xml and Cache-Control headers.
 * @param {string} xmlContent
 * @param {number} maxAgeSeconds (default 300 = 5 minutes)
 * @returns {Response}
 */
export function xmlResponse(xmlContent, maxAgeSeconds = 300) {
  return new Response(xmlContent, {
    status: 200,
    headers: {
      'Content-Type': 'application/xml; charset=utf-8',
      'Cache-Control': `public, max-age=${maxAgeSeconds}, s-maxage=${maxAgeSeconds}`,
      'X-Content-Type-Options': 'nosniff',
    },
  });
}

/**
 * Creates an HTTP Response with Content-Type text/plain and Cache-Control headers.
 * @param {string} textContent
 * @param {number} maxAgeSeconds (default 300 = 5 minutes)
 * @returns {Response}
 */
export function textResponse(textContent, maxAgeSeconds = 300) {
  return new Response(textContent, {
    status: 200,
    headers: {
      'Content-Type': 'text/plain; charset=utf-8',
      'Cache-Control': `public, max-age=${maxAgeSeconds}, s-maxage=${maxAgeSeconds}`,
    },
  });
}

/**
 * Resolves the absolute public site URL from environment or fallback default.
 * @returns {string} e.g. https://newsstand.ng
 */
export function getSiteUrl() {
  if (typeof process !== 'undefined') {
    const envUrl = process.env.SITE_URL || process.env.PUBLIC_URL || process.env.VITE_SITE_URL;
    if (envUrl) return envUrl.replace(/\/$/, '');
  }
  return 'https://newsstand.ng';
}
