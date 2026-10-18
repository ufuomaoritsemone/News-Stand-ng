import React, { useState, useMemo } from 'react';
import { useLoaderData, Link } from 'react-router';
import { api, getOptimizedImageUrl } from '../../src/services/api';
import { formatTimeAgo } from '../../src/services/timeUtils';
import { getHreflangTags } from '../../src/services/localization';
import { INEC_PARTIES, getAllCandidates } from '../../src/data/inecPartiesRegistry';
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
  CheckCircle,
  Search,
  X,
  Users,
  User,
  ExternalLink,
  ChevronRight,
  Filter,
  Building2,
  FileText
} from 'lucide-react';

export function meta({ data, location }) {
  const isPidgin = location?.pathname?.startsWith('/pcm');
  const title = isPidgin
    ? 'Nigeria Election Tracker: 22 INEC Parties, Candidates & Live Tori | News Stand NG'
    : 'Nigeria Election Tracker 2026/2027: 22 INEC Parties, Candidates & Live News | News Stand NG';
  const description = isPidgin
    ? 'Track all 22 INEC-registered political parties (APC, PDP, LP, NNPP, etc.), candidate profiles, and live verified political stories.'
    : 'Official statutory directory of all 22 INEC-recognized political parties, presidential & gubernatorial flagbearers, and live verified election news stories.';
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
  const partyParam = url.searchParams.get('party');
  const candidateParam = url.searchParams.get('candidate');

  try {
    const metricPromise = api.getMarketMetric('elections');

    let articlesPromise;
    if (partyParam) {
      const match = INEC_PARTIES.find(
        (p) => p.acronym.toLowerCase() === partyParam.toLowerCase() || p.id === partyParam.toLowerCase()
      );
      articlesPromise = api.getElectionStories({
        topicSlug: match ? match.topicSlug : partyParam,
        searchKeyword: match ? match.acronym : partyParam,
        pageSize: 12
      });
    } else if (candidateParam) {
      const allCands = getAllCandidates();
      const match = allCands.find(
        (c) => c.topicSlug === candidateParam || c.name.toLowerCase() === candidateParam.toLowerCase()
      );
      articlesPromise = api.getElectionStories({
        topicSlug: candidateParam,
        searchKeyword: match ? match.name : candidateParam,
        pageSize: 12
      });
    } else {
      articlesPromise = api.getArticles({ category: 'Politics', pageSize: 12 });
    }

    const [metric, articlesResponse] = await Promise.all([metricPromise, articlesPromise]);

    const articles = Array.isArray(articlesResponse)
      ? articlesResponse
      : articlesResponse?.articles || [];

    return {
      metric: metric || {
        title: 'Nigeria Elections & INEC Political Parties Tracker',
        primaryValue: '22 Statutory Parties',
        secondaryValue: 'General Polls: Feb 2027',
        sourceAttribution: 'Independent National Electoral Commission (INEC Nigeria)',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles,
      initialParty: partyParam || null,
      initialCandidate: candidateParam || null,
      isPidgin,
    };
  } catch (err) {
    console.warn('[Elections Loader] Fallback engaged:', err.message);
    return {
      metric: {
        title: 'Nigeria Elections & INEC Political Parties Tracker',
        primaryValue: '22 Statutory Parties',
        secondaryValue: 'General Polls: Feb 2027',
        sourceAttribution: 'Independent National Electoral Commission (INEC Nigeria)',
        lastUpdated: new Date().toISOString(),
        historicalPoints: [],
        faqs: [],
      },
      articles: [],
      initialParty: null,
      initialCandidate: null,
      isPidgin,
    };
  }
}

export default function ElectionsPage() {
  const { metric, articles: initialArticles, initialParty, initialCandidate, isPidgin } = useLoaderData();

  // Navigation tab: 'parties' | 'schedule' | 'voter-guide'
  const [activeTab, setActiveTab] = useState('parties');

  // Filter & Search state
  const [searchQuery, setSearchQuery] = useState('');
  const [raceFilter, setRaceFilter] = useState('all'); // 'all' | 'presidential' | 'gubernatorial'

  // Selected party & candidate
  const [selectedParty, setSelectedParty] = useState(() => {
    if (!initialParty) return null;
    return (
      INEC_PARTIES.find(
        (p) => p.acronym.toLowerCase() === initialParty.toLowerCase() || p.id === initialParty.toLowerCase()
      ) || null
    );
  });

  const [selectedCandidate, setSelectedCandidate] = useState(() => {
    if (!initialCandidate) return null;
    return getAllCandidates().find((c) => c.topicSlug === initialCandidate) || null;
  });

  // Story state
  const [articles, setArticles] = useState(initialArticles);
  const [isLoadingArticles, setIsLoadingArticles] = useState(false);

  // Filter parties based on search and race
  const filteredParties = useMemo(() => {
    return INEC_PARTIES.filter((party) => {
      // Race filter
      if (raceFilter === 'presidential') {
        const hasPres = party.candidates.some((c) => c.office === 'Presidential');
        if (!hasPres) return false;
      } else if (raceFilter === 'gubernatorial') {
        const hasGov = party.candidates.some((c) => c.office === 'Gubernatorial');
        if (!hasGov) return false;
      }

      // Search query
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const matchesAcronym = party.acronym.toLowerCase().includes(q);
        const matchesName = party.name.toLowerCase().includes(q);
        const matchesChair = party.chairman.toLowerCase().includes(q);
        const matchesCand = party.candidates.some((c) => c.name.toLowerCase().includes(q));
        return matchesAcronym || matchesName || matchesChair || matchesCand;
      }

      return true;
    });
  }, [searchQuery, raceFilter]);

  // Handle party selection
  async function handleSelectParty(party) {
    if (selectedParty?.id === party.id && !selectedCandidate) return;

    setSelectedParty(party);
    setSelectedCandidate(null);
    setIsLoadingArticles(true);

    try {
      const res = await api.getElectionStories({
        topicSlug: party.topicSlug,
        searchKeyword: party.acronym,
        pageSize: 12,
      });
      const list = Array.isArray(res) ? res : res?.articles || [];
      setArticles(list);
    } catch (err) {
      console.warn('Failed to fetch party stories:', err);
    } finally {
      setIsLoadingArticles(false);
    }

    // Smooth scroll to story section
    const el = document.getElementById('election-stories-stream');
    if (el) el.scrollIntoView({ behavior: 'smooth' });
  }

  // Handle candidate selection
  async function handleSelectCandidate(cand, party) {
    setSelectedCandidate(cand);
    setSelectedParty(party);
    setIsLoadingArticles(true);

    try {
      const res = await api.getElectionStories({
        topicSlug: cand.topicSlug,
        searchKeyword: cand.name,
        pageSize: 12,
      });
      const list = Array.isArray(res) ? res : res?.articles || [];
      setArticles(list);
    } catch (err) {
      console.warn('Failed to fetch candidate stories:', err);
    } finally {
      setIsLoadingArticles(false);
    }

    // Smooth scroll to story section
    const el = document.getElementById('election-stories-stream');
    if (el) el.scrollIntoView({ behavior: 'smooth' });
  }

  // Reset to general election stories
  async function handleResetFilter() {
    setSelectedParty(null);
    setSelectedCandidate(null);
    setIsLoadingArticles(true);

    try {
      const res = await api.getArticles({ category: 'Politics', pageSize: 12 });
      const list = Array.isArray(res) ? res : res?.articles || [];
      setArticles(list);
    } catch (err) {
      console.warn('Failed to reset stories:', err);
    } finally {
      setIsLoadingArticles(false);
    }
  }

  // JSON-LD Structured Data
  const partyListJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'ItemList',
    'name': 'Recognized Political Parties of Nigeria',
    'description': 'Official statutory registry of the 22 political parties recognized by the Independent National Electoral Commission (INEC).',
    'numberOfItems': INEC_PARTIES.length,
    'itemListElement': INEC_PARTIES.map((party, index) => ({
      '@type': 'ListItem',
      'position': index + 1,
      'item': {
        '@type': 'PoliticalParty',
        'name': party.name,
        'alternateName': party.acronym,
        'address': party.headquarters,
        'leader': {
          '@type': 'Person',
          'name': party.chairman,
          'jobTitle': 'National Chairman'
        }
      }
    }))
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
      { '@type': 'ListItem', 'position': 2, 'name': 'Data Trackers', 'item': 'https://newsstand.ng/elections' }
    ]
  };

  const schedule = metric.historicalPoints || [];

  return (
    <div className="data-page-layout">
      {/* Structured Data */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(partyListJsonLd) }}
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
            <Vote size={14} />
            <span>Election Tracker</span>
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
            <li className="breadcrumb-active" aria-current="page">Election & Parties Tracker</li>
          </ol>
        </nav>

        {/* Hero Card */}
        <section className="tracker-hero-sheet" aria-labelledby="election-tracker-title">
          <div className="tracker-hero-header">
            <div className="tracker-badge-pill">
              <span className="live-indicator-dot" />
              <span>{isPidgin ? 'INEC Statutory Monitor' : 'INEC 22 Registered Parties & Candidates'}</span>
            </div>
            {metric.lastUpdated && (
              <div className="tracker-timestamp">
                <Clock size={13} />
                <span>Last updated: {new Date(metric.lastUpdated).toLocaleTimeString('en-NG', { timeZone: 'Africa/Lagos', hour: '2-digit', minute: '2-digit' })} WAT</span>
              </div>
            )}
          </div>

          <h1 id="election-tracker-title" className="tracker-hero-title">
            {isPidgin ? 'Nigeria Election & Political Parties Tracker' : 'Nigeria Election & Political Parties Tracker'}
          </h1>

          <p className="tracker-hero-lead">
            {isPidgin
              ? 'Complete list of all 22 statutory political parties recognize by INEC, dia flagbearers, and real-time news stories about each party.'
              : 'Official statutory directory of Nigeria\'s 22 recognized political parties, presidential and gubernatorial candidates, and aggregated real-time campaign coverage.'}
          </p>

          {/* Quick Metrics Bar */}
          <div className="rate-cards-grid">
            <div className="rate-card parallel-rate-card">
              <div className="rate-card-label">
                <span>Recognized Parties</span>
                <span className="rate-pill-change">Statutory Register</span>
              </div>
              <div className="rate-card-value">22 Political Parties</div>
              <div className="rate-card-sub">Accredited by INEC under the 1999 Constitution & Electoral Act</div>
            </div>

            <div className="rate-card official-rate-card">
              <div className="rate-card-label">
                <span>National Horizon</span>
                <span className="rate-pill-official">Election Cycle</span>
              </div>
              <div className="rate-card-value">{metric.secondaryValue || 'General Polls: Feb 2027'}</div>
              <div className="rate-card-sub">Off-cycle governorships + presidential & legislative countdown</div>
            </div>
          </div>

          {/* Attribution Strip */}
          <div className="tracker-attribution-strip">
            <ShieldCheck size={16} className="attribution-icon" />
            <div className="attribution-content">
              <strong>Statutory Attribution:</strong> Independent National Electoral Commission (INEC Nigeria) · Official Register of Political Parties ([inecnigeria.org/parties](https://www.inecnigeria.org/parties)).
            </div>
          </div>
        </section>

        {/* Section Navigation Tabs */}
        <div className="election-nav-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'parties'}
            className={`election-tab-btn ${activeTab === 'parties' ? 'active' : ''}`}
            onClick={() => setActiveTab('parties')}
          >
            <Users size={16} />
            <span>Parties & Candidates ({INEC_PARTIES.length})</span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'schedule'}
            className={`election-tab-btn ${activeTab === 'schedule' ? 'active' : ''}`}
            onClick={() => setActiveTab('schedule')}
          >
            <Calendar size={16} />
            <span>Electoral Calendar & Timetable</span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'voter-guide'}
            className={`election-tab-btn ${activeTab === 'voter-guide' ? 'active' : ''}`}
            onClick={() => setActiveTab('voter-guide')}
          >
            <Vote size={16} />
            <span>BVAS, IReV & FAQs</span>
          </button>
        </div>

        {/* TAB 1: PARTIES & CANDIDATES EXPLORER */}
        {activeTab === 'parties' && (
          <section aria-label="Political Parties and Candidates Hub">
            {/* Search and Filter Controls */}
            <div className="election-controls-bar">
              <div className="election-search-input-wrap">
                <Search size={18} className="search-icon" />
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Search party by name, acronym (e.g. APC, PDP, LP), chairman, or candidate..."
                  className="election-search-input"
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={() => setSearchQuery('')}
                    style={{ position: 'absolute', right: 14, background: 'none', border: 'none', cursor: 'pointer', color: '#94a3b8' }}
                    aria-label="Clear search"
                  >
                    <X size={16} />
                  </button>
                )}
              </div>

              <div className="election-filter-pills">
                <button
                  type="button"
                  className={`election-filter-pill ${raceFilter === 'all' ? 'active' : ''}`}
                  onClick={() => setRaceFilter('all')}
                >
                  All 22 Recognized Parties
                </button>
                <button
                  type="button"
                  className={`election-filter-pill ${raceFilter === 'presidential' ? 'active' : ''}`}
                  onClick={() => setRaceFilter('presidential')}
                >
                  Presidential Flagbearers
                </button>
                <button
                  type="button"
                  className={`election-filter-pill ${raceFilter === 'gubernatorial' ? 'active' : ''}`}
                  onClick={() => setRaceFilter('gubernatorial')}
                >
                  Gubernatorial Contenders
                </button>
              </div>
            </div>

            {/* Active Filter Strip */}
            {(selectedParty || selectedCandidate) && (
              <div className="active-filter-strip">
                <div className="active-filter-details">
                  <Filter size={16} style={{ color: '#10b981' }} />
                  <span>
                    Filtering live stories for:
                    {selectedCandidate ? (
                      <strong> {selectedCandidate.name} ({selectedParty?.acronym || 'Candidate'})</strong>
                    ) : (
                      <strong> {selectedParty.name} ({selectedParty.acronym})</strong>
                    )}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={handleResetFilter}
                  className="active-filter-clear-btn"
                >
                  ✕ Show All Election Stories
                </button>
              </div>
            )}

            {/* 22 Parties Grid */}
            <div className="election-parties-grid">
              {filteredParties.map((party) => {
                const isSelected = selectedParty?.id === party.id && !selectedCandidate;

                return (
                  <div
                    key={party.id}
                    className={`election-party-card ${isSelected ? 'selected' : ''}`}
                    style={{ '--party-color': party.color }}
                    onClick={() => handleSelectParty(party)}
                  >
                    <div>
                      <div className="party-card-top">
                        <span
                          className="party-badge-acronym"
                          style={{ backgroundColor: party.color }}
                        >
                          {party.acronym}
                        </span>
                        <span className="party-status-tag">{party.status}</span>
                      </div>

                      <h3 className="party-card-title">{party.name}</h3>

                      <div className="party-leadership-meta">
                        <div>
                          <strong>Chairman:</strong> {party.chairman}
                        </div>
                        <div style={{ color: '#94a3b8' }}>
                          <Building2 size={12} style={{ display: 'inline', marginRight: 4, verticalAlign: 'middle' }} />
                          {party.headquarters}
                        </div>
                      </div>
                    </div>

                    {/* Candidates Shelf if party has prominent candidates */}
                    {party.candidates.length > 0 && (
                      <div className="party-candidates-shelf">
                        <span className="party-candidates-shelf-label">Flagbearers / Candidates:</span>
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                          {party.candidates.map((cand) => (
                            <button
                              key={cand.topicSlug}
                              type="button"
                              className="candidate-mini-chip"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleSelectCandidate(cand, party);
                              }}
                              title={`View stories for ${cand.name}`}
                            >
                              <User size={12} />
                              <span>{cand.name}</span>
                            </button>
                          ))}
                        </div>
                      </div>
                    )}

                    <div className="party-card-footer">
                      <span>{isSelected ? 'Viewing stories below' : 'View party stories'}</span>
                      <ChevronRight size={15} />
                    </div>
                  </div>
                );
              })}
            </div>

            {filteredParties.length === 0 && (
              <div className="election-empty-box">
                <Search size={32} style={{ opacity: 0.4 }} />
                <p>No parties or candidates matched your search criteria.</p>
                <button
                  type="button"
                  onClick={() => { setSearchQuery(''); setRaceFilter('all'); }}
                  className="active-filter-clear-btn"
                >
                  Reset Search
                </button>
              </div>
            )}

            {/* LIVE STORIES STREAM */}
            <div id="election-stories-stream" className="election-stories-section">
              <div className="election-stories-header">
                <h2 className="election-stories-title">
                  <Flame size={20} style={{ color: '#f97316' }} />
                  <span>
                    {selectedCandidate
                      ? `Stories about ${selectedCandidate.name} (${selectedParty?.acronym})`
                      : selectedParty
                      ? `Stories about ${selectedParty.name} (${selectedParty.acronym})`
                      : 'Latest Nigerian Election & Campaign Stories'}
                  </span>
                </h2>

                <div className="election-stories-count">
                  {isLoadingArticles
                    ? 'Updating feed...'
                    : `${articles.length} verified ${articles.length === 1 ? 'article' : 'articles'}`}
                </div>
              </div>

              {isLoadingArticles ? (
                <div className="election-empty-box">
                  <div className="live-indicator-dot" style={{ width: 14, height: 14 }} />
                  <p>Fetching verified coverage from national news publications...</p>
                </div>
              ) : articles.length > 0 ? (
                <div className="election-stories-grid">
                  {articles.map((art) => {
                    const articleUrl = `/news/${art.slug || art.id}`;

                    return (
                      <Link
                        key={art.id}
                        to={articleUrl}
                        className="election-story-card"
                      >
                        {art.imageUrl && (
                          <div className="election-story-thumb-wrap">
                            <img
                              src={getOptimizedImageUrl(art.imageUrl, 640)}
                              alt={art.title}
                              className="election-story-thumb-img"
                              loading="lazy"
                            />
                          </div>
                        )}

                        <div className="election-story-content">
                          <div className="election-story-source-line">
                            <span className="election-story-source">{art.source || 'News Stand NG'}</span>
                            {art.publishedAt && (
                              <span className="election-story-time">{formatTimeAgo(art.publishedAt)}</span>
                            )}
                          </div>

                          <h3 className="election-story-title">{art.title}</h3>

                          {art.excerpt && (
                            <p className="election-story-excerpt">{art.excerpt}</p>
                          )}

                          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '0.8rem', color: '#10b981', fontWeight: 600 }}>
                            <span>Read Verified Story →</span>
                            {selectedParty && (
                              <span style={{ fontSize: '0.72rem', background: 'rgba(16, 185, 129, 0.1)', padding: '2px 8px', borderRadius: 4 }}>
                                {selectedParty.acronym}
                              </span>
                            )}
                          </div>
                        </div>
                      </Link>
                    );
                  })}
                </div>
              ) : (
                <div className="election-empty-box">
                  <FileText size={32} style={{ opacity: 0.4 }} />
                  <p>
                    No recent stories directly mentioning{' '}
                    <strong>{selectedCandidate?.name || selectedParty?.name || 'this entity'}</strong> were found in the current news cycle.
                  </p>
                  <button
                    type="button"
                    onClick={handleResetFilter}
                    className="active-filter-clear-btn"
                  >
                    View All General Election Stories
                  </button>
                </div>
              )}
            </div>
          </section>
        )}

        {/* TAB 2: ELECTORAL SCHEDULE & CALENDAR */}
        {activeTab === 'schedule' && (
          <section aria-label="Official Electoral Milestones">
            {schedule.length > 0 ? (
              <div className="state-fuel-table-box" style={{ marginTop: 0 }}>
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
            ) : (
              <div className="election-empty-box">
                <Calendar size={32} style={{ opacity: 0.4 }} />
                <p>Electoral timetable milestones being audited against statutory INEC gazettes.</p>
              </div>
            )}
          </section>
        )}

        {/* TAB 3: VOTER GUIDE, BVAS & FAQS */}
        {activeTab === 'voter-guide' && (
          <section aria-label="BVAS and Voter Guide">
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
              <section className="story-faq-section" aria-label="Frequently Asked Questions" style={{ marginTop: 24 }}>
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
          </section>
        )}
      </main>

      <Footer />
    </div>
  );
}
