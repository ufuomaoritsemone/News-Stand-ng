import React, { useState, useMemo } from 'react';
import { useLoaderData, Link } from 'react-router';
import { api } from '../../src/services/api';
import { getHreflangTags } from '../../src/services/localization';
import Footer from '../../src/components/Footer';
import {
  MapPin,
  Search,
  ArrowLeft,
  ChevronRight,
  Compass,
  Building,
  Layers
} from 'lucide-react';

export function meta({ location }) {
  const isPidgin = location?.pathname?.startsWith('/pcm');
  const title = isPidgin
    ? 'All 36 States & FCT News Directory: Local Tori Across Nigeria | News Stand NG'
    : 'Nigeria State News Directory: All 36 States & FCT Regional Hubs | News Stand NG';
  const description = isPidgin
    ? 'Find clean, verified local news across all 36 states of Nigeria plus Abuja FCT. Read news from your state capital and home town.'
    : 'Explore localized news reporting across all 36 Nigerian states and the Federal Capital Territory (FCT). Filter by geopolitical zone and metropolitan city.';
  const canonicalUrl = `https://newsstand.ng${location?.pathname || '/states'}`;
  const hreflangs = getHreflangTags('/states');

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
    const groups = await api.getStates();
    return {
      groups: Array.isArray(groups) ? groups : [],
      isPidgin,
    };
  } catch (err) {
    console.warn('[States Directory Loader] Fallback engaged:', err.message);
    return {
      groups: [],
      isPidgin,
    };
  }
}

export default function StatesDirectoryPage() {
  const { groups, isPidgin } = useLoaderData();
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedZone, setSelectedZone] = useState('All');

  const filteredGroups = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();

    return groups
      .filter((g) => selectedZone === 'All' || g.zone === selectedZone)
      .map((g) => ({
        ...g,
        states: g.states.filter((s) => {
          if (!q) return true;
          return (
            s.name.toLowerCase().includes(q) ||
            s.capital.toLowerCase().includes(q) ||
            s.majorCities?.some((c) => c.toLowerCase().includes(q))
          );
        }),
      }))
      .filter((g) => g.states.length > 0);
  }, [groups, searchQuery, selectedZone]);

  const collectionJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    'name': 'Nigeria State News Directory',
    'description': 'Verified regional and local news directories across all 36 Nigerian states and the Federal Capital Territory.',
    'url': 'https://newsstand.ng/states',
    'hasPart': groups.flatMap(g => g.states.map(s => ({
      '@type': 'AdministrativeArea',
      'name': s.name,
      'url': `https://newsstand.ng/state/${s.slug}`
    })))
  };

  const breadcrumbsJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    'itemListElement': [
      { '@type': 'ListItem', 'position': 1, 'name': 'Home', 'item': 'https://newsstand.ng/' },
      { '@type': 'ListItem', 'position': 2, 'name': 'States Directory', 'item': 'https://newsstand.ng/states' }
    ]
  };

  const allZones = ['All', 'South West', 'South South', 'South East', 'North Central', 'North West', 'North East'];

  return (
    <div className="data-page-layout">
      {/* Structured Data */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(collectionJsonLd) }}
      />
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
            <MapPin size={14} />
            <span>Regional Hubs</span>
          </div>
        </div>
      </header>

      <main className="data-page-container">
        {/* Breadcrumb Navigation */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">36 States & FCT</li>
          </ol>
        </nav>

        {/* Directory Masthead */}
        <section className="states-masthead-hero">
          <div className="states-badge-chip">
            <Compass size={14} />
            <span>{isPidgin ? 'Nationwide News Coverage' : 'Federation Geopolitical Directory'}</span>
          </div>
          <h1 className="states-main-title">
            {isPidgin ? 'Tori Across All 36 States & Abuja FCT' : 'Nigeria State News Directory'}
          </h1>
          <p className="states-lead-text">
            {isPidgin
              ? 'Read clean, verified local news reporting from your state capital, commercial cities, and geopolitical zones without any fake news.'
              : 'Explore localized news reporting, commercial developments, and sub-national governance across all 36 states and the Federal Capital Territory.'}
          </p>

          {/* Search & Filter Toolbar */}
          <div className="states-filter-bar">
            <div className="states-search-input-wrap">
              <Search size={16} className="states-search-icon" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search state, capital city, or commercial hub..."
                className="states-search-input"
                aria-label="Filter states"
              />
              {searchQuery && (
                <button
                  type="button"
                  onClick={() => setSearchQuery('')}
                  className="states-clear-search-btn"
                >
                  ✕
                </button>
              )}
            </div>

            {/* Zone Filter Chips */}
            <div className="zone-filter-chips">
              {allZones.map((zone) => (
                <button
                  key={zone}
                  type="button"
                  className={`zone-chip-btn ${selectedZone === zone ? 'active' : ''}`}
                  onClick={() => setSelectedZone(zone)}
                >
                  {zone}
                </button>
              ))}
            </div>
          </div>
        </section>

        {/* Zones & States List */}
        <div className="zones-list-container">
          {filteredGroups.map((group) => (
            <section key={group.zone} className="zone-section" aria-labelledby={`zone-${group.zone.replace(/\s+/g, '-').toLowerCase()}`}>
              <div className="zone-header">
                <div className="zone-indicator-bar" />
                <h2 id={`zone-${group.zone.replace(/\s+/g, '-').toLowerCase()}`} className="zone-title">
                  {group.zone} Zone
                </h2>
                <span className="zone-count-badge">{group.states.length} States</span>
              </div>

              <div className="state-cards-grid">
                {group.states.map((state) => (
                  <Link
                    key={state.slug}
                    to={`/state/${state.slug}`}
                    className="state-directory-card"
                  >
                    <div className="state-card-top">
                      <div className="state-name-row">
                        <h3 className="state-card-name">{state.name}</h3>
                        <ChevronRight size={16} className="state-card-arrow" />
                      </div>
                      {state.slogan && (
                        <div className="state-card-slogan">"{state.slogan}"</div>
                      )}
                    </div>

                    <div className="state-card-meta">
                      <div className="meta-line">
                        <Building size={13} />
                        <span>Capital: <strong>{state.capital}</strong></span>
                      </div>
                      {state.majorCities && state.majorCities.length > 1 && (
                        <div className="meta-cities-line">
                          <span>Hubs: {state.majorCities.slice(0, 3).join(', ')}</span>
                        </div>
                      )}
                    </div>

                    <div className="state-card-footer">
                      <span className="state-stories-pill">
                        {state.articleCount > 0 ? `${state.articleCount} articles` : 'Verified Coverage'}
                      </span>
                      <span className="state-read-link">Explore News →</span>
                    </div>
                  </Link>
                ))}
              </div>
            </section>
          ))}

          {filteredGroups.length === 0 && (
            <div className="empty-state-box">
              <p>No states or cities matched your search query "{searchQuery}".</p>
              <button
                type="button"
                onClick={() => { setSearchQuery(''); setSelectedZone('All'); }}
                className="clear-filters-btn"
              >
                Reset Filters
              </button>
            </div>
          )}
        </div>
      </main>

      <Footer />
    </div>
  );
}
