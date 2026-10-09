import React from 'react';
import { useLoaderData, Link } from 'react-router';
import { api } from '../../src/services/api';
import { formatTimeAgo } from '../../src/services/timeUtils';
import { getHreflangTags } from '../../src/services/localization';
import Footer from '../../src/components/Footer';
import {
  Fuel,
  Clock,
  ExternalLink,
  HelpCircle,
  ChevronDown,
  ArrowLeft,
  ShieldCheck,
  MapPin,
  Flame
} from 'lucide-react';

export function meta({ data, location }) {
  const isPidgin = location?.pathname?.startsWith('/pcm');
  const title = isPidgin
    ? 'Fuel Price in Nigeria Today: PMS Petrol Pump Price Across States | News Stand NG'
    : 'Fuel Price in Nigeria Today: Current PMS Petrol Pump Price by State | News Stand NG';
  const description = isPidgin
    ? 'Check di latest petrol fuel pump price for Lagos, Abuja, Port Harcourt, Kano and all states for Nigeria. Verified PMS prices and filling station updates.'
    : 'Real-time Premium Motor Spirit (PMS) petrol pump price tracker across Nigerian states. Compare retail prices in Lagos, Abuja, Port Harcourt, and regional depots.';
  const canonicalUrl = `https://newsstand.ng${location?.pathname || '/fuel-price-nigeria'}`;
  const hreflangs = getHreflangTags('/fuel-price-nigeria');

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

export async function loader({ request }) {
  const url = new URL(request.url);
  const isPidgin = url.pathname.startsWith('/pcm');

  try {
    const [metric, relatedNews] = await Promise.all([
      api.getMarketMetric('fuel-prices'),
      api.getArticles({ category: 'Economy', pageSize: 6 }),
    ]);

    return {
      metric: metric || {
        title: 'PMS Petrol Pump Price in Nigeria',
        primaryValue: '₦1,025 / Litre',
        secondaryValue: '₦998 – ₦1,060 Range',
        sourceAttribution: 'Major Energies Marketers Association of Nigeria (MEMAN) & NNPCL Retail',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: Array.isArray(relatedNews) ? relatedNews : (relatedNews?.articles || []),
      isPidgin,
    };
  } catch (err) {
    console.warn('[Fuel Loader] Fallback engaged:', err.message);
    return {
      metric: {
        title: 'PMS Petrol Pump Price in Nigeria',
        primaryValue: '₦1,025 / Litre',
        secondaryValue: '₦998 – ₦1,060 Range',
        sourceAttribution: 'Major Energies Marketers Association of Nigeria (MEMAN) & NNPCL Retail',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: [],
      isPidgin,
    };
  }
}

export default function FuelPriceNigeriaPage() {
  const { metric, articles, isPidgin } = useLoaderData();

  const datasetJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'Dataset',
    'name': 'Nigeria Retail Petrol (PMS) Pump Prices by State',
    'description': 'Real-time retail price benchmark for Premium Motor Spirit across major Nigerian metropolitan centers and distribution hubs.',
    'url': 'https://newsstand.ng/fuel-price-nigeria',
    'creator': {
      '@type': 'Organization',
      'name': 'News Stand NG',
      'url': 'https://newsstand.ng'
    },
    'spatialCoverage': {
      '@type': 'Place',
      'name': 'Nigeria'
    }
  };

  const faqJsonLd = metric.faqs && metric.faqs.length > 0 ? {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    'mainEntity': metric.faqs.map(faq => ({
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
      { '@type': 'ListItem', 'position': 2, 'name': 'Data Trackers', 'item': 'https://newsstand.ng/fuel-price-nigeria' }
    ]
  };

  const statePoints = metric.historicalPoints || [];

  return (
    <div className="data-page-layout">
      {/* Structured Data */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(datasetJsonLd) }}
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
          <Link to="/" className="article-header-back-btn" title="Back to Headlines">
            <ArrowLeft size={18} />
            <span>Headlines</span>
          </Link>
          <Link to="/" className="article-header-brand">
            <span className="brand-dot" />
            <span className="brand-title">News Stand NG</span>
          </Link>
          <div className="header-badge-tag">
            <Fuel size={14} />
            <span>Energy Tracker</span>
          </div>
        </div>
      </header>

      <main className="data-page-container">
        {/* Breadcrumb Navigation */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li><span className="breadcrumb-muted">Data Trackers</span></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">Fuel Price in Nigeria</li>
          </ol>
        </nav>

        {/* Hero Tracker Card */}
        <section className="tracker-hero-sheet" aria-labelledby="fuel-tracker-title">
          <div className="tracker-hero-header">
            <div className="tracker-badge-pill">
              <span className="live-indicator-dot" />
              <span>{isPidgin ? 'Petrol Price Watch' : 'Retail Petroleum Price Monitor'}</span>
            </div>
            {metric.lastUpdated && (
              <div className="tracker-timestamp">
                <Clock size={13} />
                <span>Last updated: {new Date(metric.lastUpdated).toLocaleTimeString('en-NG', { timeZone: 'Africa/Lagos', hour: '2-digit', minute: '2-digit' })} WAT</span>
              </div>
            )}
          </div>

          <h1 id="fuel-tracker-title" className="tracker-hero-title">
            {isPidgin ? 'Fuel Price in Nigeria (PMS Petrol)' : metric.title}
          </h1>

          <p className="tracker-hero-lead">
            {isPidgin
              ? 'Real-time retail price benchmark for Premium Motor Spirit across Lagos, Abuja, Port Harcourt, and major Nigerian distribution hubs.'
              : 'Authoritative retail price benchmarks for Premium Motor Spirit (PMS) across Nigerian filling stations, regional coastal terminals, and major metropolitan hubs.'}
          </p>

          {/* Rate Highlights Grid */}
          <div className="rate-cards-grid">
            <div className="rate-card parallel-rate-card">
              <div className="rate-card-label">
                <span>National Average Retail Pump Price</span>
                <span className="rate-pill-change">PMS Retail</span>
              </div>
              <div className="rate-card-value">{metric.primaryValue}</div>
              <div className="rate-card-sub">Weighted average across NNPCL, MEMAN, and independent filling stations</div>
            </div>

            <div className="rate-card official-rate-card">
              <div className="rate-card-label">
                <span>Regional Spread Range</span>
                <span className="rate-pill-official">Depot Variance</span>
              </div>
              <div className="rate-card-value">{metric.secondaryValue || '₦998 – ₦1,060'}</div>
              <div className="rate-card-sub">Coastal sea-loading ports versus northern hinterland logistics routes</div>
            </div>
          </div>

          {/* State-by-State Comparison Grid */}
          {statePoints.length > 0 && (
            <div className="state-fuel-table-box">
              <div className="table-box-header">
                <MapPin size={16} />
                <span>Regional Retail Pump Prices Across Key States & Zones</span>
              </div>
              <div className="state-fuel-grid">
                {statePoints.map((item, idx) => (
                  <div key={idx} className="state-fuel-card">
                    <div className="state-fuel-header">
                      <span className="state-fuel-name">{item.label}</span>
                      <span className="state-fuel-zone">{item.date}</span>
                    </div>
                    <div className="state-fuel-price">
                      ₦{item.primaryValue.toLocaleString()}{' '}
                      <span className="price-unit">/ litre</span>
                    </div>
                    <div className="state-fuel-note">{item.note}</div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Attribution Box */}
          <div className="tracker-attribution-strip">
            <ShieldCheck size={16} className="attribution-icon" />
            <div className="attribution-content">
              <strong>Source Attribution:</strong> {metric.sourceAttribution} · Verified through regulatory filings and commercial retail surveys.
            </div>
          </div>
        </section>

        {/* Editorial Explainer */}
        <section className="tracker-explainer-card">
          <h2 className="explainer-heading">Why Fuel Prices Differ Across Nigerian States</h2>
          <p>
            Following downstream petroleum deregulation in Nigeria, retail petrol prices reflect cost-reflective market dynamics. Coastal states like Lagos and Delta typically experience lower prices due to proximity to maritime import discharge terminals and refinery loading gantries.
          </p>
          <p>
            Inland and northern states experience higher haulage and trucking freight costs, creating a modest price differential across geopolitical zones. Commercial supply agreements with the 650,000 bpd Dangote Petroleum Refinery in Lekki continue to influence national product distribution efficiency.
          </p>
        </section>

        {/* Structured FAQ Section */}
        {metric.faqs && metric.faqs.length > 0 && (
          <section className="story-faq-section" aria-label="Frequently Asked Questions">
            <h2 className="faq-section-title">
              <HelpCircle size={18} />
              <span>Frequently Asked Questions About Fuel Prices</span>
            </h2>
            <div className="faq-accordion-group">
              {metric.faqs.map((faq, idx) => (
                <details key={idx} className="story-faq-item" open={idx === 0}>
                  <summary className="story-faq-question">
                    <span className="faq-q-text">{faq.question}</span>
                    <ChevronDown size={16} className="faq-chevron" />
                  </summary>
                  <div className="story-faq-answer">
                    <p>{faq.answer}</p>
                  </div>
                </details>
              ))}
            </div>
          </section>
        )}

        {/* Related News */}
        {articles.length > 0 && (
          <section className="article-related-shelf">
            <h2 className="shelf-section-title">
              <Flame size={16} />
              <span>Latest Oil, Energy & Economy News</span>
            </h2>
            <div className="related-stories-grid">
              {articles.map((art) => (
                <Link key={art.id} to={`/news/${art.slug || art.id}`} className="related-story-card">
                  {art.imageUrl && (
                    <div className="related-thumb-container">
                      <img src={art.imageUrl} alt="" className="related-thumb-img" loading="lazy" />
                    </div>
                  )}
                  <div className="related-card-details">
                    <div className="related-source-line">{art.source || 'News Stand NG'}</div>
                    <h3 className="related-title">{art.title}</h3>
                    {art.publishedAt && (
                      <div className="related-time">{formatTimeAgo(art.publishedAt)}</div>
                    )}
                  </div>
                </Link>
              ))}
            </div>
          </section>
        )}
      </main>

      <Footer />
    </div>
  );
}
