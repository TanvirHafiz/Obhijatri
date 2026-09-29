// Clipboard guard (Milestone 7): warns when a page writes to the clipboard without the user
// having just pressed a copy shortcut or clicked a moment before. A page reading the clipboard
// needs a permission prompt already (Milestone 7's permission dashboard covers ClipboardRead
// indirectly through the engine's own prompt); this only watches writes, which the platform does
// not gate at all.
let lastUserAction = 0;
const markUserAction = () => { lastUserAction = Date.now(); };
document.addEventListener("keydown", markUserAction, true);
document.addEventListener("mousedown", markUserAction, true);
document.addEventListener("copy", markUserAction, true);
document.addEventListener("cut", markUserAction, true);

const RECENT_MS = 1500;
let lastWarnedAt = 0;
const WARN_COOLDOWN_MS = 4000;

function warnIfUnexpected() {
  const sinceAction = Date.now() - lastUserAction;
  if (sinceAction <= RECENT_MS) { return; }
  const now = Date.now();
  if (now - lastWarnedAt < WARN_COOLDOWN_MS) { return; }
  lastWarnedAt = now;
  send("ClipboardWrite");
}

if (window.ClipboardItem && navigator.clipboard && navigator.clipboard.writeText) {
  const originalWriteText = navigator.clipboard.writeText.bind(navigator.clipboard);
  navigator.clipboard.writeText = (text) => {
    warnIfUnexpected();
    return originalWriteText(text);
  };
  if (navigator.clipboard.write) {
    const originalWrite = navigator.clipboard.write.bind(navigator.clipboard);
    navigator.clipboard.write = (items) => {
      warnIfUnexpected();
      return originalWrite(items);
    };
  }
}

// The older execCommand("copy") path, still used by some sites.
const originalExecCommand = document.execCommand ? document.execCommand.bind(document) : null;
if (originalExecCommand) {
  document.execCommand = (command, ...rest) => {
    if (typeof command === "string" && command.toLowerCase() === "copy") {
      warnIfUnexpected();
    }
    return originalExecCommand(command, ...rest);
  };
}
