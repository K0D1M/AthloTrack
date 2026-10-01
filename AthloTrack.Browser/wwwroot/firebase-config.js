// Firebase web config for push (Firebase console > Project settings > Your apps > Web app, and
// Cloud Messaging > Web Push certificates). Public by design: it identifies the app, it grants
// nothing. Read by push.js (page) and firebase-messaging-sw.js (service worker).
globalThis.ATHLO_FIREBASE = {
  config: null,
  vapidKey: '',
};
