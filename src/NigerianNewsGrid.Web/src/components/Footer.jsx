import React from 'react';

export default function Footer({ onSelectCategory }) {
  const currentYear = new Date().getFullYear();

  return (
    <footer className="site-footer" role="contentinfo">
      <div className="app-container">
        <div className="footer-top">
          <div style={{ maxWidth: 420 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
              <img src="/logo.png" alt="Nigerian News Grid" style={{ width: 32, height: 32, borderRadius: 8 }} />
              <span style={{ fontSize: '1.2rem', fontWeight: 800, letterSpacing: '-0.02em' }}>
                NIGERIAN <span style={{ color: 'var(--emerald-bright)' }}>NEWS GRID</span>
              </span>
            </div>
            <p style={{ fontSize: '0.86rem', color: 'var(--text-muted)', lineHeight: 1.6 }}>
              A high-velocity national news aggregator delivering verified headlines, in-depth reports, and video broadcasts across Nigeria’s premier publishers and investigative desks.
            </p>
          </div>

          <div>
            <div style={{ fontSize: '0.85rem', fontWeight: 700, color: 'var(--text-primary)', marginBottom: 12 }}>
              EDITORIAL BEATS
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8, fontSize: '0.84rem', color: 'var(--text-muted)' }}>
              {['Politics', 'Business', 'Technology', 'Sports', 'Entertainment', 'Opinion'].map((cat) => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => onSelectCategory(cat)}
                  style={{ textAlign: 'left', color: 'inherit', transition: 'color 0.15s' }}
                  onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--emerald-bright)')}
                  onMouseLeave={(e) => (e.currentTarget.style.color = 'inherit')}
                >
                  {cat}
                </button>
              ))}
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.85rem', fontWeight: 700, color: 'var(--text-primary)', marginBottom: 12 }}>
              SOURCES & COVERAGE
            </div>
            <p style={{ fontSize: '0.84rem', color: 'var(--text-muted)', maxWidth: 260, lineHeight: 1.6 }}>
              Aggregating verified feeds from Punch, Vanguard, The Nation, BusinessDay, Daily Post, Channels TV, TVC News, and Arise News.
            </p>
          </div>
        </div>

        <div className="footer-bottom">
          <div>
            © {currentYear} Nigerian News Grid. All rights reserved.
          </div>
          <div style={{ display: 'flex', gap: 16 }}>
            <span>Verified RSS Ingestion</span>
            <span>·</span>
            <span>BM25 Full-Text Search</span>
            <span>·</span>
            <span>Lagos · Abuja · Port Harcourt</span>
          </div>
        </div>
      </div>
    </footer>
  );
}
