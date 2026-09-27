import React from 'react';

export default function WelcomeBanner({ brandName = 'BULETIN' }) {
  return (
    <section className="welcome-banner-section app-container" aria-label="Welcome announcement">
      <div className="welcome-banner-card">
        <span className="welcome-eyebrow">WELCOME TO {brandName}</span>
        <h1 className="welcome-headline">
          Craft narratives ✍️ that ignite{' '}
          <span className="welcome-highlight highlight-inspiration">inspiration 💡</span>,{' '}
          <span className="welcome-highlight highlight-knowledge">knowledge 📕</span>, and{' '}
          <span className="welcome-highlight highlight-entertainment">entertainment 🎬</span>
        </h1>
      </div>
    </section>
  );
}
