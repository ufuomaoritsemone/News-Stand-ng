import React from 'react';
import { useLoaderData, Link } from 'react-router';
import { api } from '../../src/services/api';
import { formatTimeAgo } from '../../src/services/timeUtils';
import { getHreflangTags } from '../../src/services/localization';
import Footer from '../../src/components/Footer';
import {
  GraduationCap,
  Clock,
  HelpCircle,
  ChevronDown,
  ArrowLeft,
  ShieldCheck,
  Calendar,
  CheckCircle2,
  Flame
} from 'lucide-react';

export function meta({ data, location }) {
  const isPidgin = location?.pathname?.startsWith('/pcm');
  const title = isPidgin
    ? 'JAMB UTME & WAEC Updates: Portal Deadlines, Fees & Exam Dates | News Stand NG'
    : 'JAMB UTME & WAEC News: Exam Dates, Portal Status & Result Checkers | News Stand NG';
  const description = isPidgin
    ? 'Check di latest updates for JAMB UTME profile code, registration closing date, WAEC May/June result checker and NECO exam schedule.'
    : 'Official tracker for JAMB UTME and WAEC examinations. Access registration timetables, accredited CBT centers, syllabus changes, and portal deadlines.';
  const canonicalUrl = `https://newsstand.ng${location?.pathname || '/jamb-waec-news'}`;
  const hreflangs = getHreflangTags('/jamb-waec-news');

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
      api.getMarketMetric('jamb-waec'),
      api.getArticles({ category: 'General', pageSize: 6 }),
    ]);

    return {
      metric: metric || {
        title: 'JAMB UTME & WAEC Examination Portal Updates',
        primaryValue: 'Registration Active',
        secondaryValue: 'UTME Exam: April 2027',
        sourceAttribution: 'Joint Admissions and Matriculation Board (JAMB) & West African Examinations Council (WAEC)',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: Array.isArray(relatedNews) ? relatedNews : (relatedNews?.articles || []),
      isPidgin,
    };
  } catch (err) {
    console.warn('[JAMB/WAEC Loader] Fallback engaged:', err.message);
    return {
      metric: {
        title: 'JAMB UTME & WAEC Examination Portal Updates',
        primaryValue: 'Registration Active',
        secondaryValue: 'UTME Exam: April 2027',
        sourceAttribution: 'Joint Admissions and Matriculation Board (JAMB) & West African Examinations Council (WAEC)',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: [],
      isPidgin,
    };
  }
}

export default function JambWaecNewsPage() {
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
      { '@type': 'ListItem', 'position': 2, 'name': 'Data Trackers', 'item': 'https://newsstand.ng/jamb-waec-news' }
    ]
  };

  const milestones = metric.historicalPoints || [];

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
            <GraduationCap size={14} />
            <span>Education Hub</span>
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
            <li className="breadcrumb-active" aria-current="page">JAMB & WAEC Updates</li>
          </ol>
        </nav>

        {/* Hero Card */}
        <section className="tracker-hero-sheet" aria-labelledby="jamb-tracker-title">
          <div className="tracker-hero-header">
            <div className="tracker-badge-pill">
              <span className="live-indicator-dot" />
              <span>{isPidgin ? 'Exam Portal Active' : 'National Examinations Monitor'}</span>
            </div>
            {metric.lastUpdated && (
              <div className="tracker-timestamp">
                <Clock size={13} />
                <span>Last updated: {new Date(metric.lastUpdated).toLocaleTimeString('en-NG', { timeZone: 'Africa/Lagos', hour: '2-digit', minute: '2-digit' })} WAT</span>
              </div>
            )}
          </div>

          <h1 id="jamb-tracker-title" className="tracker-hero-title">
            {isPidgin ? 'JAMB UTME & WAEC Examination Portal Tori' : metric.title}
          </h1>

          <p className="tracker-hero-lead">
            {isPidgin
              ? 'Official news, profile code guides, accredited CBT centers, and registration timetables for Nigerian students.'
              : 'Authoritative calendar schedules, portal registration deadlines, accreditation alerts, and syllabus requirements for Nigerian tertiary entrance and school certification candidates.'}
          </p>

          {/* Rate Highlights Grid */}
          <div className="rate-cards-grid">
            <div className="rate-card parallel-rate-card">
              <div className="rate-card-label">
                <span>Registration Status</span>
                <span className="rate-pill-change">JAMB Portal</span>
              </div>
              <div className="rate-card-value">{metric.primaryValue}</div>
              <div className="rate-card-sub">Accredited CBT centers nationwide with biometric verification</div>
            </div>

            <div className="rate-card official-rate-card">
              <div className="rate-card-label">
                <span>Key Milestone</span>
                <span className="rate-pill-official">Schedule</span>
              </div>
              <div className="rate-card-value">{metric.secondaryValue || 'UTME Window Active'}</div>
              <div className="rate-card-sub">Followed by May/June WAEC WASSCE and NECO diets</div>
            </div>
          </div>

          {/* Milestones / Timetable Strip */}
          {milestones.length > 0 && (
            <div className="state-fuel-table-box">
              <div className="table-box-header">
                <Calendar size={16} />
                <span>Annual Examination Calendar & Milestone Schedule</span>
              </div>
              <div className="state-fuel-grid">
                {milestones.map((item, idx) => (
                  <div key={idx} className="state-fuel-card">
                    <div className="state-fuel-header">
                      <span className="state-fuel-name">{item.label}</span>
                      <span className="state-fuel-zone">{item.date}</span>
                    </div>
                    {item.primaryValue > 0 ? (
                      <div className="state-fuel-price">
                        ₦{item.primaryValue.toLocaleString()}{' '}
                        <span className="price-unit">official fee</span>
                      </div>
                    ) : (
                      <div className="state-fuel-price" style={{ color: '#10b981' }}>
                        Scheduled Examination
                      </div>
                    )}
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
              <strong>Source Attribution:</strong> {metric.sourceAttribution} · Direct regulatory announcements and verified board gazettes.
            </div>
          </div>
        </section>

        {/* Editorial Explainer */}
        <section className="tracker-explainer-card">
          <h2 className="explainer-heading">Guidelines for NIN Profile Generation and CBT Registration</h2>
          <p>
            JAMB mandates the use of the National Identification Number (NIN) for all prospective candidates. Candidates must send their 11-digit NIN via SMS to 55019 or 66019 using a registered personal phone number. A unique 10-character profile code is returned for registration.
          </p>
          <p>
            Candidates are strongly cautioned to register solely at certified JAMB Computer Based Test (CBT) centers. Registration at unaccredited cybercafés or via third-party agents violates board protocols and risks disqualification.
          </p>
        </section>

        {/* Structured FAQ Section */}
        {metric.faqs && metric.faqs.length > 0 && (
          <section className="story-faq-section" aria-label="Frequently Asked Questions">
            <h2 className="faq-section-title">
              <HelpCircle size={18} />
              <span>Frequently Asked Questions About JAMB & WAEC</span>
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
              <span>Latest Education & Campus News</span>
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
