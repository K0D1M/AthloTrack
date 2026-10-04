// Picks up a new deploy without the user having to know they should reload.
// Every deploy writes /version.txt (see the Dockerfile). The app remembers the version it started
// with and checks again every few minutes and whenever it comes back into view:
//  - the app was in the background for a while (other tab, phone locked, Home Screen app
//    reopened): it reloads straight away, before the user starts doing anything;
//  - the app is in use: a small banner offers «Ανανέωση», so a half-filled form isn't lost.
// Without a version.txt (local development) nothing happens.

const CHECK_EVERY_MS = 5 * 60 * 1000;
// Shorter than this away (a quick look at another tab) still counts as "in use".
const AWAY_MS = 60 * 1000;
let loaded = null;
let hiddenAt = null;
let pending = false;
let banner = null;

async function fetchVersion() {
  try {
    const response = await fetch('/version.txt', { cache: 'no-store' });
    if (!response.ok) return null;
    const text = (await response.text()).trim();
    // A server that answers a missing file with index.html must not count as a version.
    return /^[\w.-]{1,64}$/.test(text) ? text : null;
  } catch {
    return null; // offline: try again later
  }
}

function showBanner() {
  if (banner) return;
  banner = document.createElement('div');
  banner.className = 'athlo-update';
  banner.setAttribute('role', 'status');
  const text = document.createElement('span');
  text.textContent = 'Νέα έκδοση του AthloTrack.';
  const button = document.createElement('button');
  button.type = 'button';
  button.textContent = 'Ανανέωση';
  button.addEventListener('click', () => location.reload());
  banner.append(text, button);
  document.body.append(banner);
}

/** fromBackground: the app was out of view until now, so a reload interrupts nothing. */
export async function check(fromBackground = false) {
  if (loaded === null) return;
  if (!pending) {
    const current = await fetchVersion();
    pending = current !== null && current !== loaded;
  }
  if (!pending) return;
  if (fromBackground) location.reload();
  else showBanner();
}

export async function start() {
  loaded = await fetchVersion();
  if (loaded === null) return;

  setInterval(() => check(false), CHECK_EVERY_MS);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
      hiddenAt = Date.now();
      return;
    }
    const away = hiddenAt !== null && Date.now() - hiddenAt >= AWAY_MS;
    hiddenAt = null;
    check(away);
  });
  // A Home Screen app reopened from memory may only fire pageshow.
  window.addEventListener('pageshow', e => { if (e.persisted) check(true); });
}
