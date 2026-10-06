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
//
// Diagnostics for a phone: ?imelog shows the keyboard events on screen; ?noimefix turns the fix off.

const params = new URLSearchParams(location.search);
if (params.has('imelog')) showLog(params.has('noimefix'));

if (!params.has('noimefix')) {
  let composing = false;
  document.addEventListener('compositionstart', () => { composing = true; }, true);
  document.addEventListener('compositionend', () => { composing = false; }, true);

  window.addEventListener('pointerdown', () => {
    if (!composing) return;
    const input = document.activeElement;
    if (input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement) input.blur();
  }, true);
}

function showLog(fixOff) {
  const box = document.createElement('pre');
  box.style.cssText = 'position:fixed;left:0;right:0;top:0;max-height:45vh;overflow:hidden;margin:0;padding:4px;'
    + 'z-index:2147483647;pointer-events:none;background:rgba(0,0,0,.78);color:#7CFC00;font:11px/1.25 monospace;white-space:pre-wrap';
  const lines = [];
  const t0 = performance.now();
  const add = text => {
    lines.push(Math.round(performance.now() - t0) + ' ' + text);
    if (lines.length > 22) lines.shift();
    box.textContent = (fixOff ? '[fix OFF] ' : '[fix ON] ') + navigator.userAgent.replace(/^.*?\) /, '') + '\n' + lines.join('\n');
  };
  const tag = el => el && el.tagName ? el.tagName.toLowerCase() + (el.className ? '.' + String(el.className).split(' ')[0] : '') : '-';
  const val = e => e.target && 'value' in e.target ? ' v=' + JSON.stringify(e.target.value) : '';
  for (const t of ['keydown', 'keyup'])
    document.addEventListener(t, e => add(t + ' key=' + JSON.stringify(e.key) + ' code=' + e.keyCode + (e.isComposing ? ' comp' : '') + ' ' + tag(e.target)), true);
  for (const t of ['beforeinput', 'input'])
    document.addEventListener(t, e => add(t + ' ' + e.inputType + ' d=' + JSON.stringify(e.data) + val(e)), true);
  for (const t of ['compositionstart', 'compositionupdate', 'compositionend'])
    document.addEventListener(t, e => add(t + ' d=' + JSON.stringify(e.data)), true);
  for (const t of ['focusin', 'focusout'])
    document.addEventListener(t, e => add(t + ' ' + tag(e.target)), true);
  window.addEventListener('pointerdown', e => add('pointerdown ' + e.pointerType + ' active=' + tag(document.activeElement)), true);
  const mount = () => { document.body.appendChild(box); add('log on'); };
  document.body ? mount() : addEventListener('DOMContentLoaded', mount);
}
