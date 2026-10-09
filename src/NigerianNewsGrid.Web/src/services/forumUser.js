/**
 * Helper to manage client user profile and authentication session for forum discussions.
 * Supports Google OAuth and Email OTP verification with persistent trusted sessions.
 */

export const GOOGLE_CLIENT_ID = '434931027697-0ofh3nptkv409vju55od3fhrfbkccu15.apps.googleusercontent.com';

const USER_ID_KEY = 'newsstand_forum_user_id';
const USER_NAME_KEY = 'newsstand_forum_user_name';
const VERIFIED_USER_KEY = 'newsstand_verified_user_profile';
const AUTH_TOKEN_KEY = 'newsstand_auth_token';

export function getOrCreateForumUserId() {
  try {
    const verified = getVerifiedUser();
    if (verified?.id) return verified.id;

    let id = localStorage.getItem(USER_ID_KEY);
    if (!id) {
      id = 'user_' + Math.random().toString(36).substring(2, 9) + Date.now().toString(36);
      localStorage.setItem(USER_ID_KEY, id);
    }
    return id;
  } catch {
    return 'guest_user';
  }
}

export function getStoredForumUserName() {
  try {
    const verified = getVerifiedUser();
    if (verified?.displayName) return verified.displayName;
    return localStorage.getItem(USER_NAME_KEY) || '';
  } catch {
    return '';
  }
}

export function saveForumUserName(name) {
  try {
    if (name && name.trim()) {
      localStorage.setItem(USER_NAME_KEY, name.trim());
      const verified = getVerifiedUser();
      if (verified) {
        verified.displayName = name.trim();
        localStorage.setItem(VERIFIED_USER_KEY, JSON.stringify(verified));
      }
    }
  } catch {
    // Non-blocking
  }
}

export function getVerifiedUser() {
  try {
    const raw = localStorage.getItem(VERIFIED_USER_KEY);
    if (!raw) return null;
    const user = JSON.parse(raw);
    return user && user.isVerified ? user : null;
  } catch {
    return null;
  }
}

export function saveVerifiedUser(user, token) {
  try {
    if (user) {
      localStorage.setItem(VERIFIED_USER_KEY, JSON.stringify(user));
      if (user.displayName) {
        localStorage.setItem(USER_NAME_KEY, user.displayName);
      }
      if (user.id) {
        localStorage.setItem(USER_ID_KEY, user.id);
      }
    }
    if (token) {
      localStorage.setItem(AUTH_TOKEN_KEY, token);
    }
  } catch {
    // Non-blocking
  }
}

export function clearVerifiedUser() {
  try {
    localStorage.removeItem(VERIFIED_USER_KEY);
    localStorage.removeItem(AUTH_TOKEN_KEY);
  } catch {
    // Non-blocking
  }
}

export function isUserVerified() {
  return getVerifiedUser() !== null;
}
