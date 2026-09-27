import React, { useState, useEffect } from 'react';
import { Flame, ChevronRight } from 'lucide-react';

export default function BreakingTicker({ items = [], onSelectStory }) {
  const [currentIndex, setCurrentIndex] = useState(0);

  useEffect(() => {
    if (!items || items.length <= 1) return;
    const interval = setInterval(() => {
      setCurrentIndex((prev) => (prev + 1) % items.length);
    }, 6000);
    return () => clearInterval(interval);
  }, [items]);

  if (!items || items.length === 0) {
    return null;
  }

  const current = items[currentIndex] || items[0];

  return (
    <section className="breaking-ticker-bar" aria-label="Breaking news updates">
      <div className="app-container">
        <div className="ticker-inner">
          <div className="ticker-label">
            <span className="pulse-dot" />
            <Flame size={13} style={{ marginRight: 2 }} />
            BREAKING
          </div>

          <div className="ticker-content" role="status" aria-live="polite">
            <button
              type="button"
              className="ticker-headline"
              onClick={() => onSelectStory(current)}
              title="Click to read breaking story"
            >
              <span>{current.title}</span>
              <span className="ticker-source">· {current.source || 'National'}</span>
              <ChevronRight size={14} style={{ color: 'var(--emerald-bright)' }} />
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
