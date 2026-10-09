import React, { useState, useEffect, useCallback } from 'react';
import {
  MessageSquare,
  ThumbsUp,
  ThumbsDown,
  CornerDownRight,
  Flag,
  Send,
  Loader2,
  CheckCircle,
  Pin,
  ShieldCheck,
} from 'lucide-react';
import { api } from '../services/api';
import {
  getOrCreateForumUserId,
  getStoredForumUserName,
  saveForumUserName,
  getVerifiedUser,
  clearVerifiedUser,
} from '../services/forumUser';
import AuthModal from './AuthModal';

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
  if (diffDay < 7) return `${diffDay}d ago`;

  return date.toLocaleDateString('en-NG', { month: 'short', day: 'numeric' });
}

function getAvatarColor(name) {
  const colors = [
    '#10B981', '#3B82F6', '#8B5CF6', '#EC4899', '#F59E0B', '#06B6D4', '#6366F1'
  ];
  let hash = 0;
  for (let i = 0; i < (name || '').length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  return colors[Math.abs(hash) % colors.length];
}

function CommentItem({
  comment,
  articleId,
  currentUserId,
  activeUser,
  onRequestAuth,
  onReplyAdded,
  onVoteUpdated,
  isNested = false,
}) {
  const [showReplyBox, setShowReplyBox] = useState(false);
  const [replyAuthor, setReplyAuthor] = useState(() => activeUser?.displayName || getStoredForumUserName());
  const [replyText, setReplyText] = useState('');
  const [submittingReply, setSubmittingReply] = useState(false);
  const [hasReported, setHasReported] = useState(false);
  const [isReporting, setIsReporting] = useState(false);

  useEffect(() => {
    if (activeUser?.displayName) {
      setReplyAuthor(activeUser.displayName);
    }
  }, [activeUser]);

  const initials = (comment.AuthorName || comment.authorName || 'Anonymous')
    .split(' ')
    .map((n) => n[0])
    .join('')
    .substring(0, 2)
    .toUpperCase();

  const authorName = comment.AuthorName || comment.authorName || 'Anonymous';
  const authorAvatar = comment.AuthorAvatar || comment.authorAvatar;
  const content = comment.Content || comment.content || '';
  const upvotes = comment.Upvotes ?? comment.upvotes ?? 0;
  const downvotes = comment.Downvotes ?? comment.downvotes ?? 0;
  const userVote = comment.UserVote ?? comment.userVote ?? 0;
  const createdAt = comment.CreatedAt || comment.createdAt;
  const isPinned = comment.IsPinned || comment.isPinned;
  const replies = comment.Replies || comment.replies || [];

  const handleVote = async (type) => {
    try {
      const res = await api.voteComment(comment.id || comment.Id, currentUserId, type);
      if (res && onVoteUpdated) {
        onVoteUpdated(comment.id || comment.Id, res.upvotes, res.downvotes, res.userVote);
      }
    } catch {
      // Graceful fallback
    }
  };

  const handleReport = async () => {
    if (hasReported || isReporting) return;
    setIsReporting(true);
    try {
      await api.reportComment(comment.id || comment.Id, currentUserId, 'Inappropriate or spam');
      setHasReported(true);
    } catch {
      // Non-blocking
    } finally {
      setIsReporting(false);
    }
  };

  const handleReplyClick = () => {
    if (!activeUser) {
      if (onRequestAuth) onRequestAuth();
      return;
    }
    setShowReplyBox(!showReplyBox);
  };

  const submitReply = async (e) => {
    e.preventDefault();
    if (!activeUser) {
      if (onRequestAuth) onRequestAuth();
      return;
    }
    if (!replyText.trim()) return;

    setSubmittingReply(true);
    try {
      saveForumUserName(replyAuthor);
      const newReply = await api.postComment(articleId, {
        authorName: (activeUser?.displayName || replyAuthor).trim() || 'Anonymous Reader',
        content: replyText.trim(),
        parentCommentId: comment.id || comment.Id,
        authorId: activeUser?.id || currentUserId,
        authorAvatar: activeUser?.avatarUrl || null,
      });

      if (newReply && onReplyAdded) {
        onReplyAdded(comment.id || comment.Id, newReply);
      }
      setReplyText('');
      setShowReplyBox(false);
    } catch (err) {
      console.error('Failed to post reply:', err);
    } finally {
      setSubmittingReply(false);
    }
  };

  return (
    <div className={`forum-comment-card ${isNested ? 'forum-comment-nested' : ''}`}>
      <div className="forum-comment-header">
        {authorAvatar ? (
          <img
            src={authorAvatar}
            alt={authorName}
            className="forum-avatar forum-avatar-img"
            onError={(e) => {
              e.currentTarget.style.display = 'none';
            }}
          />
        ) : (
          <div
            className="forum-avatar"
            style={{ backgroundColor: getAvatarColor(authorName) }}
            aria-hidden="true"
          >
            {initials}
          </div>
        )}
        <div className="forum-comment-meta">
          <div className="forum-author-row">
            <span className="forum-author-name">{authorName}</span>
            <span className="forum-verified-badge" title="Verified Reader">
              <ShieldCheck size={11} />
              <span>Verified</span>
            </span>
            {isPinned && (
              <span className="forum-pinned-badge">
                <Pin size={11} /> Pinned
              </span>
            )}
          </div>
          <span className="forum-comment-time">{formatRelativeTime(createdAt)}</span>
        </div>
      </div>

      <div className="forum-comment-body">
        <p>{content}</p>
      </div>

      {/* Action Row */}
      <div className="forum-comment-actions">
        <button
          type="button"
          className={`forum-action-btn ${userVote === 1 ? 'voted-up' : ''}`}
          onClick={() => handleVote(userVote === 1 ? 0 : 1)}
          title="Upvote comment"
          aria-label="Upvote"
        >
          <ThumbsUp size={13} />
          <span>{upvotes}</span>
        </button>

        <button
          type="button"
          className={`forum-action-btn ${userVote === -1 ? 'voted-down' : ''}`}
          onClick={() => handleVote(userVote === -1 ? 0 : -1)}
          title="Downvote comment"
          aria-label="Downvote"
        >
          <ThumbsDown size={13} />
          {downvotes > 0 && <span>{downvotes}</span>}
        </button>

        {!isNested && (
          <button
            type="button"
            className="forum-action-btn"
            onClick={handleReplyClick}
          >
            <CornerDownRight size={13} />
            <span>Reply</span>
          </button>
        )}

        <button
          type="button"
          className={`forum-action-btn forum-report-btn ${hasReported ? 'reported' : ''}`}
          onClick={handleReport}
          title={hasReported ? 'Reported' : 'Report comment'}
          disabled={hasReported}
        >
          <Flag size={12} />
          <span>{hasReported ? 'Flagged' : 'Report'}</span>
        </button>
      </div>

      {/* Inline Reply Form */}
      {showReplyBox && (
        <form className="forum-reply-form" onSubmit={submitReply}>
          <div className="forum-form-row">
            <input
              type="text"
              className="forum-input"
              placeholder="Your display name..."
              value={replyAuthor}
              onChange={(e) => setReplyAuthor(e.target.value)}
              maxLength={40}
            />
          </div>
          <textarea
            className="forum-textarea"
            placeholder={`Reply to ${authorName}...`}
            value={replyText}
            onChange={(e) => setReplyText(e.target.value)}
            rows={2}
            maxLength={800}
            required
            autoFocus
          />
          <div className="forum-form-footer">
            <span className="forum-char-counter">{800 - replyText.length} left</span>
            <div style={{ display: 'flex', gap: 8 }}>
              <button
                type="button"
                className="secondary-btn"
                style={{ padding: '6px 12px', fontSize: '0.8rem' }}
                onClick={() => setShowReplyBox(false)}
              >
                Cancel
              </button>
              <button
                type="submit"
                className="primary-btn"
                style={{ padding: '6px 14px', fontSize: '0.8rem' }}
                disabled={submittingReply || !replyText.trim()}
              >
                {submittingReply ? <Loader2 size={13} className="spin" /> : <Send size={13} />}
                <span>Post Reply</span>
              </button>
            </div>
          </div>
        </form>
      )}

      {/* Nested Replies */}
      {replies.length > 0 && (
        <div className="forum-nested-replies-list">
          {replies.map((reply) => (
            <CommentItem
              key={reply.id || reply.Id}
              comment={reply}
              articleId={articleId}
              currentUserId={currentUserId}
              activeUser={activeUser}
              onRequestAuth={onRequestAuth}
              onVoteUpdated={onVoteUpdated}
              isNested={true}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export default function CommentSection({
  articleId,
  articleTitle,
  currentUser: propUser,
  onOpenAuth: propOnOpenAuth,
}) {
  const [comments, setComments] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [sortBy, setSortBy] = useState('top'); // 'top' or 'newest'

  // User & verification state
  const [localUser, setLocalUser] = useState(() => getVerifiedUser());
  const [isLocalAuthOpen, setIsLocalAuthOpen] = useState(false);
  const activeUser = propUser !== undefined ? propUser : localUser;

  // Input state
  const [authorName, setAuthorName] = useState(() => activeUser?.displayName || getStoredForumUserName());
  const [content, setContent] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  const [currentUserId] = useState(() => getOrCreateForumUserId());

  // Sync authorName when activeUser updates
  useEffect(() => {
    if (activeUser?.displayName) {
      setAuthorName(activeUser.displayName);
    }
  }, [activeUser]);

  const handleOpenAuth = () => {
    if (propOnOpenAuth) {
      propOnOpenAuth();
    } else {
      setIsLocalAuthOpen(true);
    }
  };

  const handleAuthSuccess = (user) => {
    setLocalUser(user);
    if (user?.displayName) {
      setAuthorName(user.displayName);
    }
    setIsLocalAuthOpen(false);
  };

  // Load comments
  const loadComments = useCallback(async () => {
    if (!articleId) return;
    setLoading(true);
    try {
      const data = await api.getArticleComments(articleId, currentUserId, sortBy);
      setComments(data.comments || []);
      setTotalCount(data.totalCount || 0);
    } catch {
      setComments([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, [articleId, currentUserId, sortBy]);

  useEffect(() => {
    loadComments();
  }, [loadComments]);

  // Handle post comment
  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!activeUser) {
      handleOpenAuth();
      return;
    }
    if (!content.trim() || isSubmitting) return;

    setIsSubmitting(true);
    try {
      saveForumUserName(authorName);
      const created = await api.postComment(articleId, {
        authorName: (activeUser?.displayName || authorName).trim() || 'Anonymous Reader',
        content: content.trim(),
        authorId: activeUser?.id || currentUserId,
        authorAvatar: activeUser?.avatarUrl || null,
      });

      if (created) {
        // Optimistically add to top of comments
        setComments((prev) => [created, ...prev]);
        setTotalCount((prev) => prev + 1);
        setContent('');
        setSubmitSuccess(true);
        setTimeout(() => setSubmitSuccess(false), 3000);
      }
    } catch (err) {
      console.error('Failed to post comment:', err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReplyAdded = (parentCommentId, newReply) => {
    setComments((prev) =>
      prev.map((c) => {
        if ((c.id || c.Id) === parentCommentId) {
          const currentReplies = c.Replies || c.replies || [];
          return {
            ...c,
            replies: [...currentReplies, newReply],
          };
        }
        return c;
      })
    );
    setTotalCount((prev) => prev + 1);
  };

  const handleVoteUpdated = (commentId, upvotes, downvotes, userVote) => {
    setComments((prev) =>
      prev.map((c) => {
        if ((c.id || c.Id) === commentId) {
          return { ...c, upvotes, downvotes, userVote };
        }
        // Check nested replies
        const currentReplies = c.Replies || c.replies || [];
        const updatedReplies = currentReplies.map((r) =>
          (r.id || r.Id) === commentId ? { ...r, upvotes, downvotes, userVote } : r
        );
        return { ...c, replies: updatedReplies };
      })
    );
  };

  return (
    <section className="forum-comments-container" aria-label="Community discussion">
      {/* Header with Title and Sorting */}
      <div className="forum-header-bar">
        <div className="forum-header-title">
          <MessageSquare size={20} className="forum-header-icon" />
          <h3 className="forum-title">Community Discussion</h3>
          <span className="forum-count-badge">{totalCount}</span>
        </div>

        <div className="forum-sort-controls">
          <button
            type="button"
            className={`forum-sort-pill ${sortBy === 'top' ? 'active' : ''}`}
            onClick={() => setSortBy('top')}
          >
            🔥 Top
          </button>
          <button
            type="button"
            className={`forum-sort-pill ${sortBy === 'newest' ? 'active' : ''}`}
            onClick={() => setSortBy('newest')}
          >
            ⏱️ Newest
          </button>
        </div>
      </div>

      {/* Reader Verification Status Banner */}
      {activeUser ? (
        <div className="forum-user-status-bar verified">
          <div className="forum-user-status-left">
            {activeUser.avatarUrl ? (
              <img
                src={activeUser.avatarUrl}
                alt={activeUser.displayName}
                className="forum-user-status-avatar"
              />
            ) : (
              <div className="forum-user-status-avatar-fallback">
                {(activeUser.displayName || 'U')[0].toUpperCase()}
              </div>
            )}
            <div className="forum-user-status-info">
              <div className="forum-user-status-name-row">
                <span className="forum-user-status-name">{activeUser.displayName}</span>
                <span className="forum-verified-pill">
                  <ShieldCheck size={12} />
                  <span>{activeUser.channel === 'google' ? 'Google Verified' : 'Email Verified'}</span>
                </span>
              </div>
              <span className="forum-user-status-hint">Participating as verified reader</span>
            </div>
          </div>
          <button
            type="button"
            className="forum-status-action-link"
            onClick={handleOpenAuth}
            title="Edit display name or switch account"
          >
            Edit Profile
          </button>
        </div>
      ) : (
        <div className="forum-user-status-bar unverified">
          <div className="forum-user-status-left">
            <div className="forum-shield-icon-wrap">
              <ShieldCheck size={18} />
            </div>
            <div className="forum-user-status-info">
              <span className="forum-user-status-title">Reader Verification Required</span>
              <span className="forum-user-status-desc">
                Sign in with <strong>Google (1-Click)</strong> or <strong>Email OTP</strong> to join discussions and share perspectives.
              </span>
            </div>
          </div>
          <button
            type="button"
            className="primary-btn forum-verify-trigger-btn"
            onClick={handleOpenAuth}
          >
            <ShieldCheck size={14} />
            <span>Sign In / Verify</span>
          </button>
        </div>
      )}

      {/* Main Comment Composer */}
      <form className="forum-composer-card" onSubmit={handleSubmit}>
        <div className="forum-form-row">
          <input
            type="text"
            className="forum-input"
            placeholder={activeUser ? "Your public display name..." : "Sign in to verify name..."}
            value={authorName}
            onChange={(e) => setAuthorName(e.target.value)}
            maxLength={50}
            disabled={!activeUser}
          />
        </div>

        <textarea
          className="forum-textarea"
          placeholder={
            activeUser
              ? "What are your thoughts on this story? Share your perspective..."
              : "Click 'Sign In / Verify' to join the community and post comments..."
          }
          value={content}
          onChange={(e) => setContent(e.target.value)}
          onClick={() => {
            if (!activeUser) handleOpenAuth();
          }}
          rows={3}
          maxLength={1500}
          required
        />

        <div className="forum-form-footer">
          <span className="forum-char-counter">{1500 - content.length} characters left</span>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            {submitSuccess && (
              <span className="forum-success-note">
                <CheckCircle size={14} /> Posted!
              </span>
            )}

            <button
              type="submit"
              className="primary-btn forum-submit-btn"
              disabled={isSubmitting || !content.trim()}
            >
              {isSubmitting ? <Loader2 size={15} className="spin" /> : <Send size={15} />}
              <span>{activeUser ? 'Post Comment' : 'Verify & Post'}</span>
            </button>
          </div>
        </div>
      </form>

      {/* Comment List */}
      <div className="forum-comments-feed">
        {loading ? (
          <div className="forum-loading-state">
            <Loader2 size={24} className="spin" />
            <p>Loading conversation...</p>
          </div>
        ) : comments.length === 0 ? (
          <div className="forum-empty-state">
            <MessageSquare size={36} opacity={0.4} />
            <p className="forum-empty-title">No comments yet</p>
            <p className="forum-empty-subtitle">
              Be the first to share your thoughts and start the discussion on this story!
            </p>
          </div>
        ) : (
          comments.map((comment) => (
            <CommentItem
              key={comment.id || comment.Id}
              comment={comment}
              articleId={articleId}
              currentUserId={currentUserId}
              activeUser={activeUser}
              onRequestAuth={handleOpenAuth}
              onReplyAdded={handleReplyAdded}
              onVoteUpdated={handleVoteUpdated}
            />
          ))
        )}
      </div>

      {/* Local fallback AuthModal if opened independently */}
      <AuthModal
        isOpen={isLocalAuthOpen}
        onClose={() => setIsLocalAuthOpen(false)}
        onSuccess={handleAuthSuccess}
        initialDisplayName={authorName}
      />
    </section>
  );
}
