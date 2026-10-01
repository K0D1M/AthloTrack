// Firebase web config for push (Firebase console > Project settings > Your apps > Web app, and
// Cloud Messaging > Web Push certificates). Public by design: it identifies the app, it grants
// nothing. Read by push.js (page) and firebase-messaging-sw.js (service worker).
globalThis.ATHLO_FIREBASE = {
  config: {
    apiKey: 'AIzaSyAoxQcM-of-slnJzftsXXTF3nBxHiH-Y0o',
    authDomain: 'athlotrack.firebaseapp.com',
    projectId: 'athlotrack',
    storageBucket: 'athlotrack.firebasestorage.app',
    messagingSenderId: '1084531867027',
    appId: '1:1084531867027:web:cdf68b3c7fa72f9792fc66',
  },
  vapidKey: 'BI88PhskBgEtcy54g83BGvV7d05qqj48levyyiibK_A7V4tzdN5XGEG8zbzBqGztK5AM3BD3EHuL3RW8PwouDuA',
};
