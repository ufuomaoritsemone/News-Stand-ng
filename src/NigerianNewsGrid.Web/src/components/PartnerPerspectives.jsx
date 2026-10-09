import React, { useEffect } from 'react';
import { Megaphone, ExternalLink, ArrowRight, ShieldCheck, Sparkles } from 'lucide-react';
import { api } from '../services/api';

export default function PartnerPerspectives({
  stories = [],
  onSelectStory,
}) {
  const activeSponsors = stories.filter(
    (s) => s.isSponsored && (s.isActive !== false || !s.campaignExpiresAt || new Date(s.campaignExpiresAt) > new Date())
  );

  // Track impressions on mount for displayed sponsored stories
  useEffect(() => {
    activeSponsors.forEach((story) => {
      if (story?.id) {
        api.trackSponsoredImpression(story.id);
      }
    });
  }, [activeSponsors]);

  if (activeSponsors.length === 0) return null;

  return (
    <section className="partner-perspectives-section app-container" aria-label="Partner Perspectives and Corporate Briefings">
      <div className="partner-perspectives-container">
        {/* Centralized Section Header */}
        <div className="partner-perspectives-header partner-header-centralized">
          <div className="partner-kicker">
            <Sparkles size={14} className="partner-sparkle" />
            <span>PARTNER PERSPECTIVES</span>
          </div>
          <h3 className="partner-title">Corporate Briefings &amp; Special Reports</h3>
          <p className="partner-subtitle">
            Curated press releases, executive insights, and verified corporate announcements
          </p>
          <div className="partner-disclosure-wrapper">
            <span className="partner-disclosure-badge">
              <ShieldCheck size={13} />
              <span>Verified Commercial Partner Content</span>
            </span>
          </div>
        </div>

        {/* Sponsored Stories Spotlight Shelf */}
        <div className="partner-shelf-grid">
          {activeSponsors.map((story) => (
            <article
              key={story.id}
              className="partner-shelf-card"
              onClick={() => {
                api.trackSponsoredClick(story.id);
                if (onSelectStory) onSelectStory(story);
              }}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  api.trackSponsoredClick(story.id);
                  if (onSelectStory) onSelectStory(story);
                }
              }}
              aria-label={`Sponsored: ${story.title}`}
            >
              {/* Media Image */}
              <div className="partner-shelf-media">
                <img
                  src={story.imageUrl || '/logo.png'}
                  alt={story.title}
                  className="partner-shelf-img"
                  loading="lazy"
                  onError={(e) => {
                    e.target.src = 'https://images.unsplash.com/photo-1559526324-4b87b5e36e44?w=800&auto=format&fit=crop&q=80';
                  }}
                />
                <div className="partner-shelf-badge">
                  <Megaphone size={12} />
                  <span>Presented by {story.sponsorName || 'Partner'}</span>
                </div>
              </div>

              {/* Body */}
              <div className="partner-shelf-body">
                <div className="partner-shelf-kicker">
                  <span className="partner-brand-name">{story.sponsorName || 'Corporate Partner'}</span>
                  <span className="meta-dot">•</span>
                  <span className="partner-studio-label">Partner Studio</span>
                </div>

                <h4 className="partner-shelf-headline">{story.title}</h4>

                {story.summary && (
                  <p className="partner-shelf-excerpt">{story.summary}</p>
                )}

                {/* Actions */}
                <div className="partner-shelf-actions">
                  <button
                    type="button"
                    className="partner-read-btn"
                    onClick={(e) => {
                      e.stopPropagation();
                      api.trackSponsoredClick(story.id);
                      if (onSelectStory) onSelectStory(story);
                    }}
                  >
                    <span>Read Full Coverage</span>
                    <ArrowRight size={14} />
                  </button>

                  {story.sponsorUrl && (
                    <a
                      href={story.sponsorUrl}
                      target="_blank"
                      rel="sponsored nofollow noopener"
                      className="partner-outbound-link"
                      onClick={(e) => {
                        e.stopPropagation();
                        api.trackSponsoredClick(story.id);
                      }}
                      title={`Visit ${story.sponsorName || 'Partner'} website`}
                    >
                      <span>Visit Sponsor</span>
                      <ExternalLink size={12} />
                    </a>
                  )}
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
