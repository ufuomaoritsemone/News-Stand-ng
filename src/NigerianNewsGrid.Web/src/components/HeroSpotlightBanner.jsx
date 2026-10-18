import React, { useEffect } from 'react';
import { Megaphone, ExternalLink, ArrowRight, Sparkles } from 'lucide-react';
import { api } from '../services/api';

export default function HeroSpotlightBanner({
  campaign,
  onSelectStory,
}) {
  useEffect(() => {
    if (campaign?.id) {
      api.trackSponsoredImpression(campaign.id);
    }
  }, [campaign?.id]);

  if (!campaign) return null;

  const handleSelect = (e) => {
    e.preventDefault();
    if (campaign.id) {
      api.trackSponsoredClick(campaign.id);
    }
    if (onSelectStory) {
      onSelectStory(campaign);
    }
  };

  const handleOutbound = (e) => {
    e.stopPropagation();
    if (campaign.id) {
      api.trackSponsoredClick(campaign.id);
    }
  };

  return (
    <div className="hero-spotlight-wrapper app-container" role="region" aria-label="Sponsored Partner Spotlight">
      <div className="hero-spotlight-card" onClick={handleSelect}>
        <div className="hero-spotlight-content">
          <div className="hero-spotlight-badge-row">
            <span className="hero-spotlight-pill">
              <Sparkles size={13} className="hero-spotlight-icon" />
              <span>PARTNER SPOTLIGHT</span>
            </span>
            <span className="hero-spotlight-sponsor">
              Presented by <strong>{campaign.sponsorName || 'Corporate Partner'}</strong>
            </span>
          </div>

          <h3 className="hero-spotlight-title">{campaign.title}</h3>

          {campaign.summary && (
            <p className="hero-spotlight-summary">{campaign.summary}</p>
          )}

          <div className="hero-spotlight-actions">
            <button
              type="button"
              className="hero-spotlight-btn primary"
              onClick={handleSelect}
            >
              <span>Explore Briefing</span>
              <ArrowRight size={15} />
            </button>

            {campaign.sponsorUrl && (
              <a
                href={campaign.sponsorUrl}
                target="_blank"
                rel="sponsored noopener noreferrer"
                className="hero-spotlight-btn secondary"
                onClick={handleOutbound}
              >
                <span>Visit {campaign.sponsorName || 'Partner'}</span>
                <ExternalLink size={14} />
              </a>
            )}
          </div>
        </div>

        {campaign.imageUrl && (
          <div className="hero-spotlight-media">
            <img
              src={campaign.imageUrl}
              alt={campaign.title}
              className="hero-spotlight-img"
              loading="lazy"
              onError={(e) => {
                e.target.style.display = 'none';
              }}
            />
          </div>
        )}
      </div>
    </div>
  );
}
