import { useState, useEffect, useCallback } from 'react';
import { api } from '../services/api';

const DISMISS_KEY = 'newsstand_pwa_dismissed_until';
const INSTALL_TRACKED_KEY = 'newsstand_pwa_install_tracked';
const SESSION_TRACKED_KEY = 'newsstand_pwa_session_tracked';
const DISMISS_COOLDOWN_MS = 7 * 24 * 60 * 60 * 1000; // 7 days

function detectPlatform() {
  if (typeof window === 'undefined') return 'Web';
  const ua = navigator.userAgent;
  if (/android/i.test(ua)) return 'Web-PWA-Android';
  if (/iPad|iPhone|iPod/.test(ua)) return 'Web-PWA-iOS';
  if (/windows/i.test(ua)) return 'Web-PWA-Windows';
  if (/macintosh|mac os x/i.test(ua)) return 'Web-PWA-Mac';
  if (/linux/i.test(ua)) return 'Web-PWA-Linux';
  return 'Web-PWA';
}

export function usePwaInstall() {
  const [deferredPrompt, setDeferredPrompt] = useState(null);
  const [isInstallable, setIsInstallable] = useState(false);
  const [isInstalled, setIsInstalled] = useState(false);
  const [isIos, setIsIos] = useState(false);
  const [showIosModal, setShowIosModal] = useState(false);
  const [hasEngaged, setHasEngaged] = useState(false);

  useEffect(() => {
    if (typeof window === 'undefined') return;

    // 1. Register Service Worker on page load
    if ('serviceWorker' in navigator) {
      window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js', { scope: '/' })
          .catch((err) => console.debug('SW registration notice:', err));
      });
    }

    // 2. Detect if running in standalone PWA mode (either via display-mode, navigator.standalone, or ?source=pwa)
    const isStandalone =
      window.matchMedia('(display-mode: standalone)').matches ||
      window.navigator.standalone === true ||
      window.location.search.includes('source=pwa') ||
      window.location.search.includes('source=shortcut');

    if (isStandalone) {
      setIsInstalled(true);

      // Track install if not previously logged
      if (!localStorage.getItem(INSTALL_TRACKED_KEY)) {
        api.trackPwaEvent('pwa_install', {
          source: 'standalone_first_launch',
          platform: detectPlatform(),
        });
        localStorage.setItem(INSTALL_TRACKED_KEY, 'true');
      }

      // Track session open once per session
      if (!sessionStorage.getItem(SESSION_TRACKED_KEY)) {
        api.trackPwaEvent('pwa_open', {
          source: 'standalone_session',
          platform: detectPlatform(),
        });
        sessionStorage.setItem(SESSION_TRACKED_KEY, 'true');
      }
      return;
    }

    // 3. Detect iOS Safari
    const ua = window.navigator.userAgent;
    const isIosDevice = /iPad|iPhone|iPod/.test(ua) && !window.MSStream;
    const isSafari = /Safari/.test(ua) && !/Chrome|CriOS|FxiOS|EdgiOS/.test(ua);
    if (isIosDevice && isSafari && !isStandalone) {
      setIsIos(true);
      setIsInstallable(true);
    }

    // 4. Listen for Chromium beforeinstallprompt event
    const handleBeforeInstallPrompt = (e) => {
      e.preventDefault(); // Prevent default mini-infobar
      setDeferredPrompt(e);
      setIsInstallable(true);
    };

    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt);

    // 5. Listen for appinstalled event (Native OS confirmation)
    const handleAppInstalled = () => {
      setIsInstalled(true);
      setIsInstallable(false);
      setDeferredPrompt(null);
      localStorage.removeItem(DISMISS_KEY);

      if (!localStorage.getItem(INSTALL_TRACKED_KEY)) {
        api.trackPwaEvent('pwa_install', {
          source: 'browser_appinstalled_event',
          platform: detectPlatform(),
        });
        localStorage.setItem(INSTALL_TRACKED_KEY, 'true');
      }
    };

    window.addEventListener('appinstalled', handleAppInstalled);

    // 6. Track session engagement (time or articles read)
    const engagementTimer = setTimeout(() => {
      setHasEngaged(true);
    }, 45000); // 45 seconds initial engagement threshold

    return () => {
      window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
      window.removeEventListener('appinstalled', handleAppInstalled);
      clearTimeout(engagementTimer);
    };
  }, []);

  // Check if currently within dismissal cooldown
  const isDismissed = useCallback(() => {
    if (typeof window === 'undefined') return false;
    const until = localStorage.getItem(DISMISS_KEY);
    if (!until) return false;
    return Date.now() < parseInt(until, 10);
  }, []);

  // Dismiss prompt with 7-day cooldown
  const dismissPrompt = useCallback(() => {
    if (typeof window === 'undefined') return;
    localStorage.setItem(DISMISS_KEY, (Date.now() + DISMISS_COOLDOWN_MS).toString());
    setHasEngaged(false);
    api.trackPwaEvent('pwa_prompt_dismissed', {
      source: 'quick_pill_dismiss',
      platform: detectPlatform(),
    });
  }, []);

  // Trigger installation dialog
  const promptInstall = useCallback(async (triggerSource = 'custom_prompt') => {
    if (isIos) {
      setShowIosModal(true);
      return;
    }

    if (!deferredPrompt) {
      // Fallback for browsers that don't support beforeinstallprompt
      alert('To install, tap your browser menu (⋮) and select "Install app" or "Add to Home screen".');
      return;
    }

    try {
      deferredPrompt.prompt();
      const choiceResult = await deferredPrompt.userChoice;
      if (choiceResult.outcome === 'accepted') {
        setIsInstalled(true);
        setIsInstallable(false);
        if (!localStorage.getItem(INSTALL_TRACKED_KEY)) {
          api.trackPwaEvent('pwa_install', {
            source: triggerSource,
            platform: detectPlatform(),
          });
          localStorage.setItem(INSTALL_TRACKED_KEY, 'true');
        }
      } else {
        api.trackPwaEvent('pwa_prompt_dismissed', {
          source: 'browser_dialog_canceled',
          platform: detectPlatform(),
        });
      }
      setDeferredPrompt(null);
    } catch (err) {
      console.error('Error triggering PWA install:', err);
    }
  }, [deferredPrompt, isIos]);

  return {
    isInstallable,
    isInstalled,
    isIos,
    hasEngaged,
    isDismissed: isDismissed(),
    showIosModal,
    setShowIosModal,
    promptInstall,
    dismissPrompt,
    recordEngagement: () => setHasEngaged(true),
  };
}
