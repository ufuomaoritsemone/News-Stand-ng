import React, { useState, useEffect } from 'react';
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
  Flame,
  ArrowLeftRight,
  Coins
} from 'lucide-react';

const DEFAULT_CURRENCIES = {
  USD: {
    currencyCode: 'USD',
    currencyName: 'US Dollar',
    flag: '🇺🇸',
    symbol: '$',
    buyRate: 0.0,
    sellRate: 0.0,
    officialBuyRate: 0.0,
    officialSellRate: 0.0,
    change24h: '0.00%',
  },
  EUR: {
    currencyCode: 'EUR',
    currencyName: 'Euro',
    flag: '🇪🇺',
    symbol: '€',
    buyRate: 0.0,
    sellRate: 0.0,
    officialBuyRate: 0.0,
    officialSellRate: 0.0,
    change24h: '0.00%',
  },
  GBP: {
    currencyCode: 'GBP',
    currencyName: 'British Pound',
    flag: '🇬🇧',
    symbol: '£',
    buyRate: 0.0,
    sellRate: 0.0,
    officialBuyRate: 0.0,
    officialSellRate: 0.0,
    change24h: '0.00%',
  },
  JPY: {
    currencyCode: 'JPY',
    currencyName: 'Japanese Yen',
    flag: '🇯🇵',
    symbol: '¥',
    buyRate: 0.0,
    sellRate: 0.0,
    officialBuyRate: 0.0,
    officialSellRate: 0.0,
    change24h: '0.00%',
  },
};

const OFFLINE_FALLBACK_METRIC = {
  title: 'Naira Exchange Rate (USD/NGN)',
  primaryValue: '₦0.00 / $1',
  secondaryValue: '₦0.00 Official NAFEM',
  change24h: '0.00%',
  sourceAttribution: 'AbokiForex & Central Bank of Nigeria (CBN)',
  lastUpdated: null,
  historicalPoints: [],
  faqs: [],
  currencies: DEFAULT_CURRENCIES,
};

function CurrencyCalculatorCard({ currency, rateMode, isPidgin }) {
  const isOfficial = rateMode === 'official';
  const effectiveBuyRate = isOfficial
    ? (currency.officialBuyRate ?? currency.officialBuy ?? currency.officialRate ?? currency.officialSellRate ?? 0)
    : (currency.buyRate ?? currency.parallelBuy ?? currency.sellRate ?? 0);
  const effectiveSellRate = isOfficial
    ? (currency.officialSellRate ?? currency.officialSell ?? currency.officialRate ?? currency.officialBuyRate ?? 0)
    : (currency.sellRate ?? currency.parallelSell ?? currency.buyRate ?? 0);

  const currCode = currency.currencyCode || currency.code || 'FX';
  const currName = currency.currencyName || currency.name || currCode;
  const currSymbol = currency.symbol || '$';
  const currFlag = currency.flag || '🌐';

  const defaultAmt = currCode === 'JPY' ? 10000 : 100;
  const [foreignAmount, setForeignAmount] = useState(defaultAmt.toString());
  const [ngnAmount, setNgnAmount] = useState((defaultAmt * effectiveBuyRate).toFixed(2));
  const [lastEdited, setLastEdited] = useState('foreign'); // 'foreign' | 'ngn'

  // Recalculate whenever rateMode or effective rates change
  useEffect(() => {
    if (lastEdited === 'foreign') {
      const fNum = parseFloat(foreignAmount.toString().replace(/,/g, ''));
      if (!isNaN(fNum) && fNum >= 0) {
        setNgnAmount((fNum * effectiveBuyRate).toFixed(2));
      }
    } else {
      const nNum = parseFloat(ngnAmount.toString().replace(/,/g, ''));
      if (!isNaN(nNum) && nNum >= 0) {
        setForeignAmount(effectiveSellRate > 0 ? (nNum / effectiveSellRate).toFixed(2) : '0.00');
      }
    }
  }, [rateMode, effectiveBuyRate, effectiveSellRate]);

  const handleForeignChange = (e) => {
    const rawVal = e.target.value;
    setForeignAmount(rawVal);
    setLastEdited('foreign');
    const sanitized = rawVal.replace(/,/g, '');
    const num = parseFloat(sanitized);
    if (!isNaN(num) && num >= 0) {
      setNgnAmount((num * effectiveBuyRate).toFixed(2));
    } else if (rawVal === '') {
      setNgnAmount('');
    }
  };

  const handleNgnChange = (e) => {
    const rawVal = e.target.value;
    setNgnAmount(rawVal);
    setLastEdited('ngn');
    const sanitized = rawVal.replace(/,/g, '');
    const num = parseFloat(sanitized);
    if (!isNaN(num) && num >= 0 && effectiveSellRate > 0) {
      setForeignAmount((num / effectiveSellRate).toFixed(2));
    } else if (rawVal === '') {
      setForeignAmount('');
    }
  };

  const handlePresetClick = (amount) => {
    setForeignAmount(amount.toString());
    setLastEdited('foreign');
    setNgnAmount((amount * effectiveBuyRate).toFixed(2));
  };

  const presets = currCode === 'JPY'
    ? [1000, 5000, 10000, 50000]
    : [50, 100, 500, 1000];

  const spread = Math.abs(effectiveSellRate - effectiveBuyRate);

  return (
    <div className="currency-calc-card">
      <div className="calc-card-header">
        <div className="calc-currency-identity">
          <span className="calc-flag" aria-hidden="true">{currFlag}</span>
          <div className="calc-title-group">
            <div className="calc-currency-title">
              <span className="calc-code">{currCode}</span>
              <span className="calc-name">{currName}</span>
            </div>
            <div className="calc-rate-badge-row">
              <span className="calc-rate-stat">
                Buy: <strong>₦{effectiveBuyRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong>
              </span>
              <span className="calc-stat-divider">·</span>
              <span className="calc-rate-stat">
                Sell: <strong>₦{effectiveSellRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong>
              </span>
              {spread > 0 && (
                <span className="calc-spread-tag">Spread ₦{spread.toFixed(2)}</span>
              )}
            </div>
          </div>
        </div>
        {currency.change24h && (
          <span className={`calc-change-badge ${currency.change24h.startsWith('-') ? 'negative' : 'positive'}`}>
            {currency.change24h}
          </span>
        )}
      </div>

      <div className="calc-dual-converter">
        <div className="calc-input-block">
          <label className="calc-input-label" htmlFor={`input-${currCode}`}>
            <span>{currName} ({currSymbol})</span>
            {lastEdited === 'foreign' && <span className="active-flow-indicator">Source</span>}
          </label>
          <div className="calc-input-wrap">
            <span className="calc-input-symbol">{currSymbol}</span>
            <input
              id={`input-${currCode}`}
              type="number"
              step="any"
              min="0"
              className="calc-input-field"
              value={foreignAmount}
              onChange={handleForeignChange}
              placeholder="0.00"
            />
          </div>
        </div>

        <div className="calc-exchange-arrow" title="Two-way conversion">
          <ArrowLeftRight size={18} />
        </div>

        <div className="calc-input-block">
          <label className="calc-input-label" htmlFor={`input-ngn-${currCode}`}>
            <span>Nigerian Naira (₦)</span>
            {lastEdited === 'ngn' && <span className="active-flow-indicator">Source</span>}
          </label>
          <div className="calc-input-wrap">
            <span className="calc-input-symbol">₦</span>
            <input
              id={`input-ngn-${currCode}`}
              type="text"
              className="calc-input-field"
              value={ngnAmount}
              onChange={handleNgnChange}
              placeholder="0.00"
            />
          </div>
        </div>
      </div>

      <div className="calc-meta-footer">
        <div className="calc-formula-caption">
          {lastEdited === 'foreign' ? (
            <span>
              Converting to Naira via <strong>Buy Rate</strong> (1 {currCode} = ₦{effectiveBuyRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })})
            </span>
          ) : (
            <span>
              Buying {currCode} via <strong>Sell Rate</strong> (₦{effectiveSellRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} = 1 {currCode})
            </span>
          )}
        </div>

        <div className="calc-presets-pills">
          <span className="presets-label">Quick:</span>
          {presets.map((p) => (
            <button
              key={`${currCode}-preset-${p}`}
              type="button"
              className="calc-preset-btn"
              onClick={() => handlePresetClick(p)}
            >
              {currSymbol}{p.toLocaleString()}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

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
      metric: metric || OFFLINE_FALLBACK_METRIC,
      articles: Array.isArray(relatedNews) ? relatedNews : (relatedNews?.articles || []),
      isPidgin,
    };
  } catch (err) {
    console.warn('[Naira Loader] Offline fallback (0) engaged:', err.message);
    return {
      metric: OFFLINE_FALLBACK_METRIC,
      articles: [],
      isPidgin,
    };
  }
}

export default function NairaExchangeRatePage() {
  const { metric: initialMetric, articles, isPidgin } = useLoaderData();
  const [rateMode, setRateMode] = useState('parallel'); // 'parallel' | 'official'
  const [isOnline, setIsOnline] = useState(() => (typeof navigator !== 'undefined' ? navigator.onLine : true));
  const [liveMetric, setLiveMetric] = useState(initialMetric);

  useEffect(() => {
    setLiveMetric(initialMetric);
  }, [initialMetric]);

  useEffect(() => {
    if (typeof window === 'undefined') return undefined;

    const refreshLiveFx = async () => {
      try {
        const fresh = await api.getMarketMetric('naira-fx');
        if (fresh) {
          setLiveMetric(fresh);
        }
      } catch {
        setLiveMetric(OFFLINE_FALLBACK_METRIC);
      }
    };

    const handleOnline = () => {
      setIsOnline(true);
      refreshLiveFx();
    };

    const handleOffline = () => {
      setIsOnline(false);
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    if (navigator.onLine && (!initialMetric?.lastUpdated || initialMetric?.primaryValue === '₦0.00 / $1')) {
      refreshLiveFx();
    }

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [initialMetric]);

  const metric = isOnline ? (liveMetric || OFFLINE_FALLBACK_METRIC) : OFFLINE_FALLBACK_METRIC;

  const currencyCodes = ['USD', 'EUR', 'GBP', 'JPY'];
  const currencyList = currencyCodes.map((code) => {
    const def = DEFAULT_CURRENCIES[code] || {};
    const fromApi = isOnline ? (metric?.currencies?.[code] || metric?.currencies?.[code.toLowerCase()] || {}) : {};
    return {
      currencyCode: code,
      code,
      currencyName: fromApi.name || fromApi.currencyName || def.currencyName || code,
      flag: def.flag || '🌐',
      symbol: fromApi.symbol || def.symbol || '$',
      buyRate: fromApi.parallelBuy ?? fromApi.buyRate ?? def.buyRate ?? 0,
      sellRate: fromApi.parallelSell ?? fromApi.sellRate ?? def.sellRate ?? 0,
      officialBuyRate: fromApi.officialBuy ?? fromApi.officialBuyRate ?? fromApi.officialRate ?? def.officialBuyRate ?? 0,
      officialSellRate: fromApi.officialSell ?? fromApi.officialSellRate ?? fromApi.officialRate ?? def.officialSellRate ?? 0,
      change24h: fromApi.change24h || def.change24h || '0.00%',
    };
  });

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
  const minVal = points.length > 0 ? Math.min(...points.map(p => p.secondaryValue || p.primaryValue)) - 30 : 1300;
  const maxVal = points.length > 0 ? Math.max(...points.map(p => p.primaryValue)) + 30 : 1450;
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
                <span className="rate-pill-change">{metric.change24h || '0.00%'}</span>
              </div>
              <div className="rate-card-value">{metric.primaryValue || '₦0.00 / $1'}</div>
              <div className="rate-card-sub">Cash rate across Lagos, Abuja, & Kano Bureau de Change hubs</div>
            </div>

            <div className="rate-card official-rate-card">
              <div className="rate-card-label">
                <span>Official Window (NAFEM / CBN)</span>
                <span className="rate-pill-official">CBN Model</span>
              </div>
              <div className="rate-card-value">{metric.secondaryValue || '₦0.00 Official NAFEM'}</div>
              <div className="rate-card-sub">Willing-buyer, willing-seller order-book rate via commercial banks</div>
            </div>
          </div>

          {/* Interactive Currency Calculators Section */}
          <div className="fx-calculators-section">
            <div className="fx-calculators-toolbar">
              <div className="fx-toolbar-text">
                <div className="fx-toolbar-heading-row">
                  <Coins size={20} className="fx-heading-icon" />
                  <h2 className="fx-toolbar-title">
                    {isPidgin ? 'Major Currency Two-Way Calculators' : 'Live Currency Calculators (USD, EUR, GBP, JPY)'}
                  </h2>
                </div>
                <p className="fx-toolbar-desc">
                  {isPidgin
                    ? 'Put amount inside any side to calculate quick-quick. Change between parallel street rate and CBN official rate below.'
                    : 'Interactive two-way FX calculators. Enter an amount in either currency to convert instantly using verified exchange rates.'}
                </p>
              </div>

              <div className="fx-rate-mode-toggle" role="group" aria-label="Select Exchange Rate Source">
                <button
                  type="button"
                  className={`fx-mode-btn ${rateMode === 'parallel' ? 'active parallel' : ''}`}
                  onClick={() => setRateMode('parallel')}
                  aria-pressed={rateMode === 'parallel'}
                >
                  <span className="fx-mode-dot parallel-dot" />
                  <span>Parallel Market (Street / BDC)</span>
                </button>
                <button
                  type="button"
                  className={`fx-mode-btn ${rateMode === 'official' ? 'active official' : ''}`}
                  onClick={() => setRateMode('official')}
                  aria-pressed={rateMode === 'official'}
                >
                  <span className="fx-mode-dot official-dot" />
                  <span>Official CBN (NAFEM)</span>
                </button>
              </div>
            </div>

            <div className="currency-calculators-grid">
              {currencyList.map((curr, idx) => (
                <CurrencyCalculatorCard
                  key={curr.currencyCode || curr.code || `currency-${idx}`}
                  currency={curr}
                  rateMode={rateMode}
                  isPidgin={isPidgin}
                />
              ))}
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
                <details key={faq.id || faq.question || `faq-${idx}`} className="story-faq-item" open={idx === 0}>
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
              {articles.map((art, idx) => (
                <Link key={art.id || art.slug || `related-art-${idx}`} to={`/news/${art.slug || art.id}`} className="related-story-card">
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
