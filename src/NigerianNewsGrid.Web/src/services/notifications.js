/**
 * Web Push & Notification Service for News Stand NG
 */

// Helper to convert base64 to Uint8Array for VAPID key
function urlB64ToUint8Array(base64String) {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = window.atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

export const notificationService = {
  /**
   * Check if web notifications are supported in current browser environment.
   */
  isSupported() {
    return (
      typeof window !== 'undefined' &&
      'Notification' in window &&
      'serviceWorker' in navigator
    );
  },

  /**
   * Get current permission state: 'default' | 'granted' | 'denied'
   */
  getPermission() {
    if (!this.isSupported()) return 'denied';
    return Notification.permission;
  },

  /**
   * Register service worker if not already active.
   */
  async registerServiceWorker() {
    if (!('serviceWorker' in navigator)) return null;
    try {
      const reg = await navigator.serviceWorker.register('/sw.js', { scope: '/' });
      return reg;
    } catch (err) {
      console.warn('[Notifications] Service worker registration failed:', err.message);
      return null;
    }
  },

  /**
   * Request user permission for Web Push notifications.
   */
  async requestPermission() {
    if (!this.isSupported()) {
      throw new Error('Notifications are not supported by this browser.');
    }

    const permission = await Notification.requestPermission();
    if (permission === 'granted') {
      await this.registerServiceWorker();
    }
    return permission;
  },

  /**
   * Trigger an instant live test notification to verify delivery.
   */
  async sendTestNotification() {
    if (this.getPermission() !== 'granted') {
      const res = await this.requestPermission();
      if (res !== 'granted') return false;
    }

    const registration = await this.registerServiceWorker();
    if (registration && registration.showNotification) {
      await registration.showNotification('🔴 Breaking: Central Bank Unveils FX Liquidity Measures', {
        body: 'Monetary Policy Committee announces new foreign exchange reserves benchmarks and SME credit facilities.',
        icon: '/logo.png',
        badge: '/favicon.svg',
        vibrate: [200, 100, 200],
        data: {
          url: window.location.href,
        },
      });
      return true;
    } else {
      new Notification('🔴 Breaking: Central Bank Unveils FX Liquidity Measures', {
        body: 'Monetary Policy Committee announces new foreign exchange reserves benchmarks and SME credit facilities.',
        icon: '/logo.png',
      });
      return true;
    }
  },

  /**
   * Subscribe to Web Push and deliver endpoint to backend.
   */
  async subscribePush(vapidPublicKey = '') {
    const registration = await this.registerServiceWorker();
    if (!registration || !('pushManager' in registration)) return null;

    try {
      const options = { userVisibleOnly: true };
      if (vapidPublicKey) {
        options.applicationServerKey = urlB64ToUint8Array(vapidPublicKey);
      }

      const subscription = await registration.pushManager.subscribe(options);

      // Post subscription to backend endpoint
      await fetch('/api/v1/notifications/subscribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(subscription),
      }).catch(() => {
        // Non-blocking if backend endpoint is in deployment
      });

      return subscription;
    } catch (err) {
      console.warn('[Notifications] Push subscription failed:', err.message);
      return null;
    }
  },
};
