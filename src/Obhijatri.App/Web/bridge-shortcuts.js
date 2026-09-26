// Browser shortcuts while a page has focus. WebView2 in WinUI 3 does not pass these to the app.
// Only real key presses (isTrusted) are forwarded.
const pickShortcut = (e) => {
  const k = e.key.length === 1 ? e.key.toLowerCase() : e.key;
  const ctrl = e.ctrlKey && !e.altKey && !e.metaKey;
  if (ctrl && !e.shiftKey) {
    if (k === "t") return "NewTab";
    if (k === "w" || k === "F4") return "CloseTab";
    if (k === "Tab") return "NextTab";
    if (k === "l") return "FocusAddressBar";
    if (k === "h") return "History";
    if (k === "j") return "Downloads";
    if (k === "d") return "Bookmark";
    if (k === ",") return "Settings";
  }
  if (ctrl && e.shiftKey) {
    if (k === "t") return "ReopenTab";
    if (k === "Tab") return "PreviousTab";
    if (k === "n") return "PrivateWindow";
    if (k === "b") return "ToggleBookmarkBar";
  }
  if (e.altKey && !e.ctrlKey && !e.shiftKey && !e.metaKey && k === "d") return "FocusAddressBar";
  if (!e.altKey && !e.ctrlKey && !e.shiftKey && !e.metaKey && k === "F6") return "FocusAddressBar";
  return null;
};
window.addEventListener("keydown", (e) => {
  if (!e.isTrusted) { return; }
  const command = pickShortcut(e);
  if (!command) { return; }
  e.preventDefault();
  e.stopImmediatePropagation();
  send(command);
}, true);
