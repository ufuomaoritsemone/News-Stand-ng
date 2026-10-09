import React from 'react';
import { Download, Zap, WifiOff, Bell, Sparkles } from 'lucide-react';

export default function PwaInstallFeedCard({ onInstall }) {
  return (
    <article className="buletin-card pwa-feed-promo-card">
      <div className="pwa-feed-header">
        <span className="pwa-feed-badge">
          <Sparkles size={12} />
          <span>FAST WEB APP</span>
        </span>
        <span className="pwa-feed-size">Less than 1 MB</span>
      </div>

      <div className="pwa-feed-body">
        <h3 className="pwa-feed-title">
          Experience News Stand NG without browser borders
        </h3>
        <p className="pwa-feed-desc">
          Add News Stand directly to your home screen for instantaneous loading, offline reading of saved stories, and daily parallel FX rates.
        </p>

        <ul className="pwa-feed-perks">
          <li>
            <Zap size={15} className="pwa-feed-perk-icon" />
            <span>Zero App Store download — installs in 2 seconds</span>
          </li>
          <li>
            <WifiOff size={15} className="pwa-feed-perk-icon" />
            <span>Read bookmarked stories even without an active data connection</span>
          </li>
          <li>
            <Bell size={15} className="pwa-feed-perk-icon" />
            <span>Instant breaking alerts & live Naira exchange rate tracker</span>
          </li>
        </ul>
      </div>

      <div className="pwa-feed-footer">
        <button
          className="pwa-feed-install-btn"
          onClick={onInstall}
          aria-label="Install News Stand NG Web App"
        >
          <Download size={16} />
          <span>Install Web App</span>
        </button>
      </div>
    </article>
  );
}
