import React from 'react';
import { useLoaderData, Link, redirect } from 'react-router';
import { api } from '../../src/services/api';
import { formatTimeAgo } from '../../src/services/timeUtils';
import { getHreflangTags } from '../../src/services/localization';
import Footer from '../../src/components/Footer';
import {
  MapPin,
  Building,
  HelpCircle,
  ChevronDown,
  ArrowLeft,
  Flame,
  Tag,
  Compass,
  ArrowRight
} from 'lucide-react';

export function meta({ data, location }) {
  if (!data?.state) {
    return [
      { title: 'State Not Found | News Stand NG' },
      { name: 'robots', content: 'noindex, nofollow' },
    ];
  }

  const { state } = data;
  const isPidgin = location?.pathname?.startsWith('/pcm');
  const title = isPidgin
    ? `${state.name} News Today: Latest Local Tori From ${state.capital} | News Stand NG`
    : `${state.name} State News Today: Latest Headlines & Local Developments | News Stand NG`;
  const description = isPidgin
    ? `Read verified local news from ${state.name} State (${state.capital}), commercial updates, and local community stories on News Stand NG.`
    : `Comprehensive, verified news reporting from ${state.name} State, Nigeria. Track local politics, infrastructure, economy, and community stories from ${state.capital} and major hubs.`;
  const canonicalUrl = `https://newsstand.ng/state/${state.slug}`;
  const hreflangs = getHreflangTags(`/state/${state.slug}`);

  return [
    { title },
    { name: 'description', content: description },
    { tagName: 'link', rel: 'canonical', href: canonicalUrl },
    { name: 'robots', content: 'index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1' },
    { property: 'og:type', content: 'website' },
    { property: 'og:title', content: title },
    { property: 'og:description', content: description },
    { property: 'og:url', content: canonicalUrl },
    { property: 'og:site_name', content: 'News Stand NG' },
    { name: 'twitter:card', content: 'summary_large_image' },
    { name: 'twitter:title', content: title },
    { name: 'twitter:description', content: description },
    ...hreflangs,
  ];
}

export async function loader({ params, request }) {
  const { slug } = params;
  if (!slug) {
    throw new Response('State slug is required', { status: 400 });
  }

  // 301 Permanent Redirect if someone requests /state/lagos-state instead of /state/lagos
  if (slug.endsWith('-state')) {
    const clean = slug.slice(0, -6);
    return redirect(`/state/${clean}`, 301);
  }

  const url = new URL(request.url);
  const isPidgin = url.pathname.startsWith('/pcm');

  try {
    const state = await api.getState(slug);
    if (!state) {
      throw new Response('State Not Found', { status: 404 });
    }
    return { state, isPidgin };
  } catch (err) {
    if (err instanceof Response) throw err;
    console.warn(`[State Loader] Failed for slug '${slug}':`, err.message);
    throw new Response('State Not Found', { status: 404 });
  }
}

export default function StateDetailPage() {
  const { state, isPidgin } = useLoaderData();

  const placeJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'AdministrativeArea',
    'name': `${state.name} State`,
    'alternateName': state.name,
    'description': state.intro,
    'url': `https://newsstand.ng/state/${state.slug}`,
    'containedInPlace': {
      '@type': 'Country',
      'name': 'Nigeria'
    }
  };

  const faqJsonLd = state.faqs && state.faqs.length > 0 ? {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    'mainEntity': state.faqs.map(faq => ({
      '@type': 'Question',
      'name': faq.question,
      'acceptedAnswer': {
        '@type': 'Answer',
        'text': faq.answer
      }
    }))
  } : null;

  const breadcrumbsJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    'itemListElement': [
      { '@type': 'ListItem', 'position': 1, 'name': 'Home', 'item': 'https://newsstand.ng/' },
      { '@type': 'ListItem', 'position': 2, 'name': '36 States', 'item': 'https://newsstand.ng/states' },
      { '@type': 'ListItem', 'position': 3, 'name': state.name, 'item': `https://newsstand.ng/state/${state.slug}` }
    ]
  };

  const articles = state.articles || [];
  const topTopics = state.topTopics || [];

  return (
    <div className="data-page-layout">
      {/* Structured Data */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(placeJsonLd) }}
      />
      {faqJsonLd && (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(faqJsonLd) }}
        />
      )}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumbsJsonLd) }}
      />

      {/* Top Header */}
      <header className="article-page-header">
        <div className="article-header-container">
          <Link to="/states" className="article-header-back-btn" title="Back to All States">
            <ArrowLeft size={18} />
            <span>All States</span>
          </Link>
          <Link to="/" className="article-header-brand">
            <span className="brand-dot" />
            <span className="brand-title">News Stand NG</span>
          </Link>
          <div className="header-badge-tag">
            <MapPin size={14} />
            <span>{state.geopoliticalZone}</span>
          </div>
        </div>
      </header>

      <main className="data-page-container">
        {/* Breadcrumb Navigation */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li><Link to="/states">36 States</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">{state.name}</li>
          </ol>
        </nav>

        {/* Streamlined State Header */}
        <section className="state-story-list-header">
          <div className="state-story-header-left">
            <div className="state-story-badge-wrap">
              <span className="state-zone-pill">{state.geopoliticalZone}</span>
              <span className="state-count-pill">{articles.length} {articles.length === 1 ? 'Story' : 'Stories'}</span>
            </div>
            <h1 className="state-story-list-title">
              {state.name} {state.name !== 'Federal Capital Territory' && 'State'} News & Stories
            </h1>
            <p className="state-story-list-subtitle">
              Verified local news reporting, breaking updates, and community developments from {state.capital} and across {state.name}.
            </p>
          </div>
          <Link to="/states" className="state-back-all-btn">
            <ArrowLeft size={15} />
            <span>All 36 States</span>
          </Link>
        </section>

        {/* Stories List Feed */}
        <section className="state-stories-container" aria-label={`News stories from ${state.name}`}>
          {articles.length > 0 ? (
            <div className="state-articles-list">
              {articles.map((art) => (
                <article key={art.id} className="state-story-item-card">
                  {art.imageUrl && (
                    <div className="state-story-item-thumb">
                      <Link to={`/news/${art.slug || art.id}`}>
                        <img
                          src={art.imageUrl}
                          alt={art.title}
                          className="state-story-img"
                          loading="lazy"
                        />
                      </Link>
                      <span className="state-story-cat-badge">
                        {art.category || 'General'}
                      </span>
                    </div>
                  )}
                  <div className="state-story-item-content">
                    <div className="state-story-meta-top">
                      <span className="state-story-source">{art.source || 'News Stand NG'}</span>
                      <span className="meta-dot">•</span>
                      <span className="state-story-time">{formatTimeAgo(art.publishedAt)}</span>
                    </div>
                    <h2 className="state-story-title">
                      <Link to={`/news/${art.slug || art.id}`} className="state-story-title-link">
                        {art.title}
                      </Link>
                    </h2>
                    {art.summary && (
                      <p className="state-story-summary">
                        {art.summary.length > 180 ? `${art.summary.slice(0, 180)}...` : art.summary}
                      </p>
                    )}
                    <div className="state-story-footer-actions">
                      <Link to={`/news/${art.slug || art.id}`} className="state-read-story-btn">
                        <span>Read full story</span>
                        <ArrowRight size={13} />
                      </Link>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <div className="empty-state-box">
              <p>No recent news articles have been tagged for {state.name} yet.</p>
              <Link to="/states" className="clear-filters-btn">
                Browse Other States
              </Link>
            </div>
          )}
        </section>
      </main>

      <Footer />
    </div>
  );
}
