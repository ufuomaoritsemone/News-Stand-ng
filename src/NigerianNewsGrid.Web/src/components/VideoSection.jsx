import React, { useState } from 'react';
import { Play, X, ExternalLink, Flame, Tv, Eye, Clock } from 'lucide-react';

export default function VideoSection({
  title = 'Featured Videos',
  subtitle = 'Watch live reports, analyses, and breaking updates from top news broadcasters',
  type = 'trending', // 'trending' | 'news'
  videos = [],
  loading = false,
}) {
  const [selectedVideo, setSelectedVideo] = useState(null);

  // Helper to extract clean embed URL
  const getEmbedUrl = (vid) => {
    if (!vid) return null;
    const videoId = vid.videoId || vid.id;
    if (videoId && videoId.length === 11) {
      return `https://www.youtube.com/embed/${videoId}?autoplay=1&rel=0`;
    }
    if (vid.videoUrl || vid.url) {
      const match = (vid.videoUrl || vid.url).match(/(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=))([\w-]{11})/);
      if (match && match[1]) {
        return `https://www.youtube.com/embed/${match[1]}?autoplay=1&rel=0`;
      }
    }
    return null;
  };

  const getWatchUrl = (vid) => {
    if (vid.videoUrl) return vid.videoUrl;
    if (vid.videoId) return `https://www.youtube.com/watch?v=${vid.videoId}`;
    return 'https://www.youtube.com';
  };

  const isTrending = type === 'trending';

  return (
    <section className="video-section app-container" aria-label={title}>
      {/* Header */}
      <div className="video-section-header">
        <div className="video-section-heading">
          <div className="video-section-title-wrap">
            {isTrending ? (
              <span className="video-type-badge trending-badge">
                <Flame size={16} />
                <span>YouTube Trending</span>
              </span>
            ) : (
              <span className="video-type-badge broadcast-badge">
                <span className="live-pulse-dot" />
                <Tv size={15} />
                <span>Broadcast News</span>
              </span>
            )}
            <h2 className="video-section-title">{title}</h2>
          </div>
          <p className="video-section-subtitle">{subtitle}</p>
        </div>
      </div>

      {/* Grid of Video Cards */}
      {loading ? (
        <div className="video-cards-grid">
          {[1, 2, 3, 4, 5, 6].map((n) => (
            <div key={n} className="video-card-skeleton skeleton" style={{ minHeight: 280, borderRadius: 12 }} />
          ))}
        </div>
      ) : videos.length === 0 ? (
        <div className="video-empty-state">
          <Tv size={40} style={{ color: 'var(--text-muted)', marginBottom: 12 }} />
          <h3>No video stories found</h3>
          <p>Check back shortly as latest YouTube broadcasts sync in real time.</p>
        </div>
      ) : (
        <div className="video-cards-grid">
          {videos.map((vid, idx) => (
            <article
              key={vid.id || vid.videoId || idx}
              className="video-grid-card"
              onClick={() => setSelectedVideo(vid)}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => e.key === 'Enter' && setSelectedVideo(vid)}
              aria-label={`Play video: ${vid.title}`}
            >
              {/* Thumbnail Container */}
              <div className="video-thumb-container">
                <img
                  src={vid.thumbnailUrl || `https://i.ytimg.com/vi/${vid.videoId}/hqdefault.jpg`}
                  alt={vid.title}
                  className="video-thumb-img"
                  loading="lazy"
                  onError={(e) => {
                    e.target.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=600&q=80';
                  }}
                />

                {/* Duration Badge */}
                {vid.duration && (
                  <span className="video-duration-pill">
                    <Clock size={11} />
                    {vid.duration}
                  </span>
                )}

                {/* Trending Rank Badge */}
                {vid.trendingRank && (
                  <span className="video-rank-badge">
                    #{vid.trendingRank} Trending
                  </span>
                )}

                {/* Play Button Overlay */}
                <div className="video-play-overlay">
                  <div className="video-play-disc">
                    <Play size={22} fill="#FFFFFF" color="#FFFFFF" style={{ marginLeft: 3 }} />
                  </div>
                </div>
              </div>

              {/* Card Meta & Title */}
              <div className="video-card-details">
                <div className="video-source-row">
                  <span className="video-channel-name">{vid.channelName || 'YouTube News'}</span>
                  {vid.viewCount && (
                    <span className="video-views-count">
                      <Eye size={12} /> {vid.viewCount}
                    </span>
                  )}
                </div>

                <h3 className="video-card-title">{vid.title}</h3>

                {vid.summary && (
                  <p className="video-card-summary">{vid.summary}</p>
                )}

                <div className="video-card-actions">
                  <span className="video-play-action">
                    <Play size={13} fill="currentColor" /> Watch Now
                  </span>
                  <a
                    href={getWatchUrl(vid)}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="video-yt-link"
                    onClick={(e) => e.stopPropagation()}
                    title="Open on YouTube"
                  >
                    <span>YouTube</span>
                    <ExternalLink size={12} />
                  </a>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}

      {/* Embedded YouTube Modal Player */}
      {selectedVideo && (
        <div
          className="search-overlay video-modal-overlay"
          onClick={() => setSelectedVideo(null)}
          role="dialog"
          aria-modal="true"
          aria-label={selectedVideo.title}
        >
          <div
            className="video-modal-dialog"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Top Bar */}
            <div className="video-modal-header">
              <div style={{ flex: 1, minWidth: 0 }}>
                <h3 className="video-modal-title">{selectedVideo.title}</h3>
                <span className="video-modal-channel">{selectedVideo.channelName || 'YouTube News'}</span>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <a
                  href={getWatchUrl(selectedVideo)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="primary-btn"
                  style={{ padding: '6px 14px', fontSize: '0.8rem' }}
                >
                  <span>Open on YouTube</span>
                  <ExternalLink size={14} />
                </a>

                <button
                  type="button"
                  className="icon-btn"
                  onClick={() => setSelectedVideo(null)}
                  title="Close player"
                  aria-label="Close video player"
                  style={{ background: 'var(--bg-elevated)' }}
                >
                  <X size={18} />
                </button>
              </div>
            </div>

            {/* Video Player Frame */}
            <div className="video-modal-frame-wrap">
              <iframe
                src={getEmbedUrl(selectedVideo)}
                title={selectedVideo.title}
                allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                allowFullScreen
                className="video-modal-iframe"
              />
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
