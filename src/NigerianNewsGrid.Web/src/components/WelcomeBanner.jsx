import React from 'react';
import { getNigerianDateBanner } from '../services/timeUtils';
import { Radio, ShieldCheck } from 'lucide-react';

export default function WelcomeBanner({
  brandName = 'NEWS STAND NG',
  storyCount = null,
  activeCategory = 'All',
}) {
  const dateStr = getNigerianDateBanner();

  return (
    <section className="welcome-banner-section app-container" aria-label="Newsroom status">
      <div className="welcome-banner-card welcome-banner-card--compact">
        <div className="welcome-masthead-main">
          <div className="welcome-eyebrow-row">
            <div className="live-status-pill">
              <span className="live-status-dot" />
              <Radio size={12} style={{ marginRight: 4 }} />
              <span>LIVE NATIONAL DESK</span>
            </div>
            <span className="welcome-date-text">{dateStr}</span>
            <div className="verified-badge-chip">
              <ShieldCheck size={13} style={{ color: 'var(--brand-green)', marginRight: 4 }} />
              <span>Verified Aggregation</span>
            </div>
          </div>

          <h1 className="welcome-headline">
            {activeCategory && activeCategory !== 'All' ? (
              <>
                {activeCategory} News in Nigeria —{' '}
                <span className="welcome-highlight highlight-inspiration">Direct</span> &{' '}
                <span className="welcome-highlight highlight-knowledge">Verified</span>
              </>
            ) : (
              <>
                Real-Time Nigerian News,{' '}
                <span className="welcome-highlight highlight-inspiration">Direct</span> &{' '}
                <span className="welcome-highlight highlight-knowledge">Timely</span>
              </>
            )}
          </h1>
        </div>
      </div>
    </section>
  );
}
