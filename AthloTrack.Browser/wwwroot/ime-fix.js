// Phone keyboards and Avalonia's web text boxes (which share one hidden <input>). Avalonia takes
// text from two places only: keydown events with a real key (desktop typing) and finished
// compositions (compositionend). Two fixes for phone keyboards:
//
// 1. Text committed without either. Samsung Keyboard (and others) send digits, symbols and
//    sometimes whole words as a plain `beforeinput insertText` with an "Unidentified" (229) keydown.
//    Avalonia dropped it: no @ or . in an email, nothing at all in a password box. That text is
//    replayed here as keydown/keyup events, which Avalonia turns into text input as usual.
//
// 2. A word being composed when another field is tapped (Gboard). Avalonia moves its focus on the
//    touch, *before* the browser finishes the word, so the word landed in the newly chosen field
//    (e.g. the email in the password box) and the old field kept only its vanishing preview. When a
//    touch starts while composing, the hidden input is blurred first, which makes the browser commit
//    the word while the old field still has Avalonia's focus. The window capture listener runs
//    before Avalonia's own pointer handling.
//
// Desktop browsers type through real keydowns and rarely compose, so neither changes anything there.
// Diagnostics for a phone: ?imelog shows the keyboard events on screen; ?noimefix turns the fixes off.

const params = new URLSearchParams(location.search);
if (params.has('imelog')) showLog(params.has('noimefix'));

if (!params.has('noimefix')) {
  let composing = false;
  document.addEventListener('compositionstart', () => { composing = true; }, true);
  document.addEventListener('compositionend', () => { composing = false; }, true);

  // 1. Replay committed text as key presses.
  document.addEventListener('beforeinput', e => {
    if (e.inputType !== 'insertText' || !e.data || composing || e.isComposing) return;
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || !input.classList.contains('avalonia-input-element')) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    for (const ch of e.data) {
      for (const type of ['keydown', 'keyup'])
        input.dispatchEvent(new KeyboardEvent(type, { key: ch, bubbles: true, cancelable: true }));
    }
  }, true);

  // 2. Finish the word before another field takes the focus.
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
    document.addEventListener(t, e => add(t + ' key=' + JSON.stringify(e.key) + ' code=' + e.keyCode + (e.isComposing ? ' comp' : '') + (e.isTrusted ? '' : ' replay') + ' ' + tag(e.target)), true);
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
