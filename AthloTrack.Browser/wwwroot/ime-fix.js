// Phone keyboards (Gboard on Android Chrome) "compose" a word before committing it. Avalonia's
// web text boxes share one hidden <input>, and Avalonia moves its focus on the touch that picks
// another field, *before* the browser finishes the word (compositionend). The finished word then
// landed in the newly chosen field (e.g. the email in the password box), and the field being
// typed in kept only its uncommitted preview, which vanished.
//
// Fix: when a touch starts while a word is being composed, end the composition first (blurring the
// hidden input makes the browser commit it), while the old field still has Avalonia's focus. The
// window capture listener runs before Avalonia's own pointer handling. Desktop browsers rarely
// compose, so this changes nothing there.

let composing = false;
document.addEventListener('compositionstart', () => { composing = true; }, true);
document.addEventListener('compositionend', () => { composing = false; }, true);

window.addEventListener('pointerdown', () => {
  if (!composing) return;
  const input = document.activeElement;
  if (input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement) input.blur();
}, true);
