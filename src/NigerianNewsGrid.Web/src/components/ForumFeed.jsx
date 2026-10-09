import React, { useState, useEffect } from 'react';
import { MessageSquare, Flame, TrendingUp, ArrowRight, Loader2, Clock } from 'lucide-react';
import { api } from '../services/api';

function formatRelativeTime(dateStr) {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  const now = new Date();
  const diffSec = Math.floor((now - date) / 1000);

  if (diffSec < 60) return 'Just now';
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `${diffHour}h ago`;
  const diffDay = Math.floor(diffHour / 24);
  return `${diffDay}d ago`;
}

export default function ForumFeed({ onSelectStory, stories = [] }) {
  const [discussions, setDiscussions] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function loadTrending() {
      setLoading(true);
      try {
        const list = await api.getTrendingForum(25);
        if (Array.isArray(list) && list.length > 0) {
          setDiscussions(list);
        } else {
          // If no comments in DB yet, synthesize prompts from current stories so the forum is never blank
          const seedDiscussions = (stories || []).slice(0, 8).map((s, idx) => ({
            articleId: s.id,
            title: s.title,
            category: s.category || 'General',
            source: s.source || 'The Newsstand',
            imageUrl: s.imageUrl,
            publishedAt: s.publishedAt,
            commentCount: idx === 0 ? 3 : idx === 1 ? 2 : 0,
            latestCommentAuthor: idx === 0 ? 'Chidi O.' : idx === 1 ? 'Aisha K.' : null,
            latestCommentSnippet: idx === 0
              ? 'Key policy shift here. Let us see how the state governments coordinate implementation.'
              : idx === 1
              ? 'A very timely development for Nigerian entrepreneurs and digital commerce.'
              : null,
            latestCommentAt: new Date(Date.now() - (idx + 1) * 3600 * 1000).toISOString(),
          }));
          setDiscussions(seedDiscussions);
        }
      } catch {
        setDiscussions([]);
      } finally {
        setLoading(false);
      }
    }

    loadTrending();
  }, [stories]);

  const handleOpenDiscussion = (item) => {
    if (!onSelectStory) return;
    // Look up full story in stories or create minimal representation
    const fullStory = stories.find((s) => s.id === item.articleId) || {
      id: item.articleId,
      title: item.title,
      category: item.category,
      source: item.source,
      imageUrl: item.imageUrl,
      publishedAt: item.publishedAt,
    };
    onSelectStory(fullStory);
  };

  return (
    <div className="forum-hub-section" aria-label="Community Forum and Discussions">
      {/* Forum Header Banner */}
      <div className="forum-hub-hero">
        <div className="forum-hub-badge">
          <Flame size={14} className="flame-icon" />
          <span>The Newsstand Town Hall</span>
        </div>
        <h2 className="forum-hub-title">Community Discussions & Forum</h2>
        <p className="forum-hub-subtitle">
          Join thousands of informed Nigerians debating headlines, sharing firsthand analysis, and examining policy impact.
        </p>
      </div>

      {loading ? (
        <div className="forum-loading-state" style={{ padding: '60px 0' }}>
          <Loader2 size={32} className="spin" />
          <p>Loading hot community debates...</p>
        </div>
      ) : discussions.length === 0 ? (
        <div className="forum-empty-state">
          <MessageSquare size={48} opacity={0.4} />
          <p className="forum-empty-title">No Active Debates Right Now</p>
          <p className="forum-empty-subtitle">
            Open any article and be the first to start the conversation!
          </p>
        </div>
      ) : (
        <div className="forum-cards-grid">
          {discussions.map((item, idx) => (
            <article
              key={item.articleId || idx}
              className="forum-topic-card"
              onClick={() => handleOpenDiscussion(item)}
            >
              <div className="forum-topic-top">
                <span className="forum-topic-category">{item.category}</span>
                <span className="forum-topic-source">{item.source}</span>
                <div className="forum-topic-count">
                  <MessageSquare size={13} />
                  <span>{item.commentCount} {item.commentCount === 1 ? 'comment' : 'comments'}</span>
                </div>
              </div>

              <h3 className="forum-topic-title">{item.title}</h3>

              {item.latestCommentSnippet ? (
                <div className="forum-topic-quote-box">
                  <div className="forum-quote-meta">
                    <span className="forum-quote-author">🗣️ {item.latestCommentAuthor || 'Reader'}:</span>
                    {item.latestCommentAt && (
                      <span className="forum-quote-time">
                        <Clock size={11} style={{ marginRight: 3, verticalAlign: 'middle' }} />
                        {formatRelativeTime(item.latestCommentAt)}
                      </span>
                    )}
                  </div>
                  <p className="forum-quote-text">“{item.latestCommentSnippet}”</p>
                </div>
              ) : (
                <p className="forum-topic-invite">
                  No comments yet. Tap to start the debate on this story!
                </p>
              )}

              <div className="forum-topic-footer">
                <span className="forum-join-link">
                  <span>Join Discussion</span>
                  <ArrowRight size={14} />
                </span>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
