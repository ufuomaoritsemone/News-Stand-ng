import React, { useState, useEffect, useRef } from 'react';
import {
  X,
  Mail,
  ShieldCheck,
  CheckCircle,
  AlertCircle,
  Loader2,
  ArrowRight,
  User,
  Sparkles,
} from 'lucide-react';
import { api } from '../services/api';
import { GOOGLE_CLIENT_ID, saveVerifiedUser } from '../services/forumUser';

export default function AuthModal({ isOpen, onClose, onSuccess, initialDisplayName = '' }) {
  const [activeTab, setActiveTab] = useState('google'); // 'google' | 'email'

  // Google State
  const googleBtnRef = useRef(null);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [googleError, setGoogleError] = useState('');

  // Email State
  const [email, setEmail] = useState('');
  const [customName, setCustomName] = useState(initialDisplayName);
  const [emailStep, setEmailStep] = useState('request'); // 'request' | 'verify'
  const [otpCode, setOtpCode] = useState('');
  const [emailLoading, setEmailLoading] = useState(false);
  const [emailError, setEmailError] = useState('');
  const [emailMessage, setEmailMessage] = useState('');
  const [debugOtp, setDebugOtp] = useState('');
  const [resendTimer, setResendTimer] = useState(0);

  // Success state
  const [verifiedProfile, setVerifiedProfile] = useState(null);

  // Dynamic loader for Google Identity Services script
  const loadGoogleGsiScript = () => {
    if (typeof window === 'undefined') return Promise.resolve(false);
    if (window.google?.accounts?.id) return Promise.resolve(true);

    return new Promise((resolve) => {
      let script = document.getElementById('google-gsi-client');
      if (!script) {
        script = document.createElement('script');
        script.id = 'google-gsi-client';
        script.src = 'https://accounts.google.com/gsi/client';
        script.async = true;
        script.defer = true;
        document.head.appendChild(script);
      }
      script.addEventListener('load', () => resolve(true), { once: true });
      script.addEventListener('error', () => resolve(false), { once: true });
      if (window.google?.accounts?.id) resolve(true);
    });
  };

  // Initialize Google Identity Services button with dynamic script loading
  useEffect(() => {
    if (!isOpen || activeTab !== 'google') return;

    let isMounted = true;

    const renderGoogleBtn = () => {
      if (!isMounted) return;
      if (window.google?.accounts?.id && googleBtnRef.current) {
        googleBtnRef.current.innerHTML = '';
        window.google.accounts.id.initialize({
          client_id: GOOGLE_CLIENT_ID,
          callback: async (response) => {
            if (!response?.credential) return;
            setGoogleLoading(true);
            setGoogleError('');
            try {
              const res = await api.verifyGoogleToken(response.credential, customName.trim() || null);
              if (res?.success && res.user) {
                saveVerifiedUser(res.user, res.token);
                setVerifiedProfile(res.user);
                setTimeout(() => {
                  if (onSuccess) onSuccess(res.user);
                  onClose();
                }, 1200);
              } else {
                setGoogleError(res?.error || 'Google authentication failed.');
              }
            } catch (err) {
              setGoogleError(err.message || 'Error verifying with server.');
            } finally {
              setGoogleLoading(false);
            }
          },
        });

        window.google.accounts.id.renderButton(googleBtnRef.current, {
          theme: 'filled_blue',
          size: 'large',
          shape: 'pill',
          text: 'continue_with',
          width: 280,
        });
      }
    };

    loadGoogleGsiScript().then((loaded) => {
      if (!isMounted || !loaded) return;
      renderGoogleBtn();
    });

    return () => {
      isMounted = false;
    };
  }, [isOpen, activeTab, customName, onSuccess, onClose]);

  // Resend Countdown Timer
  useEffect(() => {
    if (resendTimer <= 0) return;
    const t = setInterval(() => setResendTimer((prev) => prev - 1), 1000);
    return () => clearInterval(t);
  }, [resendTimer]);

  if (!isOpen) return null;

  // Handle Request Email OTP
  const handleRequestOtp = async (e) => {
    e.preventDefault();
    if (!email.trim()) return;

    setEmailLoading(true);
    setEmailError('');
    setEmailMessage('');
    try {
      const res = await api.requestEmailOtp(email.trim(), customName.trim() || null);
      if (res?.success) {
        setEmailStep('verify');
        setEmailMessage(res.message || `Code sent to ${email}`);
        if (res.debugCode) {
          setDebugOtp(res.debugCode);
        }
        setResendTimer(60);
      } else {
        setEmailError(res?.error || 'Failed to send verification code.');
      }
    } catch (err) {
      setEmailError(err.message || 'Failed to request verification code.');
    } finally {
      setEmailLoading(false);
    }
  };

  // Handle Verify Email OTP
  const handleVerifyOtp = async (e) => {
    e.preventDefault();
    if (!otpCode.trim()) return;

    setEmailLoading(true);
    setEmailError('');
    try {
      const res = await api.verifyEmailOtp(email.trim(), otpCode.trim(), customName.trim() || null);
      if (res?.success && res.user) {
        saveVerifiedUser(res.user, res.token);
        setVerifiedProfile(res.user);
        setTimeout(() => {
          if (onSuccess) onSuccess(res.user);
          onClose();
        }, 1200);
      } else {
        setEmailError(res?.error || 'Verification failed.');
      }
    } catch (err) {
      setEmailError(err.message || 'Verification failed. Please check the code.');
    } finally {
      setEmailLoading(false);
    }
  };

  return (
    <div className="auth-modal-backdrop" onClick={onClose}>
      <div
        className="auth-modal-dialog glass-card"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="auth-modal-title"
      >
        {/* Close Button */}
        <button
          type="button"
          className="auth-modal-close"
          onClick={onClose}
          aria-label="Close dialog"
        >
          <X size={18} />
        </button>

        {/* Modal Header */}
        <div className="auth-modal-header">
          <div className="auth-badge">
            <ShieldCheck size={16} />
            <span>Reader Verification</span>
          </div>
          <h2 id="auth-modal-title" className="auth-modal-title">
            Join The Conversation
          </h2>
          <p className="auth-modal-subtitle">
            Verify your reader identity once to comment, debate stories, and vote on community perspectives.
          </p>
        </div>

        {/* Success Banner */}
        {verifiedProfile ? (
          <div className="auth-success-screen">
            <CheckCircle size={48} className="auth-success-icon" />
            <h3 className="auth-success-title">You're Verified!</h3>
            <p className="auth-success-desc">
              Welcome, <strong>{verifiedProfile.displayName}</strong>. Your comments now carry the official verified badge.
            </p>
          </div>
        ) : (
          <>
            {/* Display Name / Pseudonym Option */}
            <div className="auth-name-field">
              <label htmlFor="auth-custom-name" className="auth-input-label">
                <User size={13} style={{ marginRight: 4 }} />
                Public Display Name (or Pen-Name)
              </label>
              <input
                id="auth-custom-name"
                type="text"
                className="forum-input"
                placeholder="e.g., Tunde A., Aisha K., AbujaObserver..."
                value={customName}
                onChange={(e) => setCustomName(e.target.value)}
                maxLength={40}
              />
              <span className="auth-input-hint">
                You can use a pseudonym or your real name for story debates.
              </span>
            </div>

            {/* Verification Channel Tabs */}
            <div className="auth-channel-tabs" role="tablist">
              <button
                type="button"
                role="tab"
                aria-selected={activeTab === 'google'}
                className={`auth-tab-btn ${activeTab === 'google' ? 'active' : ''}`}
                onClick={() => {
                  setActiveTab('google');
                  setGoogleError('');
                }}
              >
                <svg width="16" height="16" viewBox="0 0 24 24" style={{ marginRight: 6 }}>
                  <path
                    fill="#4285F4"
                    d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v4.51h6.6c-.29 1.52-1.14 2.8-2.4 3.65v3.03h3.88c2.27-2.09 3.665-5.17 3.665-9.12z"
                  />
                  <path
                    fill="#34A853"
                    d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.88-3.03c-1.08.72-2.45 1.16-4.05 1.16-3.12 0-5.77-2.1-6.72-4.93H1.25v3.13C3.28 21.44 7.33 24 12 24z"
                  />
                  <path
                    fill="#FBBC05"
                    d="M5.28 14.29c-.25-.72-.38-1.49-.38-2.29s.13-1.57.38-2.29V6.58H1.25C.45 8.18 0 9.99 0 12s.45 3.82 1.25 5.42l4.03-3.13z"
                  />
                  <path
                    fill="#EA4335"
                    d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.33 0 3.28 2.56 1.25 6.58l4.03 3.13c.95-2.83 3.6-4.96 6.72-4.96z"
                  />
                </svg>
                <span>Google (1-Click)</span>
              </button>

              <button
                type="button"
                role="tab"
                aria-selected={activeTab === 'email'}
                className={`auth-tab-btn ${activeTab === 'email' ? 'active' : ''}`}
                onClick={() => {
                  setActiveTab('email');
                  setEmailError('');
                }}
              >
                <Mail size={15} style={{ marginRight: 6 }} />
                <span>Email (OTP Code)</span>
              </button>
            </div>

            {/* 1. Google 1-Click Tab Pane */}
            {activeTab === 'google' && (
              <div className="auth-tab-pane">
                <p className="auth-pane-desc">
                  Instant 1-click verification with your Google account. Fast, secure, and no passwords required.
                </p>

                {googleLoading ? (
                  <div className="auth-loading-wrap">
                    <Loader2 size={24} className="spin" />
                    <span>Verifying with Google...</span>
                  </div>
                ) : (
                  <div className="google-btn-wrapper" ref={googleBtnRef}>
                    {/* Fallback if Google GIS script blocked */}
                    <div style={{ fontSize: '0.82rem', color: 'var(--text-muted)' }}>
                      Loading Google Sign-In...
                    </div>
                  </div>
                )}

                {googleError && (
                  <div className="auth-error-banner">
                    <AlertCircle size={15} />
                    <span>{googleError}</span>
                  </div>
                )}
              </div>
            )}

            {/* 2. Email OTP Tab Pane */}
            {activeTab === 'email' && (
              <div className="auth-tab-pane">
                {emailStep === 'request' ? (
                  <form onSubmit={handleRequestOtp} className="auth-email-form">
                    <p className="auth-pane-desc">
                      Enter your email to receive a 6-digit verification code.
                    </p>

                    <div className="forum-form-row">
                      <input
                        type="email"
                        className="forum-input"
                        placeholder="e.g., yourname@gmail.com"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        required
                        autoFocus
                      />
                    </div>

                    {emailError && (
                      <div className="auth-error-banner">
                        <AlertCircle size={15} />
                        <span>{emailError}</span>
                      </div>
                    )}

                    <button
                      type="submit"
                      className="primary-btn auth-submit-btn"
                      disabled={emailLoading || !email.trim()}
                    >
                      {emailLoading ? <Loader2 size={15} className="spin" /> : <ArrowRight size={15} />}
                      <span>Send Verification Code</span>
                    </button>
                  </form>
                ) : (
                  <form onSubmit={handleVerifyOtp} className="auth-email-form">
                    <p className="auth-pane-desc">
                      Enter the 6-digit code sent to <strong>{email}</strong>:
                    </p>

                    {emailMessage && (
                      <div className="auth-info-banner">
                        <Sparkles size={14} />
                        <span>{emailMessage}</span>
                      </div>
                    )}

                    {debugOtp && (
                      <div className="auth-debug-banner">
                        <span>🧪 Dev Code: <strong>{debugOtp}</strong></span>
                        <button
                          type="button"
                          className="auth-debug-copy-btn"
                          onClick={() => setOtpCode(debugOtp)}
                        >
                          Auto-Fill
                        </button>
                      </div>
                    )}

                    <div className="forum-form-row">
                      <input
                        type="text"
                        className="forum-input auth-otp-input"
                        placeholder="123456"
                        value={otpCode}
                        onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                        maxLength={6}
                        required
                        autoFocus
                      />
                    </div>

                    {emailError && (
                      <div className="auth-error-banner">
                        <AlertCircle size={15} />
                        <span>{emailError}</span>
                      </div>
                    )}

                    <div style={{ display: 'flex', gap: 10, marginTop: 10 }}>
                      <button
                        type="button"
                        className="secondary-btn"
                        style={{ padding: '8px 14px', fontSize: '0.85rem' }}
                        onClick={() => setEmailStep('request')}
                      >
                        Back
                      </button>

                      <button
                        type="submit"
                        className="primary-btn auth-submit-btn"
                        style={{ flex: 1 }}
                        disabled={emailLoading || otpCode.length !== 6}
                      >
                        {emailLoading ? <Loader2 size={15} className="spin" /> : <CheckCircle size={15} />}
                        <span>Verify & Continue</span>
                      </button>
                    </div>

                    <div style={{ textAlign: 'center', marginTop: 14 }}>
                      <button
                        type="button"
                        className="auth-resend-link"
                        disabled={resendTimer > 0 || emailLoading}
                        onClick={handleRequestOtp}
                      >
                        {resendTimer > 0 ? `Resend code in ${resendTimer}s` : 'Resend code'}
                      </button>
                    </div>
                  </form>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
