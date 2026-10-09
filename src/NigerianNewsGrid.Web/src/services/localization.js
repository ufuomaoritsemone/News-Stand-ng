/**
 * News Stand NG — Localization & Multi-Language Dictionary Engine (Phase 8)
 * Supports English (en-NG) and Nigerian Pidgin (pcm), with future hooks for ha, yo, ig.
 */

export const SUPPORTED_LANGUAGES = [
  { code: 'en', name: 'English', label: 'English', flag: '🇳🇬' },
  { code: 'pcm', name: 'Naija Pidgin', label: 'Pidgin', flag: '🇳🇬' },
  { code: 'ha', name: 'Hausa', label: 'Hausa (Soon)', flag: '🇳🇬', disabled: true },
  { code: 'yo', name: 'Yorùbá', label: 'Yorùbá (Soon)', flag: '🇳🇬', disabled: true },
  { code: 'ig', name: 'Asụsụ Igbo', label: 'Igbo (Soon)', flag: '🇳🇬', disabled: true },
];

const DICTIONARY = {
  // Navigation & General UI
  'nav.top_stories': { en: 'Top Stories', pcm: 'Top Tori' },
  'nav.forum': { en: 'Forum', pcm: 'Gbeborun' },
  'nav.forum_badge': { en: 'Debates', pcm: 'Debate' },
  'nav.alerts': { en: 'Alerts', pcm: 'Alert' },
  'nav.saved': { en: 'Saved', pcm: 'Keep' },
  'nav.search_placeholder': { en: 'Search stories...', pcm: 'Find tori...' },
  'nav.states': { en: '36 States & FCT', pcm: '36 States & FCT' },
  'nav.market_data': { en: 'Data Trackers', pcm: 'Market Data' },
  'nav.sign_in': { en: 'Sign In', pcm: 'Log In' },

  // Data Trackers
  'tracker.naira_title': { en: 'Naira Exchange Rate (USD/NGN)', pcm: 'Naira FX Price Today (USD/NGN)' },
  'tracker.naira_badge': { en: 'Forex Monitor', pcm: 'Naira Watch' },
  'tracker.fuel_title': { en: 'PMS Petrol Pump Price in Nigeria', pcm: 'Fuel Price For Nigeria (PMS)' },
  'tracker.fuel_badge': { en: 'Energy Tracker', pcm: 'Petrol Price' },
  'tracker.jamb_title': { en: 'JAMB UTME & WAEC Exam Portal', pcm: 'JAMB & WAEC Exam Tori' },
  'tracker.jamb_badge': { en: 'Education News', pcm: 'School Exam' },
  'tracker.elections_title': { en: 'Nigeria Elections & INEC Timetable', pcm: 'Election & INEC Timetable' },
  'tracker.elections_badge': { en: 'Democracy Tracker', pcm: 'Election Watch' },

  // Detail Page Headers
  'common.last_updated': { en: 'Last Updated', pcm: 'Time Wey We Update Am' },
  'common.source_attribution': { en: 'Verified Data Sources', pcm: 'Where Di Data Come From' },
  'common.read_full_story': { en: 'Read Full Story', pcm: 'Read Complete Tori' },
  'common.back_to_headlines': { en: 'Headlines', pcm: 'Headlines' },
  'common.faq_title': { en: 'Frequently Asked Questions', pcm: 'Questions People Dey Ask Well Well' },
  'common.related_news': { en: 'Related Coverage & Updates', pcm: 'Other Tori Wey Connect' },
  'common.key_facts': { en: 'Key Facts', pcm: 'Important Facts' },

  // States Hub
  'states.title': { en: 'Nigeria State News Directory', pcm: 'Tori Across 36 States & FCT' },
  'states.subtitle': { en: 'Explore verified local news reporting, commercial developments, and governance across all 36 states and the Federal Capital Territory.', pcm: 'Read clean tori wey dey happen for each of di 36 states and Abuja FCT without any fake news.' },
  'states.all_zones': { en: 'All 6 Geopolitical Zones', pcm: 'All 6 Geopolitical Zones' },
  'states.capital': { en: 'Capital', pcm: 'State Capital' },
  'states.zone': { en: 'Zone', pcm: 'Zone' },
  'states.stories_count': { en: 'stories available', pcm: 'tori dey ground' },
  'states.view_state': { en: 'View State News', pcm: 'Look State Tori' },
};

/**
 * Translate a key into the target language with English fallback.
 */
export function t(key, lang = 'en') {
  const normLang = lang?.toLowerCase() === 'pcm' ? 'pcm' : 'en';
  const entry = DICTIONARY[key];
  if (!entry) return key;
  return entry[normLang] || entry['en'] || key;
}

/**
 * Generate reciprocal hreflang alternate link objects for standard SEO meta tags.
 */
export function getHreflangTags(canonicalPath) {
  // Strip existing /pcm prefix if present
  let cleanPath = canonicalPath.startsWith('/pcm') ? canonicalPath.slice(4) : canonicalPath;
  if (!cleanPath.startsWith('/')) cleanPath = `/${cleanPath}`;

  const baseUrl = 'https://newsstand.ng';

  return [
    { tagName: 'link', rel: 'alternate', hrefLang: 'en-ng', href: `${baseUrl}${cleanPath}` },
    { tagName: 'link', rel: 'alternate', hrefLang: 'pcm', href: `${baseUrl}/pcm${cleanPath}` },
    { tagName: 'link', rel: 'alternate', hrefLang: 'x-default', href: `${baseUrl}${cleanPath}` },
  ];
}
