// Bangla phonetic typing in web pages (Obhijatri). Uses the Avro engine from avro-phonetic.js,
// the shared RULES, and the localised LABELS provided by the app.
//
// How it works: while it is on, letters typed into a text field are kept as a romanised "word"
// and the Bangla for that word is shown in its place ("ami" shows আমি). A space, punctuation,
// a click or an arrow key ends the word. Text is inserted with the editor's own insertText
// command, so undo, React, Facebook and Gmail all see normal typing.
//
// Safety: it never runs in password fields, fields that look like a PIN, OTP or card code,
// or email, number, phone and URL fields. The check is repeated on every key press, so a
// "show password" button that turns a password field into a text field is still skipped.

const avro = createAvroPhonetic(RULES);
const ruleChars = new Set();
for (const pattern of RULES.patterns) {
  for (const ch of pattern.find) {
    if (!/[A-Za-z0-9]/.test(ch)) { ruleChars.add(ch); }
  }
}
const isConvertible = (ch) => ch.length === 1 && ch !== " " && (/[A-Za-z0-9]/.test(ch) || ruleChars.has(ch));

// Keep our own references so later page scripts cannot change what these calls do.
const nativeExecCommand = Document.prototype.execCommand;
const exec = (command, value) => nativeExecCommand.call(document, command, false, value);

const TEXT_INPUT_TYPES = new Set(["", "text", "search"]);
const SECRET_HINT = /pass|pwd|pin|otp|secret|cvv|cvc/i;

let enabled = false;
let roman = "";
let shown = "";
let target = null;
let suggestions = [];
let selected = 0;
let requestId = 0;

const deepActive = () => {
  let el = document.activeElement;
  while (el && el.shadowRoot && el.shadowRoot.activeElement) { el = el.shadowRoot.activeElement; }
  return el;
};

const looksSecret = (el) => {
  const auto = (el.getAttribute("autocomplete") || "").toLowerCase();
  if (auto.includes("password") || auto === "one-time-code" || auto.startsWith("cc-")) { return true; }
  return SECRET_HINT.test((el.getAttribute("name") || "") + " " + (el.id || ""));
};

const editableOf = (el) => {
  if (!el) { return null; }
  if (el instanceof HTMLInputElement) {
    const type = (el.getAttribute("type") || "").toLowerCase();
    if (!TEXT_INPUT_TYPES.has(type) || el.type === "password") { return null; }
    if (el.readOnly || el.disabled || looksSecret(el)) { return null; }
    return el;
  }
  if (el instanceof HTMLTextAreaElement) { return el.readOnly || el.disabled ? null : el; }
  if (el.isContentEditable) { return el; }
  return null;
};

const isTextControl = (el) => el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement;

// Is the Bangla we showed last still right before the caret? If not, the user moved on.
const contextOk = (el) => {
  if (!shown) { return true; }
  if (isTextControl(el)) {
    const end = el.selectionStart;
    return end === el.selectionEnd && end >= shown.length && el.value.slice(end - shown.length, end) === shown;
  }
  const sel = document.getSelection();
  if (!sel || !sel.isCollapsed || !sel.focusNode || sel.focusNode.nodeType !== Node.TEXT_NODE) { return false; }
  const offset = sel.focusOffset;
  return offset >= shown.length && sel.focusNode.data.slice(offset - shown.length, offset) === shown;
};

const fireInput = (el, data) => {
  el.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: data ? "insertText" : "deleteContentBackward", data }));
};

// Replace the Bangla shown for the current word with `next`.
const replaceShown = (el, next) => {
  if (isTextControl(el)) {
    const end = el.selectionStart;
    const start = end - shown.length;
    if (shown) { el.setSelectionRange(start, end); }
    if (next) {
      if (!exec("insertText", next)) { el.setRangeText(next, start, end, "end"); fireInput(el, next); }
    } else if (shown) {
      if (!exec("delete")) { el.setRangeText("", start, end, "end"); fireInput(el, null); }
    }
  } else {
    const sel = document.getSelection();
    if (shown && sel && sel.focusNode && sel.focusNode.nodeType === Node.TEXT_NODE) {
      const node = sel.focusNode;
      const offset = sel.focusOffset;
      const range = document.createRange();
      range.setStart(node, offset - shown.length);
      range.setEnd(node, offset);
      sel.removeAllRanges();
      sel.addRange(range);
    }
    if (next) { exec("insertText", next); } else if (shown) { exec("delete"); }
  }
  shown = next;
};

const reset = () => {
  roman = "";
  shown = "";
  hidePopup();
};

const setEnabled = (value, fromUser) => {
  enabled = value;
  reset();
  updateToggle();
  if (fromUser) { send("Phonetic", value ? "1" : "0"); }
};

const requestSuggestions = () => {
  requestId++;
  send("Suggest", requestId + ":" + roman);
};

const choose = (index) => {
  if (target && suggestions[index] && contextOk(target)) { replaceShown(target, suggestions[index]); }
  reset();
};

// ---- On-page controls: the অ/A button and the suggestion list, in a closed shadow root ----

let ui = null;

const style = (el, css) => { for (const key of Object.keys(css)) { el.style.setProperty(key, css[key]); } };

const ensureUi = () => {
  if (ui) {
    if (!ui.host.isConnected) { document.documentElement.appendChild(ui.host); }
    return ui;
  }
  const host = document.createElement("obhijatri-typing");
  style(host, { all: "initial", position: "fixed", top: "0", left: "0", width: "0", height: "0", "z-index": "2147483647" });
  const root = host.attachShadow({ mode: "closed" });

  const toggle = document.createElement("button");
  toggle.type = "button";
  style(toggle, {
    position: "fixed", display: "none", width: "26px", height: "26px", padding: "0", margin: "0",
    border: "1px solid rgba(0,0,0,0.25)", "border-radius": "13px", cursor: "pointer",
    font: "600 13px 'Hind Siliguri', 'Nirmala UI', 'Segoe UI', sans-serif", "line-height": "24px",
    "text-align": "center", "box-shadow": "0 1px 3px rgba(0,0,0,0.3)",
  });
  toggle.addEventListener("mousedown", (e) => e.preventDefault());
  toggle.addEventListener("click", (e) => {
    if (!e.isTrusted) { return; }
    setEnabled(!enabled, true);
    if (target) { target.focus(); }
  });

  const popup = document.createElement("div");
  popup.setAttribute("role", "listbox");
  style(popup, {
    position: "fixed", display: "none", "min-width": "120px", padding: "4px", "border-radius": "6px",
    background: "#ffffff", color: "#1a1a1a", border: "1px solid rgba(0,0,0,0.2)",
    "box-shadow": "0 4px 12px rgba(0,0,0,0.25)",
    font: "16px 'Hind Siliguri', 'Nirmala UI', 'Segoe UI', sans-serif",
  });

  root.appendChild(toggle);
  root.appendChild(popup);
  document.documentElement.appendChild(host);
  ui = { host, toggle, popup };
  return ui;
};

const updateToggle = () => {
  const el = editableOf(deepActive());
  if (!el) {
    if (ui) { ui.toggle.style.display = "none"; }
    return;
  }
  const { toggle } = ensureUi();
  const rect = el.getBoundingClientRect();
  if (rect.width < 40 || rect.height < 16 || rect.bottom < 0 || rect.top > innerHeight) {
    toggle.style.display = "none";
    return;
  }
  toggle.textContent = enabled ? "অ" : "A";
  toggle.title = enabled ? LABELS.on : LABELS.off;
  toggle.setAttribute("aria-label", toggle.title);
  toggle.setAttribute("aria-pressed", enabled ? "true" : "false");
  style(toggle, enabled
    ? { background: "#0f6cbd", color: "#ffffff" }
    : { background: "#f3f3f3", color: "#444444" });
  const multiLine = !(el instanceof HTMLInputElement) && rect.height > 40;
  const top = multiLine ? rect.bottom - 32 : rect.top + (rect.height - 26) / 2;
  style(toggle, {
    display: "block",
    left: Math.max(0, Math.min(innerWidth - 30, rect.right - 32)) + "px",
    top: Math.max(0, Math.min(innerHeight - 30, top)) + "px",
  });
};

const popupVisible = () => ui !== null && ui.popup.style.display !== "none" && suggestions.length > 1;

const hidePopup = () => {
  // Also drop any suggestion reply still on its way, so it cannot reopen the list.
  requestId++;
  suggestions = [];
  selected = 0;
  if (ui) { ui.popup.style.display = "none"; }
};

const caretRect = (el) => {
  if (!isTextControl(el)) {
    const sel = document.getSelection();
    if (sel && sel.rangeCount) {
      const r = sel.getRangeAt(0).getBoundingClientRect();
      if (r.width || r.height) { return { left: r.left, bottom: r.bottom }; }
    }
  }
  const r = el.getBoundingClientRect();
  return { left: r.left, bottom: isTextControl(el) && el instanceof HTMLInputElement ? r.bottom : Math.min(r.bottom, r.top + 40) };
};

const renderPopup = () => {
  const { popup } = ensureUi();
  popup.replaceChildren();
  suggestions.forEach((text, index) => {
    const item = document.createElement("div");
    item.setAttribute("role", "option");
    item.setAttribute("aria-selected", index === selected ? "true" : "false");
    item.textContent = text;
    style(item, {
      padding: "3px 10px", "border-radius": "4px", cursor: "pointer", "white-space": "nowrap",
      background: index === selected ? "#0f6cbd" : "transparent",
      color: index === selected ? "#ffffff" : "#1a1a1a",
    });
    item.addEventListener("mousedown", (e) => e.preventDefault());
    item.addEventListener("click", (e) => { if (e.isTrusted) { choose(index); } });
    popup.appendChild(item);
  });
};

const showPopup = () => {
  if (!target || suggestions.length < 2) { hidePopup(); return; }
  renderPopup();
  const { popup } = ui;
  const at = caretRect(target);
  style(popup, {
    display: "block",
    left: Math.max(0, Math.min(innerWidth - 180, at.left)) + "px",
    top: Math.max(0, Math.min(innerHeight - 40, at.bottom + 4)) + "px",
  });
};

// ---- Messages from the app ----

hostHandlers.Phonetic = (payload) => {
  enabled = payload === "1";
  reset();
  updateToggle();
};

hostHandlers.Suggest = (payload) => {
  const colon = payload.indexOf(":");
  if (colon < 0 || Number(payload.slice(0, colon)) !== requestId || !roman) { return; }
  let list;
  try { list = JSON.parse(payload.slice(colon + 1)); } catch { return; }
  if (!Array.isArray(list)) { return; }
  suggestions = list.filter((s) => typeof s === "string").slice(0, 8);
  selected = 0;
  showPopup();
};

// ---- Keyboard and focus ----

window.addEventListener("keydown", (e) => {
  if (!e.isTrusted || e.isComposing) { return; }

  if (e.ctrlKey && !e.altKey && !e.metaKey && !e.shiftKey && (e.key === "m" || e.key === "M")) {
    e.preventDefault();
    e.stopImmediatePropagation();
    setEnabled(!enabled, true);
    return;
  }
  if (!enabled) { return; }

  const el = editableOf(deepActive());
  if (!el) { reset(); return; }
  if (el !== target) { reset(); target = el; }
  if (e.ctrlKey || e.altKey || e.metaKey) { reset(); return; }

  if (popupVisible()) {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      e.stopImmediatePropagation();
      selected = (selected + (e.key === "ArrowDown" ? 1 : suggestions.length - 1)) % suggestions.length;
      renderPopup();
      return;
    }
    if ((e.key === "Enter" || e.key === "Tab") && selected > 0) {
      e.preventDefault();
      e.stopImmediatePropagation();
      choose(selected);
      return;
    }
    if (e.key === "Escape") {
      e.preventDefault();
      e.stopImmediatePropagation();
      hidePopup();
      return;
    }
  }

  if (e.key === "Backspace") {
    if (roman && contextOk(el)) {
      e.preventDefault();
      e.stopImmediatePropagation();
      roman = roman.slice(0, -1);
      replaceShown(el, roman ? avro.parse(roman) : "");
      if (roman) { requestSuggestions(); } else { hidePopup(); }
    } else {
      reset();
    }
    return;
  }

  if (isConvertible(e.key)) {
    if (!contextOk(el)) { roman = ""; shown = ""; }
    e.preventDefault();
    e.stopImmediatePropagation();
    roman += e.key;
    replaceShown(el, avro.parse(roman));
    requestSuggestions();
    return;
  }

  // Space with a different suggestion picked: use it, then let the space through.
  if (e.key === " " && popupVisible() && selected > 0 && contextOk(el)) {
    replaceShown(el, suggestions[selected]);
  }
  reset();
}, true);

window.addEventListener("mousedown", (e) => {
  if (ui && e.composedPath().includes(ui.host)) { return; }
  reset();
}, true);

let repositionQueued = false;
const queueReposition = () => {
  if (repositionQueued) { return; }
  repositionQueued = true;
  requestAnimationFrame(() => {
    repositionQueued = false;
    updateToggle();
    if (popupVisible()) { showPopup(); }
  });
};

document.addEventListener("focusin", () => { reset(); target = editableOf(deepActive()); queueReposition(); }, true);
document.addEventListener("focusout", () => { reset(); queueReposition(); }, true);
window.addEventListener("scroll", queueReposition, true);
window.addEventListener("resize", queueReposition);

// Ask the app whether typing is on for this site.
send("PhoneticHello");
