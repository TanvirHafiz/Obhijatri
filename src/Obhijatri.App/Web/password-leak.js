// Password leak check (Milestone 7). On a real, user-triggered form submission, hashes any
// non-empty password field with SHA-1 (Web Crypto, built into the browser engine) and sends only
// that hash to the app. The password itself, and the hash, never go anywhere else: the app checks
// the hash against Have I Been Pwned using only the first 5 hex characters (k-anonymity).
document.addEventListener("submit", (e) => {
  if (!e.isTrusted) { return; }
  const form = e.target;
  if (!(form instanceof HTMLFormElement)) { return; }
  const fields = form.querySelectorAll('input[type="password"]');
  for (const field of fields) {
    if (field.value) { hashAndSendPassword(field.value); }
  }
}, true);

async function hashAndSendPassword(password) {
  try {
    if (!crypto.subtle) { return; }
    const bytes = new TextEncoder().encode(password);
    const digest = await crypto.subtle.digest("SHA-1", bytes);
    const hex = Array.from(new Uint8Array(digest)).map((b) => b.toString(16).padStart(2, "0")).join("");
    send("PasswordHash", hex);
  } catch (err) {
    // Web Crypto unavailable (non-secure context) or a transient error: nothing to send.
  }
}
