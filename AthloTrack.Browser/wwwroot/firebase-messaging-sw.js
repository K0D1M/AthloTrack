// AthloTrack push service worker. With the app closed, Firebase shows the notification itself and
// opens its link (webpush.fcm_options.link, set by the "push" Edge Function) when it's tapped.
importScripts('/firebase-config.js');
importScripts(
  'https://www.gstatic.com/firebasejs/12.19.0/firebase-app-compat.js',
  'https://www.gstatic.com/firebasejs/12.19.0/firebase-messaging-compat.js',
);

// Notifications shown by push.js while the app was open: open their link on tap.
self.addEventListener('notificationclick', event => {
  const data = event.notification.data;
  if (!data || !data.link || data.FCM_MSG) return; // Firebase's own notifications handle themselves
  event.notification.close();
  event.waitUntil(clients.openWindow(data.link));
});

if (self.ATHLO_FIREBASE && self.ATHLO_FIREBASE.config) {
  firebase.initializeApp(self.ATHLO_FIREBASE.config);
  firebase.messaging();
}
