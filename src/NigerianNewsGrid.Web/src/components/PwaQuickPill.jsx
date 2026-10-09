import React from 'react';
import { Download, X, Zap } from 'lucide-react';

export default function PwaQuickPill({ onInstall, onDismiss }) {
  return (
    <div className="pwa-quick-pill" role="banner" aria-label="Install App Banner">
      <div className="pwa-pill-content" onClick={onInstall}>
        <div className="pwa-pill-badge">
          <Zap size={14} />
          <span>APP</span>
        </div>
        <div className="pwa-pill-text">
          <strong>Install News Stand NG</strong>
          <span>• Zero data lag & offline reads</span>
        </div>
      </div>

      <div className="pwa-pill-actions">
        <button
          className="pwa-pill-install-btn"
          onClick={onInstall}
          aria-label="Install News Stand NG App"
        >
          <Download size={14} />
          <span>Install</span>
        </button>
        <button
          className="pwa-pill-close-btn"
          onClick={onDismiss}
          aria-label="Dismiss app install banner"
        >
          <X size={15} />
        </button>
      </div>
    </div>
  );
}
