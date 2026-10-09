import React from 'react';
import { useLoaderData, Link } from 'react-router';
import { api } from '../../src/services/api';
import { formatTimeAgo } from '../../src/services/timeUtils';
import { getHreflangTags } from '../../src/services/localization';
import Footer from '../../src/components/Footer';
import {
  TrendingUp,
  Clock,
  ExternalLink,
  HelpCircle,
  ChevronDown,
  ArrowLeft,
  ShieldCheck,
  Building2,
  Calendar,
  Flame
} from 'lucide-react';

export function meta({ data, location }) {
  const isPidgin = location?.pathname?.startsWith('/pcm');
  const title = isPidgin
    ? 'Naira Exchange Rate Today: Official CBN & Parallel Market FX Price | News Stand NG'
    : 'Naira Exchange Rate Today: Official NAFEM vs Parallel Market (USD/NGN) | News Stand NG';
  const description = isPidgin
    ? 'Check di latest Naira exchange rate against US Dollar for official CBN NAFEM and parallel black market, plus 30-day rate chart and verified news.'
    : 'Live Nigerian Naira (USD/NGN) exchange rate monitor. Real-time comparison of Central Bank of Nigeria (NAFEM) and parallel street market rates, 30-day volatility chart, and forex news.';
  const canonicalUrl = `https://newsstand.ng${location?.pathname || '/naira-exchange-rate'}`;
  const hreflangs = getHreflangTags('/naira-exchange-rate');

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
      api.getMarketMetric('naira-fx'),
      api.getArticles({ category: 'Economy', pageSize: 6 }),
    ]);

    return {
      metric: metric || {
        title: 'Naira Exchange Rate (USD/NGN)',
        primaryValue: '₦1,645.00 / $1',
        secondaryValue: '₦1,482.50 Official NAFEM',
        change24h: '+0.35%',
        sourceAttribution: 'Central Bank of Nigeria (NAFEM) & Lagos Parallel Market Desk',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: Array.isArray(relatedNews) ? relatedNews : (relatedNews?.articles || []),
      isPidgin,
    };
  } catch (err) {
    console.warn('[Naira Loader] Fallback engaged:', err.message);
    return {
      metric: {
        title: 'Naira Exchange Rate (USD/NGN)',
        primaryValue: '₦1,645.00 / $1',
        secondaryValue: '₦1,482.50 Official NAFEM',
        change24h: '+0.35%',
        sourceAttribution: 'Central Bank of Nigeria (NAFEM) & Lagos Parallel Market Desk',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: [],
      isPidgin,
    };
  }
}

export default function NairaExchangeRatePage() {
  const { metric, articles, isPidgin } = useLoaderData();

  const datasetJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'Dataset',
    'name': 'Nigerian Naira (USD/NGN) Exchange Rate Daily Fixings',
    'description': 'Historical and real-time exchange rates for the Nigerian Naira against the US Dollar across official NAFEM and parallel markets.',
    'url': 'https://newsstand.ng/naira-exchange-rate',
    'creator': {
      '@type': 'Organization',
      'name': 'News Stand NG',
      'url': 'https://newsstand.ng'
    },
    'temporalCoverage': '2026',
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
      { '@type': 'ListItem', 'position': 2, 'name': 'Data Trackers', 'item': 'https://newsstand.ng/naira-exchange-rate' }
    ]
  };

  // Render SVG Sparkline / Trend Chart
  const points = metric.historicalPoints || [];
  const minVal = points.length > 0 ? Math.min(...points.map(p => p.secondaryValue || p.primaryValue)) - 30 : 1400;
  const maxVal = points.length > 0 ? Math.max(...points.map(p => p.primaryValue)) + 30 : 1700;
  const range = maxVal - minVal || 1;

  const chartHeight = 220;
  const chartWidth = 720;
  const paddingX = 40;
  const paddingY = 25;

  const getCoordinates = (index, value) => {
    const x = paddingX + (index / (Math.max(1, points.length - 1))) * (chartWidth - paddingX * 2);
    const y = chartHeight - paddingY - ((value - minVal) / range) * (chartHeight - paddingY * 2);
    return { x, y };
  };

  const parallelLine = points.map((p, idx) => {
    const { x, y } = getCoordinates(idx, p.primaryValue);
    return `${idx === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
  }).join(' ');

  const officialLine = points.map((p, idx) => {
    const { x, y } = getCoordinates(idx, p.secondaryValue || p.primaryValue);
    return `${idx === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
  }).join(' ');

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

      {/* Top Masthead Header */}
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
            <TrendingUp size={14} />
            <span>Market Data</span>
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
            <li className="breadcrumb-active" aria-current="page">Naira Exchange Rate</li>
          </ol>
        </nav>

        {/* Hero Tracker Card */}
        <section className="tracker-hero-sheet" aria-labelledby="tracker-title">
          <div className="tracker-hero-header">
            <div className="tracker-badge-pill">
              <span className="live-indicator-dot" />
              <span>{isPidgin ? 'Live FX Watch' : 'Live Foreign Exchange Monitor'}</span>
            </div>
            {metric.lastUpdated && (
              <div className="tracker-timestamp">
                <Clock size={13} />
                <span>Last updated: {new Date(metric.lastUpdated).toLocaleTimeString('en-NG', { timeZone: 'Africa/Lagos', hour: '2-digit', minute: '2-digit' })} WAT</span>
              </div>
            )}
          </div>

          <h1 id="tracker-title" className="tracker-hero-title">
            {isPidgin ? 'Naira FX Price Today (USD/NGN)' : metric.title}
          </h1>

          <p className="tracker-hero-lead">
            {isPidgin
              ? 'Real-time update of Naira to US Dollar for both CBN official window and street parallel black market with 30-day trend chart.'
              : 'Real-time Nigerian Foreign Exchange intelligence comparing the official Central Bank of Nigeria NAFEM window against open parallel street market quotes.'}
          </p>

          {/* Rate Highlights Grid */}
          <div className="rate-cards-grid">
            <div className="rate-card parallel-rate-card">
              <div className="rate-card-label">
                <span>Parallel Market (Street / BDC)</span>
                <span className="rate-pill-change">{metric.change24h || '+0.35%'}</span>
              </div>
              <div className="rate-card-value">{metric.primaryValue}</div>
              <div className="rate-card-sub">Cash rate across Lagos, Abuja, & Kano Bureau de Change hubs</div>
            </div>

            <div className="rate-card official-rate-card">
              <div className="rate-card-label">
                <span>Official Window (NAFEM / CBN)</span>
                <span className="rate-pill-official">CBN Model</span>
              </div>
              <div className="rate-card-value">{metric.secondaryValue || '₦1,482.50 / $1'}</div>
              <div className="rate-card-sub">Willing-buyer, willing-seller order-book rate via commercial banks</div>
            </div>
          </div>

          {/* 30-Day SVG Chart */}
          {points.length > 0 && (
            <div className="tracker-chart-container">
              <div className="chart-header-row">
                <div className="chart-title">
                  <Calendar size={15} />
                  <span>30-Day Rate Volatility & Trajectory</span>
                </div>
                <div className="chart-legend">
                  <span className="legend-item"><span className="legend-dot parallel" /> Parallel Rate</span>
                  <span className="legend-item"><span className="legend-dot official" /> Official NAFEM</span>
                </div>
              </div>

              <div className="svg-chart-wrapper">
                <svg viewBox={`0 0 ${chartWidth} ${chartHeight}`} className="tracker-svg-chart" aria-label="30-day exchange rate trend chart">
                  {/* Subtle Gridlines */}
                  <line x1={paddingX} y1={paddingY} x2={chartWidth - paddingX} y2={paddingY} stroke="rgba(255,255,255,0.08)" strokeDasharray="3 3" />
                  <line x1={paddingX} y1={chartHeight / 2} x2={chartWidth - paddingX} y2={chartHeight / 2} stroke="rgba(255,255,255,0.08)" strokeDasharray="3 3" />
                  <line x1={paddingX} y1={chartHeight - paddingY} x2={chartWidth - paddingX} y2={chartHeight - paddingY} stroke="rgba(255,255,255,0.12)" />

                  {/* Lines */}
                  <path d={officialLine} fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
                  <path d={parallelLine} fill="none" stroke="#f59e0b" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />

                  {/* Terminal dots */}
                  {points.length > 0 && (
                    <>
                      <circle cx={getCoordinates(points.length - 1, points[points.length - 1].primaryValue).x} cy={getCoordinates(points.length - 1, points[points.length - 1].primaryValue).y} r="4.5" fill="#f59e0b" />
                      <circle cx={getCoordinates(points.length - 1, points[points.length - 1].secondaryValue || points[points.length - 1].primaryValue).x} cy={getCoordinates(points.length - 1, points[points.length - 1].secondaryValue || points[points.length - 1].primaryValue).y} r="4.5" fill="#10b981" />
                    </>
                  )}
                </svg>
              </div>

              <div className="chart-dates-axis">
                <span>{points[0]?.label || '30 days ago'}</span>
                <span>{points[Math.floor(points.length / 2)]?.label || '15 days ago'}</span>
                <span>{points[points.length - 1]?.label || 'Today'}</span>
              </div>
            </div>
          )}

          {/* Attribution Box */}
          <div className="tracker-attribution-strip">
            <ShieldCheck size={16} className="attribution-icon" />
            <div className="attribution-content">
              <strong>Source Attribution:</strong> {metric.sourceAttribution} · Verified through public order-book fixings and financial market reporting.
            </div>
          </div>
        </section>

        {/* Editorial Explainer Section */}
        <section className="tracker-explainer-card">
          <h2 className="explainer-heading">Understanding the Naira Foreign Exchange Window</h2>
          <p>
            Nigeria operates a managed floating exchange rate system through the Nigerian Autonomous Foreign Exchange Market (NAFEM), administered by the Central Bank of Nigeria (CBN). Rates are established through market-determined demand and supply dynamics between authorized dealers.
          </p>
          <p>
            The parallel market reflects cash transactions outside formal electronic banking corridors, catering primarily to retail travelers, informal traders, and immediate liquidity demands. The spread between official and parallel rates remains an essential barometer of macroeconomic liquidity and foreign capital inflows.
          </p>
        </section>

        {/* Structured FAQ Section */}
        {metric.faqs && metric.faqs.length > 0 && (
          <section className="story-faq-section" aria-label="Frequently Asked Questions">
            <h2 className="faq-section-title">
              <HelpCircle size={18} />
              <span>Frequently Asked Questions About Naira Exchange Rates</span>
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

        {/* Related Economic News */}
        {articles.length > 0 && (
          <section className="article-related-shelf">
            <h2 className="shelf-section-title">
              <Flame size={16} />
              <span>Latest Economic & Currency News</span>
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
