import React from 'react';
import { useLoaderData, Link } from 'react-router';
import { api } from '../../src/services/api';
import { formatTimeAgo } from '../../src/services/timeUtils';
import { getHreflangTags } from '../../src/services/localization';
import Footer from '../../src/components/Footer';
import {
  Vote,
  Clock,
  HelpCircle,
  ChevronDown,
  ArrowLeft,
  ShieldCheck,
  Calendar,
  Flame,
  CheckCircle
} from 'lucide-react';

export function meta({ data, location }) {
  const isPidgin = location?.pathname?.startsWith('/pcm');
  const title = isPidgin
    ? 'Nigeria Election News & INEC Timetable: Off-Cycle Polls & CVR | News Stand NG'
    : 'Nigeria Elections 2026/2027: INEC Timetable, Off-Cycle Polls & BVAS | News Stand NG';
  const description = isPidgin
    ? 'Follow di latest election updates, INEC timetable for off-cycle governorship elections, continuous voter registration (CVR), and BVAS audit.'
    : 'Real-time Nigerian elections intelligence. Official INEC timetables, off-cycle governorship schedules, voter registration portals, and polling unit results verification.';
  const canonicalUrl = `https://newsstand.ng${location?.pathname || '/elections'}`;
  const hreflangs = getHreflangTags('/elections');

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
      api.getMarketMetric('elections'),
      api.getArticles({ category: 'Politics', pageSize: 6 }),
    ]);

    return {
      metric: metric || {
        title: 'Nigeria Elections & INEC Timetable Tracker',
        primaryValue: 'Off-Cycle Governorship Prep',
        secondaryValue: 'General Polls: Feb 2027',
        sourceAttribution: 'Independent National Electoral Commission (INEC Nigeria)',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: Array.isArray(relatedNews) ? relatedNews : (relatedNews?.articles || []),
      isPidgin,
    };
  } catch (err) {
    console.warn('[Elections Loader] Fallback engaged:', err.message);
    return {
      metric: {
        title: 'Nigeria Elections & INEC Timetable Tracker',
        primaryValue: 'Off-Cycle Governorship Prep',
        secondaryValue: 'General Polls: Feb 2027',
        sourceAttribution: 'Independent National Electoral Commission (INEC Nigeria)',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: [],
      isPidgin,
    };
  }
}

export default function ElectionsPage() {
  const { metric, articles, isPidgin } = useLoaderData();

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
      { '@type': 'ListItem', 'position': 2, 'name': 'Data Trackers', 'item': 'https://newsstand.ng/elections' }
    ]
  };

  const schedule = metric.historicalPoints || [];

  return (
    <div className="data-page-layout">
      {/* Structured Data */}
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
            <Vote size={14} />
            <span>Democracy Tracker</span>
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
            <li className="breadcrumb-active" aria-current="page">Nigeria Elections</li>
          </ol>
        </nav>

        {/* Hero Card */}
        <section className="tracker-hero-sheet" aria-labelledby="election-tracker-title">
          <div className="tracker-hero-header">
            <div className="tracker-badge-pill">
              <span className="live-indicator-dot" />
              <span>{isPidgin ? 'Democracy Monitor' : 'Electoral Commission Timetable'}</span>
            </div>
            {metric.lastUpdated && (
              <div className="tracker-timestamp">
                <Clock size={13} />
                <span>Last updated: {new Date(metric.lastUpdated).toLocaleTimeString('en-NG', { timeZone: 'Africa/Lagos', hour: '2-digit', minute: '2-digit' })} WAT</span>
              </div>
            )}
          </div>

          <h1 id="election-tracker-title" className="tracker-hero-title">
            {isPidgin ? 'Nigeria Election & INEC Timetable Tori' : metric.title}
          </h1>

          <p className="tracker-hero-lead">
            {isPidgin
              ? 'Comprehensive update for off-cycle governorship elections, continuous voter registration (CVR), and BVAS accreditation across Nigerian states.'
              : 'Authoritative calendar schedules, off-cycle gubernatorial countdowns, Continuous Voter Registration (CVR) timetables, and BVAS biometric verification updates.'}
          </p>

          {/* Rate Highlights Grid */}
          <div className="rate-cards-grid">
            <div className="rate-card parallel-rate-card">
              <div className="rate-card-label">
                <span>Immediate Priority</span>
                <span className="rate-pill-change">Off-Cycle Polls</span>
              </div>
              <div className="rate-card-value">{metric.primaryValue}</div>
              <div className="rate-card-sub">Staggered gubernatorial cycles across designated states</div>
            </div>

            <div className="rate-card official-rate-card">
              <div className="rate-card-label">
                <span>National Horizon</span>
                <span className="rate-pill-official">INEC Framework</span>
              </div>
              <div className="rate-card-value">{metric.secondaryValue || 'General Elections'}</div>
              <div className="rate-card-sub">Presidential, Senate, House of Reps, and State Assemblies</div>
            </div>
          </div>

          {/* Electoral Schedule Cards */}
          {schedule.length > 0 && (
            <div className="state-fuel-table-box">
              <div className="table-box-header">
                <Calendar size={16} />
                <span>Official Electoral Milestones & Off-Cycle Timetables</span>
              </div>
              <div className="state-fuel-grid">
                {schedule.map((item, idx) => (
                  <div key={idx} className="state-fuel-card">
                    <div className="state-fuel-header">
                      <span className="state-fuel-name">{item.label}</span>
                      <span className="state-fuel-zone">{item.date}</span>
                    </div>
                    <div className="state-fuel-price" style={{ color: '#10b981', fontSize: '1rem' }}>
                      <CheckCircle size={15} style={{ marginRight: 5, verticalAlign: 'text-bottom' }} />
                      <span>{item.date}</span>
                    </div>
                    <div className="state-fuel-note">{item.note}</div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Attribution Strip */}
          <div className="tracker-attribution-strip">
            <ShieldCheck size={16} className="attribution-icon" />
            <div className="attribution-content">
              <strong>Source Attribution:</strong> {metric.sourceAttribution} · Direct statutory releases under the Electoral Act 2022.
            </div>
          </div>
        </section>

        {/* Editorial Explainer */}
        <section className="tracker-explainer-card">
          <h2 className="explainer-heading">Understanding BVAS Biometrics and the IReV Portal</h2>
          <p>
            Under the Electoral Act 2022, the Bimodal Voter Accreditation System (BVAS) is the mandatory biometric mechanism deployed at every polling unit in Nigeria. BVAS validates voter identity using fingerprint and facial recognition to eliminate multiple voting.
          </p>
          <p>
            Upon the conclusion of ballot counting, presiding officers electronically photograph and transmit the certified Form EC8A polling unit result sheet directly to the INEC Result Viewing Portal (IReV) for public auditing.
          </p>
        </section>

        {/* Structured FAQ Section */}
        {metric.faqs && metric.faqs.length > 0 && (
          <section className="story-faq-section" aria-label="Frequently Asked Questions">
            <h2 className="faq-section-title">
              <HelpCircle size={18} />
              <span>Frequently Asked Questions About Nigeria Elections</span>
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
              <span>Latest Politics & Governance News</span>
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
