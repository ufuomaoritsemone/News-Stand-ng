import React, { useState } from 'react';
import { Play, X, Tv } from 'lucide-react';

export default function VideoReel({ videos = [] }) {
  const [activeVideo, setActiveVideo] = useState(null);

  if (!videos || videos.length === 0) return null;

  // Extract YouTube ID if present
  const getEmbedUrl = (url) => {
    if (!url) return null;
    const regExp = /^.*(youtu.be\/|v\/|u\/\w\/|embed\/|watch\?v=|&v=)([^#&?]*).*/;
    const match = url.match(regExp);
    return match && match[2].length === 11
      ? `https://www.youtube.com/embed/${match[2]}?autoplay=1`
      : url;
  };

  return (
    <section className="video-section app-container" aria-label="Video News Stories">
      <div className="section-header">
        <div>
          <h2 className="section-title">
            <Tv size={22} style={{ color: 'var(--danger)' }} />
            Video Bulletins & Broadcasts
          </h2>
          <div className="section-subtitle">
            Curated daily broadcast reports from Channels TV, Arise News & TVC News
          </div>
        </div>
      </div>

      <div className="video-reel">
        {videos.slice(0, 8).map((vid) => (
          <div
            key={vid.id || vid.url}
            className="video-card"
            onClick={() => setActiveVideo(vid)}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => e.key === 'Enter' && setActiveVideo(vid)}
          >
            <div className="video-thumb-wrapper">
              <img
                src={vid.thumbnailUrl || 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=600&q=80'}
                alt={vid.title}
                className="video-thumb"
                loading="lazy"
              />
              <div className="play-overlay-btn">
                <div className="play-icon-circle">
                  <Play size={20} fill="#FFFFFF" />
                </div>
              </div>
            </div>
            <div className="video-info">
              <h3 className="video-title">{vid.title}</h3>
              <div className="video-channel">{vid.channelTitle || vid.source || 'News Broadcast'}</div>
            </div>
          </div>
        ))}
      </div>

      {/* Video Player Modal */}
      {activeVideo && (
        <div
          className="search-overlay"
          onClick={() => setActiveVideo(null)}
          role="dialog"
          aria-modal="true"
        >
          <div
            style={{
              position: 'relative',
              width: '100%',
              maxWidth: 880,
              background: '#000',
              borderRadius: 16,
              overflow: 'hidden',
              boxShadow: 'var(--shadow-lg)',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <button
              type="button"
              className="icon-btn"
              style={{
                position: 'absolute',
                top: 12,
                right: 12,
                zIndex: 10,
                background: 'rgba(0,0,0,0.6)',
                color: '#FFF',
              }}
              onClick={() => setActiveVideo(null)}
              aria-label="Close video"
            >
              <X size={18} />
            </button>
            <div style={{ position: 'relative', paddingBottom: '56.25%', height: 0 }}>
              <iframe
                src={getEmbedUrl(activeVideo.url)}
                title={activeVideo.title}
                allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                allowFullScreen
                style={{
                  position: 'absolute',
                  top: 0,
                  left: 0,
                  width: '100%',
                  height: '100%',
                  border: 0,
                }}
              />
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
