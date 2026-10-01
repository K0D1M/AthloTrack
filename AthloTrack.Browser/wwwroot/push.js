// Web push for AthloTrack through Firebase Cloud Messaging: the same Firebase project and the
// same "push" Edge Function as the Android app. Imported by Services/BrowserPush.cs as "push".
import './firebase-config.js';

const SDK = 'https://www.gstatic.com/firebasejs/12.19.0';
const fb = globalThis.ATHLO_FIREBASE;
let messagingPromise = null;

const configured = () => !!(fb && fb.config && fb.config.appId && fb.vapidKey);
const isIos = () => /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
const isStandalone = () => navigator.standalone === true || matchMedia('(display-mode: standalone)').matches;

/** 'unconfigured' | 'unsupported' | 'install-first' (iPhone, not on the Home Screen) | 'not-asked' | 'granted' | 'denied' */
export function state() {
  if (!configured()) return 'unconfigured';
  // iOS only delivers web push to a web app opened from the Home Screen.
  if (isIos() && !isStandalone()) return 'install-first';
  if (!('Notification' in globalThis) || !('serviceWorker' in navigator) || !('PushManager' in globalThis)) return 'unsupported';
  switch (Notification.permission) {
    case 'granted': return 'granted';
    case 'denied': return 'denied';
    default: return 'not-asked';
  }
}

function messaging() {
  messagingPromise ??= (async () => {
    const { initializeApp } = await import(`${SDK}/firebase-app.js`);
    const fm = await import(`${SDK}/firebase-messaging.js`);
    if (!(await fm.isSupported())) return null;
    const registration = await navigator.serviceWorker.register('/firebase-messaging-sw.js');
    await navigator.serviceWorker.ready;
    const instance = fm.getMessaging(initializeApp(fb.config));
    // While the app is open FCM hands the message to the page instead of showing it: show it anyway.
    fm.onMessage(instance, p => registration.showNotification(p.notification?.title ?? 'AthloTrack', {
      body: p.notification?.body ?? '',
      icon: '/icon-192.png',
      data: { link: p.fcmOptions?.link ?? '/' },
    }));
    return { fm, instance, registration };
  })();
  return messagingPromise;
}

/** Must run from a tap: browsers (Safari above all) refuse the prompt otherwise. */
export async function requestPermission() {
  const s = state();
  if (s === 'granted') return true;
  if (s !== 'not-asked') return false;
  return (await Notification.requestPermission()) === 'granted';
}

/** This browser's FCM token, or '' (never prompts). */
export async function getToken() {
  if (state() !== 'granted') return '';
  const m = await messaging();
  if (!m) return '';
  return (await m.fm.getToken(m.instance, { vapidKey: fb.vapidKey, serviceWorkerRegistration: m.registration })) ?? '';
}

/** The app was opened from a notification: "type|notificationId", or ''. */
export function launchRequest() {
  const q = new URLSearchParams(location.search);
  const type = q.get('type');
  if (!type) return '';
  history.replaceState(null, '', location.pathname); // a reload shouldn't replay it
  return `${type}|${q.get('notification') ?? ''}`;
}
