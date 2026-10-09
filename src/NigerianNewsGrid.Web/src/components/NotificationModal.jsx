import React, { useState, useEffect } from 'react';
import { Bell, BellRing, X, CheckCircle2, ShieldCheck, Zap } from 'lucide-react';
import { notificationService } from '../services/notifications';

export default function NotificationModal({ isOpen, onClose }) {
  const [permission, setPermission] = useState('default');
  const [statusMsg, setStatusMsg] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setPermission(notificationService.getPermission());
      setStatusMsg('');
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleEnable = async () => {
    setLoading(true);
    setStatusMsg('');
    try {
      const res = await notificationService.requestPermission();
      setPermission(res);
      if (res === 'granted') {
        setStatusMsg('✓ Notifications enabled! Sending test alert...');
        await notificationService.sendTestNotification();
      } else if (res === 'denied') {
        setStatusMsg('Permission was blocked. You can unblock notifications in your browser settings.');
      }
    } catch (err) {
      setStatusMsg(`Notice: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const handleTestNotification = async () => {
    setLoading(true);
    try {
      await notificationService.sendTestNotification();
      setStatusMsg('✓ Test breaking news notification sent!');
    } catch (err) {
      setStatusMsg('Failed to trigger notification: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const isGranted = permission === 'granted';
  const isDenied = permission === 'denied';

  return (
    <div
      className="search-overlay"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-label="Web Push Notifications"
    >
      <div
        className="glass-card"
        style={{
          width: '100%',
          maxWidth: 480,
          background: 'var(--bg-card)',
          borderRadius: 20,
          border: '1px solid var(--border-card)',
          padding: '28px 24px',
          boxShadow: '0 20px 50px rgba(0, 0, 0, 0.6)',
          position: 'relative',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Close Button */}
        <button
          type="button"
          className="icon-btn"
          style={{ position: 'absolute', top: 16, right: 16 }}
          onClick={onClose}
          aria-label="Close dialog"
        >
          <X size={18} />
        </button>

        {/* Header Icon */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 16 }}>
          <div
            style={{
              width: 48,
              height: 48,
              borderRadius: '50%',
              background: isGranted
                ? 'linear-gradient(135deg, rgba(0, 200, 118, 0.2), rgba(0, 135, 81, 0.4))'
                : 'linear-gradient(135deg, rgba(225, 29, 72, 0.2), rgba(185, 28, 28, 0.35))',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              border: `1.5px solid ${isGranted ? 'var(--emerald-bright)' : 'var(--brand-red)'}`,
            }}
          >
            {isGranted ? (
              <BellRing size={24} color="var(--emerald-bright)" />
            ) : (
              <Bell size={24} color="var(--brand-red)" />
            )}
          </div>

          <div>
            <h2 style={{ fontFamily: 'var(--font-serif)', fontSize: '1.4rem', fontWeight: 800, margin: 0, color: 'var(--text-primary)' }}>
              Breaking News Alerts
            </h2>
            <span style={{ fontSize: '0.8rem', color: isGranted ? 'var(--emerald-bright)' : 'var(--text-muted)', fontWeight: 600 }}>
              {isGranted ? '✓ Active & Receiving Live Alerts' : isDenied ? '⚠️ Blocked in Browser Settings' : 'Instant Real-time Dispatch'}
            </span>
          </div>
        </div>

        {/* Description */}
        <p style={{ fontSize: '0.92rem', color: 'var(--text-secondary)', lineHeight: 1.55, margin: '0 0 20px 0' }}>
          Get instant, high-priority dispatches for breaking national news, central bank policy announcements, and key investigative reports directly on your device.
        </p>

        {/* Feature Highlights */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 24, background: 'var(--bg-elevated)', padding: '14px 16px', borderRadius: 12, border: '1px solid var(--border-subtle)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: '0.82rem', color: 'var(--text-primary)' }}>
            <Zap size={15} style={{ color: 'var(--gold-accent)' }} />
            <span>Instant delivery when major stories break</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: '0.82rem', color: 'var(--text-primary)' }}>
            <ShieldCheck size={15} style={{ color: 'var(--emerald-bright)' }} />
            <span>Zero spam: strictly filtered to urgent headlines</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: '0.82rem', color: 'var(--text-primary)' }}>
            <CheckCircle2 size={15} style={{ color: 'var(--brand-red)' }} />
            <span>Works even when this browser tab is closed</span>
          </div>
        </div>

        {/* Status Message */}
        {statusMsg && (
          <div
            style={{
              padding: '10px 14px',
              borderRadius: 8,
              fontSize: '0.82rem',
              fontWeight: 600,
              marginBottom: 16,
              background: isGranted ? 'rgba(0, 200, 118, 0.15)' : 'rgba(239, 68, 68, 0.15)',
              color: isGranted ? 'var(--emerald-bright)' : 'var(--danger)',
              border: `1px solid ${isGranted ? 'var(--emerald-bright)' : 'var(--danger)'}`,
            }}
          >
            {statusMsg}
          </div>
        )}

        {/* Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          {isGranted ? (
            <button
              type="button"
              className="primary-btn"
              style={{ flex: 1, justifyContent: 'center' }}
              onClick={handleTestNotification}
              disabled={loading}
            >
              <Zap size={16} />
              <span>{loading ? 'Sending...' : 'Send Test Notification'}</span>
            </button>
          ) : (
            <button
              type="button"
              className="primary-btn"
              style={{ flex: 1, justifyContent: 'center' }}
              onClick={handleEnable}
              disabled={loading || isDenied}
            >
              <BellRing size={16} />
              <span>{loading ? 'Requesting...' : isDenied ? 'Permission Blocked' : 'Enable Live Alerts'}</span>
            </button>
          )}

          <button
            type="button"
            className="icon-btn"
            style={{ padding: '0 16px', height: 42, width: 'auto', borderRadius: 'var(--radius-pill)', fontSize: '0.86rem', fontWeight: 600 }}
            onClick={onClose}
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
