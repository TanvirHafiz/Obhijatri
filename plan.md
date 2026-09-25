# plan.md: অভিযাত্রী (Obhijatri) Browser, v1

Handoff plan for Claude Code. Recommended setup: **Claude Sonnet 5 at medium effort**.
Work one milestone per session. Do not start the next milestone until the owner (Tanvir) has tested the current one and said "approved".

---

## 0. Read this first (rules for Claude Code)

1. **One milestone at a time.** Finish, build, test, then STOP and report. Never jump ahead.
2. **Always build before reporting.** Run `dotnet build` (and tests from Milestone 2 onward). Report errors honestly; never claim something works without running it.
3. **Ask before adding any NuGet package** not listed in section 3. Explain why it is needed.
4. **No telemetry, no analytics, no cloud AI.** No page content ever leaves the PC except the privacy-preserving checks named in this plan (Safe Browsing hash prefixes, HIBP k-anonymity range queries).
5. **Every UI string lives in resource files** (`Strings/bn-BD/Resources.resw` and `Strings/en-US/Resources.resw`). No hardcoded user-facing text in XAML or C#.
6. **Bangla is the default language.** English is the fallback and a settings toggle.
7. **No em dashes (the long dash character) in any UI string, message, or doc.** Use commas, colons, or full stops instead.
8. **Keep it light.** Performance budgets in section 6 are requirements, not goals.
9. **Security fixes over features.** If you notice a security problem in earlier code, flag it at the top of your report.
10. **Update `PROGRESS.md`** at the end of every session: what was done, what was tested, known issues, next milestone.

---

## 1. Product summary

**Name:** অভিযাত্রী (Obhijatri), meaning "voyager". Use "অভিযাত্রী" in the Bangla UI and "Obhijatri" in English UI, file names, and code identifiers.

A lightweight Windows 10/11 browser positioned as **the safest browser for Bangla speakers**.

Headline features:
- Full Bangla UI, with Bangla numerals throughout.
- Built-in Avro-style Bangla phonetic typing that works in every text box on every website.
- A local Bangla scam shield (fake bKash/Nagad/Rocket pages, fake prize/job/SIM-block scams, lookalike domains) with plain-Bangla warnings.
- An "এটা কি প্রতারণা?" (Is this a scam?) button that works offline, with optional local AI explanations via Ollama.

Target users: non-technical Bangla speakers, older family members, low-end PCs (4 GB RAM), mobile hotspot users.

---

## 2. Environment prerequisites (owner sets up once)

- Windows 10 (22H2) or Windows 11
- Visual Studio 2022 with the ".NET desktop development" and "Windows App SDK C# templates" workloads, or the .NET 8 SDK plus Windows App SDK
- WebView2 Evergreen Runtime (preinstalled on Windows 11)
- Git
- Optional: Ollama running locally (only needed for Milestone 10, layer 2)
- Optional: a Google Safe Browsing API key (Milestone 6). Stored in a local user secrets file, never committed.

---

## 3. Tech stack (fixed for v1)

| Layer | Choice |
| --- | --- |
| Language / runtime | C# on .NET 8 |
| UI | WinUI 3 (Windows App SDK), Mica backdrop |
| Web engine | Microsoft WebView2 (Evergreen) |
| Storage | SQLite via `Microsoft.Data.Sqlite` |
| Tests | xUnit |
| Packaging | MSIX plus a small EXE bootstrapper that installs WebView2 if missing |

Approved packages: `Microsoft.WindowsAppSDK`, `Microsoft.Web.WebView2`, `Microsoft.Data.Sqlite`, `CommunityToolkit.Mvvm`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`.

Useful WebView2 APIs (verify exact names against the current SDK docs before use):
- `CoreWebView2EnvironmentOptions.Language = "bn"` for Bangla engine menus and dialogs
- `CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync` for injected scripts
- `CoreWebView2.AddWebResourceRequestedFilter` + `WebResourceRequested` for request blocking
- `CoreWebView2.TrySuspendAsync` / `Resume` for tab sleeping
- `CoreWebView2Profile.PreferredTrackingPreventionLevel` for extra tracker protection
- Separate `CoreWebView2` profiles for private windows

---

## 4. Solution layout

```
Obhijatri.sln
src/
  Obhijatri.App/         WinUI 3 shell: windows, tab strip, address bar, settings, new tab page
    Strings/bn-BD/Resources.resw
    Strings/en-US/Resources.resw
    Assets/Fonts/       Noto Sans Bengali, Hind Siliguri (check licenses)
    Web/                Local HTML/JS/CSS for warning pages, new tab page, injected scripts
  Obhijatri.Core/        Tab manager, session restore, history, bookmarks, settings, SQLite
  Obhijatri.Safety/      Scam shield, lookalike detector, Safe Browsing client, HIBP client,
                       download scanning, filter-list engine, permission store
  Obhijatri.Bangla/      Phonetic engine, Bijoy-to-Unicode converter, Bangla numerals,
                       Bangla (Bangabda) and Hijri calendar helpers
  Obhijatri.AI/          Rule-based scam scorer, optional Ollama client
tests/
  Obhijatri.Tests/       Unit tests for Safety, Bangla and AI projects
  Fixtures/            Test URLs, fake scam page HTML, Bijoy samples
CLAUDE.md              Rules from section 0, kept short
PROGRESS.md            Session log
```

---

## 5. Milestones

Each milestone lists **Tasks**, **Acceptance criteria** (Claude Code checks these), and **Owner test** (Tanvir checks these by hand).

### Milestone 1: Shell and single tab
Tasks
- Create the solution and projects from section 4. Create `CLAUDE.md` (rules from section 0) and `PROGRESS.md`.
- Main window with Mica backdrop, one WebView2, address bar, back, forward, reload, home.
- WebView2 environment created with `Language = "bn"`.
- Address bar: if input looks like a URL, navigate; otherwise search with Google (configurable later).

Acceptance
- `dotnet build` succeeds with zero warnings treated as errors in new code.
- App launches, loads `https://www.prothomalo.com`, back/forward/reload work.

Owner test
- Right-click a page: context menu appears in Bangla.

### Milestone 2: Tabs, history, bookmarks, downloads panel
Tasks
- Tab strip: new, close, reorder by drag, middle-click close, Ctrl+T, Ctrl+W, Ctrl+Tab, Ctrl+Shift+T.
- Vertical tabs toggle (setting).
- SQLite database in `%LOCALAPPDATA%\Obhijatri\`: history, bookmarks, settings, sessions.
- History page with search and "clear last hour / day / all".
- Bookmark bar, folders, import from Chrome/Edge HTML bookmark export.
- Downloads panel (progress, open, show in folder). Scanning comes in Milestone 7.
- Private window using a separate in-memory profile.
- Lazy session restore: only the active tab loads on start.
- Add the test project and first unit tests (history and bookmark storage).

Acceptance
- 20 tabs open and close with no crash and no leaked WebView2 processes after closing.
- Bookmarks and history persist across restarts. Private window leaves no history.

Owner test
- Import bookmarks from Chrome. Restart the app and confirm tabs restore.

### Milestone 3: Full Bangla UI
Tasks
- Move every user-facing string into `bn-BD` and `en-US` resource files.
- Settings page sections: সাধারণ, নিরাপত্তা, গোপনীয়তা, চেহারা, উন্নত.
- Language toggle (Bangla / English), applies after restart is acceptable for v1.
- Bangla numerals helper in `Obhijatri.Bangla`; use it for tab counts, download sizes, dates, RAM meter.
- Bundle fonts; set the app UI font to a good Bangla font.

Acceptance
- A search of the source for hardcoded user-facing strings finds none.
- Unit tests: numeral conversion (0-9 to ০-৯, decimals, thousands separators).

Owner test
- Read every screen in Bangla and list any awkward translations for correction.

### Milestone 4: Bangla phonetic typing
Tasks
- Phonetic engine in `Obhijatri.Bangla` (C#) for the address bar, and an equivalent injected JS module for web pages.
- Use an existing open-source Avro-style phonetic rule set if its license allows bundling; confirm the license and record it in `THIRD_PARTY_NOTICES.md`. If unclear, stop and ask the owner.
- Toggle: Ctrl+M and a floating "অ/A" button; state remembered per site.
- Works in `input`, `textarea` and `contenteditable` elements, including inside iframes where allowed.
- Suggestion dropdown for ambiguous words with a small built-in word list.
- Must not break password fields: never active in `type="password"`.

Acceptance
- Unit tests: "ami banglay gan gai" becomes "আমি বাংলায় গান গাই"; at least 50 test words pass.
- Phonetic mode is never active in password fields (test with a fixture page).

Owner test
- Type in Google search, Facebook post box, Gmail compose and the address bar.

### Milestone 5: Ad/tracker blocking and HTTPS-only
Tasks
- Filter engine in `Obhijatri.Safety`: start with domain and simple URL-pattern rules from EasyList and EasyPrivacy, plus a small BD-specific list in `Fixtures`/`Assets`. Full Adblock Plus syntax is not required in v1.
- Lists stored locally, updated weekly in the background, checked for integrity before use.
- Apply blocking via `WebResourceRequested`. Measure the cost; if the catch-all filter slows page loads noticeably, narrow the filters and report the numbers.
- Set tracking prevention to strict.
- HTTPS-only: upgrade HTTP to HTTPS; if HTTPS fails, show a local Bangla warning page with "continue anyway".
- Per-site "allow ads on this site" toggle.

Acceptance
- Unit tests for rule matching.
- Page load time on 5 BD news sites is equal or faster than with blocking off (report numbers).

Owner test
- Visit Prothom Alo, Kaler Kantho, Jugantor, bdnews24, The Daily Star: ads gone, pages not broken.

### Milestone 6: Scam shield and lookalike domain alarm
Tasks
- Lookalike detector: punycode/homoglyph detection, edit-distance checks against a protected list (bKash, Nagad, Rocket, major BD banks, Facebook, Google, Daraz, government portals, PayPal and other global targets).
- BD scam list: local JSON, signed, updated daily from a project-hosted URL (URL is a setting; leave a placeholder).
- Google Safe Browsing Update API client using hash prefixes (no full URLs sent). If no API key is configured, skip gracefully.
- Full-screen Bangla warning page (local HTML) with: what the risk is in plain words, "go back to safety" (primary), "I understand, continue" (small, secondary).
- Test fixtures: 30 fake-looking URLs that must be flagged and 30 real sites that must not. Use made-up domains in unit tests; do not visit real scam sites.

Acceptance
- All 30 fake fixtures flagged; zero false positives on the 30 real ones.
- Check adds under 20 ms per navigation (report timing).

Owner test
- Type `bkash-verify.xyz` and a punycode lookalike of `facebook.com`: warning appears in Bangla.

### Milestone 7: Downloads, leak check, permissions, clipboard guard, payment lock
Tasks
- Download scanning: after download completes, invoke Windows' attachment scanning (for example `IAttachmentExecute`, which also applies Mark of the Web) so Defender scans it. Block opening until the scan finishes. Block double extensions like `.pdf.exe`.
- Password leak check: when a login form is submitted, SHA-1 the password locally, send only the first 5 hex characters to the HIBP range API, compare locally, warn in Bangla if found.
- Permission dashboard: list per-site camera, mic, location, notifications; one-click revoke. Deny notification requests by default with a quiet "allow?" chip in the address bar.
- Clipboard guard: injected script detects clipboard writes the user did not trigger; warn in Bangla.
- Payment lock mode: on a list of banking/payment domains, block third-party scripts, show a green "নিরাপদ মোড" badge.
- Cookie auto-delete (opt-in setting).

Acceptance
- EICAR test file is caught and blocked.
- Unit test: HIBP client only ever sends 5 characters (mock the HTTP client).

Owner test
- Try a known-leaked test password like `password123` on a test login page: Bangla warning appears.

### Milestone 8: Performance features
Tasks
- Tab sleeping via `TrySuspendAsync` after 10 minutes idle (setting), skip audio-playing and pinned tabs.
- RAM meter: per-tab on hover, total in toolbar, "memory saved today" counter.
- Low-data mode: block autoplay, lazy-load images, block heavy embeds.
- Measure and report against the performance budgets in section 6.

Acceptance
- Budgets in section 6 met on the owner's test machine, or a clear report of which are missed and why.

Owner test
- Open 20 tabs on a 4 GB RAM PC and use it for 15 minutes.

### Milestone 9: New tab page and Bangla extras
Tasks
- New tab page (local HTML): date in English, Bangla (Bangabda, using the revised Bangladesh calendar) and Hijri; Bangla numerals; prayer times for a chosen district (calculated locally, no API); speed-dial tiles.
- Bijoy to Unicode converter: detect SutonnyMJ/Bijoy-encoded text and offer one-click conversion.
- "Fix Bangla fonts" option: override site fonts for Bangla text only.
- Translate page to Bangla: use the engine's built-in translate if available; otherwise hide the button unless Ollama is enabled.
- Reader mode plus read-aloud using the Windows Bangla voice if installed; if not, show how to install it.

Acceptance
- Unit tests: Bangabda date for at least 10 known dates (including Pohela Boishakh, 14 April), Bijoy conversion on fixture samples.

Owner test
- Check today's Bangla date and prayer times for Dhaka against a trusted source.

### Milestone 10: "এটা কি প্রতারণা?" button
Tasks
- Layer 1 (always works, offline): rule-based scorer in `Obhijatri.AI` using signals: lookalike score, domain on scam list, login/payment/OTP/PIN forms on non-official domains, urgency words in Bangla and English, countdown timers, prize claims. Output: green / yellow / red plus top 3 reasons in Bangla.
- Layer 2 (optional): if Ollama is reachable on localhost, send page text plus Layer 1 signals to a local model and show a short plain-Bangla explanation. Model name is a setting. Time out after 20 seconds and fall back to Layer 1.
- Always label the verdict as an estimate; never say "100% safe".

Acceptance
- Unit tests on fixture pages: all fake scam fixtures score yellow or red; clean fixtures score green.
- Works with networking disabled (Layer 1).

Owner test
- Try on a real bank site, a news site, and a fixture scam page with Ollama on and off.

### Milestone 11: Installer and polish
Tasks
- MSIX package plus EXE bootstrapper that installs the WebView2 runtime if missing.
- App icon placeholder, "About" page with version and third-party notices.
- Crash-safe settings and database (recover from a corrupt file by backing it up and starting fresh).
- Final pass for hardcoded strings, em dashes, and unused code.

Acceptance
- Installer under 15 MB. Clean install and uninstall on a fresh Windows 10 VM.

Owner test
- Install on a second PC and use it as the daily browser for 2 days; report bugs.

---

## 6. Performance budgets

| Metric | Budget |
| --- | --- |
| Installer size | under 15 MB |
| Cold start to usable window (SSD) | under 1 second |
| Idle RAM, shell plus one blank tab | under 150 MB (shell process; report WebView2 processes separately) |
| 20 tabs on a 4 GB RAM PC | usable, no swapping stalls, with sleeping on |
| Safety check per navigation | under 20 ms |

---

## 7. Out of scope for v1 (do not build)

Local AI sidebar, smart tab cleanup, command palette, split view, site privacy report card, Chrome extension support, sync, built-in password manager, Android version, any cloud AI.

---

## 8. Session prompt template

Paste this at the start of each Claude Code session (change the number):

```
Read plan.md, CLAUDE.md and PROGRESS.md. Implement Milestone N only.
Follow the rules in section 0. When done: build, run tests, update PROGRESS.md,
and report what works, what you tested, known issues, and anything you need from me.
Then stop and wait for my approval.
```

If a milestone feels too big for one session, split it into parts (for example 7a, 7b), say so at the start, and stop after each part.

---

## 9. Open decisions (owner)

- Logo for অভিযাত্রী (Obhijatri). The name is decided.
- Default search engine.
- Open source or closed source.
- Who maintains the BD scam list and where it is hosted.
- License confirmation for the phonetic rule set and bundled fonts.
