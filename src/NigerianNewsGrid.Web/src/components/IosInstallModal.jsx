import React from 'react';
import { Share, PlusSquare, X, Smartphone, Zap, WifiOff, Bell } from 'lucide-react';

export default function IosInstallModal({ isOpen, onClose }) {
  if (!isOpen) return null;

  return (
    <div className="ios-install-backdrop" onClick={onClose}>
      <div
        className="ios-install-sheet"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label="Install News Stand NG on iOS"
      >
        <button
          className="ios-install-close"
          onClick={onClose}
          aria-label="Close installation guide"
        >
          <X size={20} />
        </button>

        <div className="ios-install-header">
          <div className="ios-install-app-icon">
            <img src="/icon-192.png" alt="News Stand NG" width={52} height={52} />
          </div>
          <div>
            <h3 className="ios-install-title">Install News Stand NG</h3>
            <p className="ios-install-subtitle">Fast, light & offline-capable web app</p>
          </div>
        </div>

        <div className="ios-install-perks">
          <div className="ios-perk-item">
            <Zap size={15} className="ios-perk-icon" />
            <span>Instant launch (zero storage bloat)</span>
          </div>
          <div className="ios-perk-item">
            <WifiOff size={15} className="ios-perk-icon" />
            <span>Read saved bookmarks offline</span>
          </div>
          <div className="ios-perk-item">
            <Bell size={15} className="ios-perk-icon" />
            <span>Breaking news & Naira FX alerts</span>
          </div>
        </div>

        <div className="ios-install-steps">
          <div className="ios-step">
            <span className="ios-step-number">1</span>
            <div className="ios-step-text">
              Tap the <strong>Share button</strong> in Safari's bottom navigation bar.
            </div>
            <div className="ios-step-visual">
              <Share size={18} />
            </div>
          </div>

          <div className="ios-step">
            <span className="ios-step-number">2</span>
            <div className="ios-step-text">
              Scroll down and tap <strong>Add to Home Screen</strong>.
            </div>
            <div className="ios-step-visual">
              <PlusSquare size={18} />
            </div>
          </div>

          <div className="ios-step">
            <span className="ios-step-number">3</span>
            <div className="ios-step-text">
              Tap <strong>Add</strong> in the top-right corner to finish.
            </div>
            <div className="ios-step-visual">
              <span style={{ fontWeight: 700, fontSize: '0.85rem', color: '#008751' }}>Add</span>
            </div>
          </div>
        </div>

        <button
          className="ios-install-done-btn"
          onClick={() => {
            try {
              if (!localStorage.getItem('newsstand_pwa_install_tracked')) {
                import('../services/api').then(({ api }) => {
                  api.trackPwaEvent('pwa_install', { source: 'ios_safari_guide', platform: 'Web-PWA-iOS' });
                  localStorage.setItem('newsstand_pwa_install_tracked', 'true');
                });
              }
            } catch {}
            onClose();
          }}
        >
          Got It
        </button>
      </div>
    </div>
  );
}
