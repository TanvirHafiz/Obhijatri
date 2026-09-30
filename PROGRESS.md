# PROGRESS.md

## Session 12 (2026-09-30): Milestone 11, installer and polish

Status: **approved by the owner.** Committed and published: source on https://github.com/TanvirHafiz/Obhijatri, installer as release v1.0.0.
Owner result: installed on a second Windows 11 laptop with Setup.exe, works well. (Setup.exe was changed after the first test build to trust the test certificate itself; a Wi-Fi-off failure was a network problem, not a bug.) Uninstall and the 2-day use test were not reported.

### Read first
- **The installer has NOT been installed or uninstalled on any machine.** There is no fresh Windows 10 VM here, and I did not
  install a package or trust a certificate on your PC. What I did verify: the MSIX builds, is signed, and is 13.17 MB;
  `Setup.exe` compiles; the trimmed app (without the Windows AI libraries) runs; 617 tests pass. The packaged run (identity, data
  redirection, the App Runtime dependency) and `Setup.exe` are untested. Please run the second-PC test first, see "Owner test".
- **Size:** the plan asked for under 15 MB. A self-contained app is 157 MB, so the package is framework dependent: `Obhijatri.msix`
  13.17 MB + `Setup.exe` 10 KB, 12.8 MB zipped. The price: a PC that has none of the prerequisites downloads about 180 MB during
  setup (WebView2 1.8 MB, .NET 8 Desktop Runtime 59 MB, Windows App Runtime 2.5 120 MB), each checked for a Microsoft signature.
  Windows 10/11 PCs with Edge/WebView2 already have the first one.
- **Real release needs a real code signing certificate.** The test build is signed `CN=Obhijatri` (test certificate). Windows will
  refuse it until the `.cer` is trusted (see `tools/package/README-installer.txt`, needs Administrator). The manifest Publisher must
  equal the certificate subject.

### What was built
- **Crash-safe data:** `BrowserDatabase.OpenOrRecover`: a corrupt or non-database file is moved to `obhijatri.db.corrupt-<stamp>`
  (with its -wal/-shm), a fresh database starts, and a one-time info bar tells the user. Newest 3 backups kept. Real disk or
  permission errors are not treated as corruption. If even that fails, the app runs on a temporary in-memory database.
  Settings already fell back to defaults on a bad file. 14 new tests. Bug found by them: a rejected file kept the connection open
  and locked the file; `Create()` now disposes it.
- **About page** (`obhijatri://about`, menu item): version, privacy statement, engine version, data folder (MSIX aware, `DataFolder`),
  third-party notices (embedded) and a folder with the MPL source files (`Legal`, also shipped in the app).
- **Icon placeholders** (`tools/make_icons.ps1`): window icon, exe icon, tile and store logos. Replace with real art later.
- **Packaging** (`tools/package`): `build-package.ps1` (publish framework dependent, delete unused onnxruntime/DirectML/Windows AI
  libs, fill `AppxManifest.template.xml`, `makeappx`, sign), `build-installer.ps1` (adds `Setup.exe` built by the Windows
  .NET Framework compiler, plus README). The MSBuild MSIX tooling is not used. Output in `artifacts/` (git ignored).
- The app skips the Windows App SDK bootstrap when it has package identity (`OnPackageIdentity_NoOp`).
- Cleanup pass: no hardcoded UI strings, no dashes (checker passes, 479 strings), THIRD_PARTY_NOTICES stray character fixed.

### What was tested
- Debug and Release build: 0 warnings, 0 errors. 617 tests passing. `check_strings` passes. Phonetic JS 95/95.
- About page screenshot (`--about-selftest`) looks right.
- Package builds and signs; `Setup.exe` compiles. Prerequisite links answer (200) with the expected sizes.

### Known issues
- Not tested: install, launch from Start menu, uninstall (data folder removal), upgrade, `Setup.exe` download flow, ARM64.
- `Setup.exe` prints Bangla in a console; old consoles may show boxes. Use `/en` for English.
- Windows App Runtime detection uses `Get-AppxPackage` for the current user.
- No auto-update yet.

### Owner test (from plan.md)
Second PC, use for 2 days. Steps: copy the installer folder, trust the test cert (README), run `Setup.exe`, then use normally.

### Next
Nothing planned after Milestone 11 in plan.md except fixes from your 2-day test.

## Session 11 (2026-09-29): Milestone 10, "এটা কি প্রতারণা?" (Is this a scam?)

Status: **awaiting owner approval.** Milestone 9 (9a and 9b) was approved and committed (37637b7).
Not committed yet: this milestone.

### Read first
- **Ollama is not installed on this PC, so layer 2 has only been tested against a fake Ollama server** I
  wrote that speaks the API as documented (`GET /api/tags`, `POST /api/generate` with `stream:false`).
  Please test with your real Ollama: Settings, নিরাপত্তা, "স্থানীয় AI (Ollama)", turn on, set a model you
  have pulled (default `llama3.2`), press "সংযোগ পরীক্ষা". I do not know how good `llama3.2` is at Bangla;
  try the model you like.
- **Translate page to Bangla is still not built.** It is not in this milestone's task list. It would use
  the same Ollama connection; say if you want it (probably as a reader mode option).
- **Security review of the new code (nothing found in earlier code):** the local AI client refuses any
  address that is not plain http on a loopback address (127.0.0.1, ::1, localhost, no user name), uses no
  system proxy and follows no redirects, so page text cannot leave this computer; the model is told the
  page text is untrusted and it is fenced (a page cannot fake the fence); the colour of the verdict comes
  only from the rule check, never from the model; the model's answer is shown as plain text with markdown,
  control and direction-changing characters removed and a length cap; the page script's result is parsed
  as untrusted with hard size caps.

### What was built
- **Layer 1, offline, always works** (`Obhijatri.AI/Scoring`): green, yellow or red plus the top three
  reasons in plain Bangla. Pure functions, no network or file access. It weighs:
  - the address, using the scam shield's local facts (lookalike, signed scam list, Safe Browsing prefixes:
    all red) and how the address looks (raw IP, punycode, abused endings such as .xyz, many hyphens or parts);
  - what the page asks for: password, PIN or OTP, card number, ID number, phone number, and where the form
    sends the answers (a different site) and whether the connection is HTTPS;
  - what it says (Bangla and English phrase lists in `ScamPhrases.json`): urgency, prize and lottery,
    SIM or KYC or NID threats, job and money offers, PIN and OTP requests, countdown timers, a brand's name
    (bKash, Nagad, banks, Facebook and so on) on a site that is not the brand's own.
  - Points: yellow from 25, red from 50, or red at once for a scam list, Safe Browsing or lookalike hit. A
    page that asks for nothing (no form, no chat link) can never go above 20 from words alone, so a news story
    that quotes scam phrases stays green; a form-less scam that says "message this WhatsApp number" is caught
    by a contact-link signal. The real site of a protected brand is green unless it is on the scam list.
  - Every verdict is worded as an estimate. There is no "safe" value at all, and the note under every
    result says green does not mean no risk.
- **Layer 2, optional** (`Obhijatri.AI/Ollama`, off by default): with Ollama switched on, a short Bangla
  explanation is added under the reasons ("আপনার কম্পিউটারের AI-এর ব্যাখ্যা (এটিও অনুমান)"). It gives up
  after 20 seconds, or at once if Ollama is not running or the model is missing, and then says so and the
  rule result stands. Settings has the switch, the model name and a connection test that lists installed
  models and tells you the exact `ollama pull` command if yours is missing.
- Toolbar button "এটা কি প্রতারণা?" (works on any web page; on one of our own warning pages only the address
  is judged, because the warning's own words would look like a scam). Page facts come from `scam-signals.js`
  (run on demand, nothing sent anywhere).
- 42 new strings. Debug-only `--scamcheck-selftest`. The local test server `tools/m7-checks/serve.ps1` now
  also serves five made-up pages for you to try the button on (page 7 of its index).

### Tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: **559 passed** (84 new).
  - Fixtures (`tests/Fixtures/scam-pages.json`, made-up pages, written before scoring them): 34 scam pages,
    all yellow or red (20 red, 14 yellow, none green); 32 clean pages, all green, including news stories
    that quote scam phrases, official bKash, Facebook, Google, EBL and Daraz pages, shops with sale timers,
    a shop that lists "pay with bKash" beside a card form, OTP login for a ride app, and university and
    job portals with forms. The 30 fake and 30 real addresses of the scam shield tests: all fake red, all
    real green. Scoring takes about a thousandth of a millisecond per page.
  - Layer 2 with a fake HTTP handler: only loopback addresses accepted (13 address cases), only ever
    contacts 127.0.0.1:11434, not running, wrong model, server errors, odd or huge answers, timeout and
    cancellation give null, markdown and hidden characters stripped, prompt cannot be fooled by a fake fence.
  - Hostile page data: wrong shapes, oversize text, odd host names, a page saying it is "100% safe".
- Live (`--scamcheck-selftest`): the real page script and scorer on six local pages (fake bKash login red,
  prize and WhatsApp-only pages yellow, scam article, shop and plain login green); five real sites, all
  green (Prothom Alo, The Daily Star, bKash, Dutch-Bangla Bank, Daraz); the fake Ollama: model list, quick
  answer (15 ms), server error, no server (about 2 s), slow server gives up at 20.0 s, switched off asks
  nothing. Result panel screenshots checked (banner colours, Bangla text, AI section).

### Known issues
- These are heuristics. A new kind of scam, or a scam in another language, can score green; a legitimate
  site with an unusual mix of signs can score yellow. Never treat green as safe (the text says so).
- A brand's own domain is trusted without reading the page (a hacked official page would show green unless it
  is on the scam list). Only the protected brand list (30 brands) is known; other companies' names are not.
- Words are matched as plain phrases, so heavy spelling variation can hide them; forms inside frames or
  built by scripts after the page loaded may be missed. Login and OTP pages of ordinary sites are green by design.
- Not seen by a person in the live app: the toolbar button, the real flyout, the Settings card.
- The toolbar now has many buttons (scam check, reader, memory, shield, and so on); tell me if it feels crowded.

### Addendum: translate page to Bangla (owner request, out of plan.md's Milestone 10 list)
The owner asked for Google Translate as an option, keeping the Ollama route for privacy. Both are **off until
switched on** (Settings, প্রধান সেটিংস for Google; নিরাপত্তা for Ollama). When either is on, the main menu gets
"পেজ বাংলায় অনুবাদ করুন"; if both are on you choose each time.
- **Google route** (`Obhijatri.Safety/Translate/GoogleTranslate`): the tab goes to Google's translation of the same
  page (`www-example-com.translate.goog/...`, plain `translate.google.com` for http pages). Google fetches the page
  itself, so Google learns the address and the text; pages behind a login do not translate. A one-time warning
  says this before the first use. **Never offered** in private windows, on banking and payment sites (the payment
  lock list), on addresses only reachable from this PC or network, on IP numbers or special ports, on an already
  translated page, or for very long addresses; each refusal says why. The scam shield judges a translated page by
  the real site behind Google's wrapper name (the wrapper of a Facebook page would otherwise look like a lookalike;
  a test proves this), so a scam site does not get through by being translated.
- **Local route** (Ollama, in reader mode): the article is translated block by block by your local model, so the
  text never leaves this PC. Paragraphs already in Bangla are skipped; there is a stop button, "show original" and
  "show translation", and reading aloud follows whichever is shown. Every answer is plain text. If Ollama is not
  answering it gives up after two failures (about 5 s) with a message instead of waiting for every paragraph.
  Translation quality depends on your model (untested with a real one, see above).
- 18 new strings; 45 new tests (604 in all): address building for 30 cases, refusals, host encoding round trips,
  the Bangla check, translation prompt and limits, unwrapping and settings defaults.
- Live (`--translate-selftest`): the real Google route on The Daily Star (English) gave a Bangla page (2,436 Bangla
  letters) on the wrapper address with no scam warning; the reader translation against the fake Ollama replaced
  the English blocks, kept the list bullet, left the Bangla paragraph alone, and toggled original and translation.
- Known: Google's translated page carries Google's own toolbar; going back returns to the original. The Ollama
  route only translates the article text that reader mode extracts, not the whole page.

### Next
Wait for approval of Milestone 10 (and the translation addition). Then Milestone 11 (installer and polish), the last one.

## Session 10 (2026-09-29): Milestone 9, part 9b: Bijoy converter, Bangla fonts, reader mode, read aloud

Status: **9b awaiting owner approval.** Milestone 9 is complete except for translate, see below.

### Read first
- **Owner feedback on 9a, done:** the Hijri card now says "হিজরি (সৌদি ক্যালেন্ডার)" with a note under the date
  cards that it is Saudi Arabia's calendar, that Bangladesh may differ, and to use the government calendar
  and holiday list for Eid and Ramadan. The settings card says the same. The prayer note now says to follow
  the Islamic Foundation timetable if it differs. **I could not get the Foundation's own table**
  (islamicfoundation.gov.bd was not reachable through my tools). My method is the one it states (Karachi,
  Hanafi Asr, see 9a) and matches an independent implementation within 2 minutes, but please compare
  Dhaka with the Foundation's table and tell me the difference per prayer; a fixed offset is easy to add.
- **Translate page to Bangla: not built, on purpose.** plan.md says use the engine's built-in translate if
  available, otherwise hide the button unless Ollama is enabled. WebView2 has no translate API (that is an
  Edge browser feature), and Ollama arrives in Milestone 10, so the button is hidden. Nothing to see yet.
- **No Windows Bangla voice on this PC** (only David, Zira and Mark, all English). Read aloud is built but
  **I could not hear or test it**; the reader shows the install note instead. Please install a Bangla
  voice (Windows Settings, Time and language, Speech, Add voices) and try it: play, pause, stop, and
  clicking a paragraph to start from there.
- **Third-party code, approved by you:** the Bijoy tables and algorithm come from `bijoy2unicode`
  (MPL 2.0, same license as the Avro files). See THIRD_PARTY_NOTICES.md. The original has a **bug in reph
  (র্) handling** (কর্ম came out as কমর্, about 3% of words); I fixed it in the port and tested it.

### What was built
- **Bijoy (SutonnyMJ) to Unicode** (`Obhijatri.Bangla/Bijoy`): tables generated from the original source,
  algorithm ported to C#. The page script `bijoy-detect.js` (part of the token-protected bridge) looks once
  after load and again 4 seconds later, bounded to 3000 text nodes, for text in a Bijoy family font
  (SutonnyMJ, SutonnyOMJ, Bijoy*, any name ending in MJ). If found, a toolbar chip "ইউনিকোডে বদলান" appears.
  One click: the page lists its Bijoy text, the app converts it (one tested C# engine), the page takes the
  result back as plain text (no HTML) and switches those elements to Nirmala UI. Reloading undoes it.
  Only text in Bijoy fonts is touched, so English and Unicode text on the same page are safe.
- **Fix Bangla fonts** (Settings, থিম; default off): Bangla letters on web pages are drawn in a Windows
  Bangla font (Nirmala UI, then Vrinda) while English text, icon fonts and sizes stay as the site made
  them. `bangla-fonts.js` adds a font that covers only Bangla code points, and puts it first in the font
  stack of each element that has Bangla text (also text added later). Applies to pages loaded after the
  switch. New shared helper `Browser/LiveScript` now also drives low data mode's script.
- **Reader mode** (toolbar button and Views/ReaderView): `reader-extract.js` finds the article (the block
  with most paragraph text, dropping navigation, footers, comments, related boxes, ads by name) and
  returns plain text blocks. `Core/Reader/ReaderArticle.Parse` treats that as untrusted: unknown kinds
  dropped, control and bidirectional characters removed, sizes capped. The view is native text (no images,
  no page HTML or styles), large type (size buttons, remembered), closes on tab change or navigation.
- **Read aloud**: Windows speech (`SpeechSynthesizer`) with a Bangla voice (Bangladesh preferred over
  India), play, pause, stop, click a paragraph to start there, the paragraph being read is highlighted and
  kept in view. Text is cut into pieces at sentence ends (`SpeechChunker`). If there is no Bangla voice,
  the view explains how to install one and opens Windows speech settings on request.
- 33 new strings. Debug-only `--reader-selftest` (`--quick` skips the real sites, `--sites-only` skips the
  local pages); `--newtab-selftest`. The older self-tests now start on a blank web tab because the new tab
  page is the default first tab.

### Tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: **475 passed** (53 new).
  - Bijoy: 12 known words and sentences, the package's own sample, 11 reph words checked by hand
    (কর্ম, মার্চ, কার্তিক, পাসপোর্ট...), র্যাব, and a **differential test against the original Python
    package** on 934 Bangla words and sentences (everything without a reph gives identical output), a fuzz
    test of 20,000 random strings (no crash, deterministic) and speed (100 KB of Bijoy in well under a second;
    it was 2.4 seconds until I made the reordering work word by word).
  - Reader: parsing good and hostile input (wrong shapes, unknown kinds, control and bidi characters, huge
    sizes, deep nesting, HTML in text), speech chunking (sentence ends, very long sentences, no spaces).
- Live (`--reader-selftest`): a page with sidebar, related box, comments, footer and navigation: only the
  story is kept (7 checks pass); the font fix adds the Bangla face only when on; a page with SutonnyMJ text
  is detected, converted (2 pieces), ordinary text untouched, chip hidden afterwards; reader view opened
  and closed, screenshot checked. Real news pages: Kaler Kantho (10 to 19 paragraphs), bdnews24 (7), The
  Daily Star (11), Prothom Alo (3 to 4). Jugantor's article was never found by my test's link picker (a
  test limitation, not checked by hand).

### Known issues
- Read aloud is untested with a real Bangla voice (see above).
- Bijoy detection is by font name only. A page that shows Bijoy text in a font family not named like
  SutonnyMJ or ending in MJ is not detected. Detection runs on every page (bounded, a few milliseconds)
  and is not switchable.
- Some rarely used Bijoy conjuncts may convert differently from how Bijoy shows them; the table is the
  original's. র‍্য (from "i¨") is written with a ZWJ. The output uses single-character য়, ড় and ঢ় like
  the original (looks identical, differs from strict normalisation).
- Reader mode is text only (no images), needs about two paragraphs of text, and has no keyboard shortcut.
  Extraction is heuristic: pages with unusual markup may include a stray line or miss some.
- Fix Bangla fonts sets an inline font on each element with Bangla text, so it can conflict with pages that
  restyle themselves through scripts; it is off by default and applies on reload.
- Not seen by a person in the live app: the Bijoy chip, the reader toolbar button, the font setting.

### Next
Wait for approval of 9b. Then Milestone 10 (the "এটা কি প্রতারণা?" button).


## Session 9 (2026-09-29): Milestone 9, part 9a: new tab page, calendars, prayer times

Milestone 9 is split (plan.md section 8). **9a (this session)**: new tab page, Bangla and Hijri dates,
prayer times, speed dial. **9b (not started)**: Bijoy to Unicode converter, "Fix Bangla fonts",
translate (hidden unless Ollama), reader mode and read-aloud. Status: **9a approved** (2026-09-29).

### Read first
- **Deviation from plan.md: the new tab page is a native view, not local HTML.** Like History and
  Settings it is built in WinUI, so a new tab needs no WebView2 and no engine process (the idle RAM
  budget), and all strings and Bangla digits come from the same code paths. Say so if you want web content.
- New tabs, the + button, Ctrl+T and an empty startup now open this page. The Home button and the
  "home page" setting still go to Google. Typing an address or clicking a tile replaces the new tab.
- **Please check the Hijri date.** It comes from the Saudi Umm al-Qura calendar, which today gives 18
  Rabi al-Thani 1448. Bangladesh usually follows moon sighting a day later. A setting (Settings, General)
  shifts it by up to two days either way; the default is 0 (same as Saudi). Tell me which default you want.
- The Bangla calendar uses today's rules for every date, including years before the 2019 revision,
  when Ashwin had 30 days. Only matters for old dates.

### What was built
- `Obhijatri.Bangla/Calendars/BanglaCalendar`: revised Bangladesh calendar (year starts 14 April;
  Boishakh to Ashwin 31 days, Kartik to Magh 30, Falgun 29 or 30 in a leap year, Choitro 30; rules
  checked against Wikipedia's Bengali calendar page). Also the six seasons. `HijriCalendar`: Umm al-Qura
  from .NET with a day adjustment.
- `Obhijatri.Bangla/Prayer`: `PrayerCalculator` (solar position, no network) and the 64 districts with
  coordinates (`Districts.json`, embedded). Bangladesh method: Fajr and Isha 18 degrees, Asr Hanafi,
  sunrise and Maghrib at the horizon with refraction, UTC+6, rounded to the minute.
- `Views/NewTabView`: three date cards (Bangla with season, English with weekday, Hijri), prayer card with a
  district picker (remembered) and the next prayer highlighted (after Isha it is tomorrow's Fajr,
  refreshed every minute), and 8 speed dial tiles: your most visited sites of the last 60 days
  (`HistoryStore.TopSites`, grouped by site, front page address), filled up with well known Bangla sites.
  Private windows show only the defaults (no history there). Tiles are monograms (no favicons offline).
- `Core` now references `Obhijatri.Bangla` (for setting validation). New settings: `PrayerDistrict`,
  `HijriAdjustment`. 62 new strings (both languages). `Formatting.Time(TimeOnly)` added.
- Debug-only `--newtab-selftest` renders the window to `logsenchmark
ewtab.png`.

### Tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: **422 passed** (41 new):
  - Bangla dates: 17 known dates (Pohela Boishakh, last day of the year, Pohela Falgun, 8 Falgun =
    21 February, 12 Choitro = 26 March, 1 Poush = 16 December, month starts, 31 day Ashwin) and 5 leap year
    cases (Falgun 29 or 30 days, 29 February 2028); every day from 2020 to 2035 follows the previous one;
    year lengths 365 or 366 for 13 years.
  - Hijri: 1 Ramadan 1447 = 18 Feb 2026 and 1 Shawwal 1447 = 20 Mar 2026 (Umm al-Qura), adjustment shifts.
  - Prayer times against an independent implementation of the same method (aladhan.com API, fetched
    through a summarising tool, so treat as a cross-check, not an official source): Dhaka 29 Sep 2026,
    Chattogram 14 Apr 2026, Rangpur 21 Dec 2026, all six times within 2 minutes. Also: correct order for
    every district every week of 2026, east before west.
  - Top sites, settings defaults and damaged values.
- Screenshot of the page checked: dates, six prayer cells with the next one highlighted, tiles.
  Bangla today: 14 Ashwin 1433, autumn; Dhaka Fajr 4:34, sunrise 5:49, Dhuhr 11:49, Asr 4:08 pm,
  Maghrib 5:48 pm, Isha 7:03 pm.

### Known issues
- Not seen by a person in the live app: clicking a tile, the district picker, the settings card, dark
  theme (the screenshot is forced to light because an off-screen render has no backdrop).
- Coordinates are district headquarters from memory, rounded; a mosque timetable can differ by 1 or 2
  minutes, and a few districts' coordinates deserve a check against a map.
- The Islamic Foundation's official timetable is not the same source as my cross-check; compare Dhaka against
  it and tell me the difference (a fixed minute offset per prayer is easy to add).
- No speed dial editing (add, remove, pin); tiles follow history.
- The page uses Bangladesh time (UTC+6) for dates and prayer times whatever the PC's time zone.
- Everything from earlier milestones is unchanged.

### Next
Wait for approval of 9a, then 9b.


## Session 8 (2026-09-29): Milestone 8, tab sleeping, memory meter, low data mode, budgets

Status: **approved** (2026-09-29). Committed together with Milestone 7 as da1b1a2.

### Read first
- No security problems found in earlier code. The Milestone 7 hands-on checks (double-extension block,
  permission prompts, notification chip, clipboard guard, payment lock badge) were **not done** this
  session: they need a person driving the UI, and I did not attempt screen automation.
- **Tab sleeping saves a lot, but only after a workaround.** WebView2's `TrySuspendAsync` refused most
  tabs (even example.com) although their control was collapsed: the engine still counted them as
  visible. Showing the control at zero size for 300 ms and hiding it again fixes it (nothing shows on
  screen). Without that, 17 of 18 tabs refused and total memory fell only about 3%.
- **Low data mode's "lazy-load images" does not work for images already in the page's HTML.** The parser
  starts fetching them before any page script can set `loading="lazy"` (measured: 40 of 40 fetched).
  It only works for images and frames added after load. Measured effect on the five BD news sites:
  about 0% on four, so do not count on it. See the numbers below.

### What was built
- **Tab sleeping** (`BrowserTab.Performance.cs`, `MainWindow.Performance.cs`, `Core/Performance/TabSleepPolicy`):
  a 30-second timer puts a background tab to sleep after N idle minutes (setting: off, 5, 10, 15, 30, 60;
  default 10, in উন্নত settings). Skips the active tab, pinned tabs, tabs playing audio, loading tabs.
  Order: `TrySuspendAsync`, then the zero-size visibility bounce and one retry, then (if the engine still
  refuses) `MemoryUsageTargetLevel = Low` so the page keeps running but trims memory. A sleeping tab
  wakes on click, reload, back or forward, before it is shown. Sleeping tabs are dimmed and the hover
  text says so. Idle time counts from the moment the tab was left.
- **Pinned tabs** (the plan mentions them but no pinning existed): tab right-click menu, "ট্যাব পিন
  করুন". A pinned tab moves to the start of the strip, loses its close button (Ctrl+W and middle click
  still close it), never sleeps, and is remembered across restarts (database schema version 5,
  `session_tabs.is_pinned`). Not built: shrunken pinned tab width.
- **Memory meter**: toolbar figure (total private memory of the app plus all engine processes, hidden by
  a setting), click for a breakdown (app, web pages and engine, sleeping tabs, memory saved today) and a
  "sleep background tabs now" button. Per-tab memory is in each tab's hover text. It refreshes every 5
  seconds only while the window is active. Uses private working set (what Task Manager calls Memory),
  read via `K32GetProcessMemoryInfo`; a renderer process is charged to the tab whose frames it runs.
- **Memory saved today** (`Core/Performance/MemorySavedCounter`): for tabs put to sleep in one pass,
  memory before minus memory 30 seconds later (the engine releases memory gradually: about half after 8
  seconds, nearly all after 40). A tab that cannot be found in the second measurement is never credited.
  Resets on a new day, survives restarts.
- **Low data mode** (settings, উন্নত, and the main menu): (1) video and audio only start after a click
  or key press in the page (`play()` is refused otherwise, autoplay attributes removed, autoplay that
  slips through is paused); (2) `loading="lazy"` on images and frames added after load; (3) heavy
  embeds inside another site's page (YouTube, Vimeo, Facebook plugins, X, Instagram, TikTok, Twitch,
  SoundCloud, Spotify) are replaced by a short Bangla notice, decided by a host and path table in
  memory (`Safety/LowData/HeavyEmbeds`, no I/O in the request path). Applies to pages loaded after the
  switch. The setting is cached in a field, not read from the database per request.
- 26 new strings (both languages); Bangla uses স্লিপ, মেমরি, পিন, লো-ডেটা মোড.
- Debug-only self-tests: `--perf-selftest` (idle memory, 20 real tabs, sleeping, pinning, wake) and
  `--lowdata-selftest` (`--quick` for the local page only). Output in `%LOCALAPPDATA%\Obhijatri\logs`
  (`perf-selftest.txt`, `lowdata-selftest.txt`, screenshots in `logs\lowdata`).

### Performance budgets (plan.md section 6), this machine, SSD, Windows 10
| Budget | Result |
| --- | --- |
| Installer under 15 MB | Not measurable yet (Milestone 11). |
| Cold start to usable window under 1 s | Release build, fresh profile, 3 runs: 1229 ms (first run), 785 ms, 718 ms. "Usable" here means the window exists with its title; the first run is slightly over budget. |
| Idle RAM, shell plus one blank tab under 150 MB (shell) | **Met**: 72.5 to 81 MB private working set (plain working set is 189 MB because it counts shared system libraries; the plan's wording is ambiguous, so both are reported). Engine processes for that one tab add about 80 to 115 MB. |
| 20 tabs on a 4 GB PC usable, sleeping on | **Not testable here** (this PC has far more RAM). 20 real news and reference sites: 1957 MB awake. After sleeping 18 of them (active and one pinned tab awake): 1056 MB after 8 s, 633 MB after 40 s (65% less), and a sleeping tab woke and answered in about 25 ms. 633 MB should fit on a 4 GB PC; the awake state at 1.9 GB would be tight. |
| Safety check per navigation under 20 ms | Unchanged from Milestone 6. Low data adds one field read and one table lookup per request. |

Idle timer path was verified by making two tabs look 11 minutes idle: exactly those two slept.

### Low data measurements (local page, then five BD news sites, cache cleared, medians of 2 rounds)
- Local page: script `play()` allowed off, refused on; autoplay video plays off, blocked on; YouTube
  embed 1,013,000 bytes off, 372 bytes on (notice shown, screenshot checked); 40 of 40 images marked
  lazy but all 40 still fetched.
- News sites (bytes received, all frames, from the engine's network events): Prothom Alo 1.43 to 1.42
  MB, Kaler Kantho 1.91 to 1.91, Jugantor 1.59 to 1.58, The Daily Star 2.56 to 2.56, **bdnews24 1.84 to
  0.72 MB (-61%, load 1241 to 377 ms)**. Total -12%. Screenshots of on and off checked: pages not broken.

### Tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: **381 passed** (41 new: sleep
  policy, settings defaults and damaged values, memory counter day rollover, pinned session round trip,
  heavy embed matches and lookalike hosts). `check_strings.py` passes; JS phonetic test 95/95.

### Known issues
- The hands-on UI checks (right-click Pin menu look, toolbar meter and flyout look, dimmed tabs, Bangla
  wording) were not seen by a person; only the self-tests and screenshots of pages. Please look.
- Low data lazy loading, see above. A real fix would need rewriting page HTML, which is out of scope.
- A tab whose window is minimised or in the background keeps its active tab awake (only inactive tabs
  sleep).
- Trimmed-only sleep (engine refuses to suspend after the retry) keeps the page running and saves less.
  Never seen in the final runs.
- Sleeping a tab briefly shows it at zero size; if you switch to it during those 300 ms it flashes small.
- Media requests, images and fonts are not blocked by low data mode; only embeds and autoplay.
- Everything from earlier milestones (scam list signing key, payment lock has no per-site override, etc.)
  is unchanged.

### Next
Wait for owner approval, then Milestone 9 (new tab page and Bangla extras).

## Session 7 addendum: default theme (owner request, out of plan.md)

Not a plan.md milestone item: the owner asked for a default red/green theme evoking the current flag,
with gold used sparingly for the gold map on the original 1971 flag's red sun. Shown as a mockup first
(accent-only option), then approved and applied as the actual default.

- `App.xaml`: `SystemAccentColor` and its six tonal variants overridden to a red (`#F42A41`), which
  flows through every WinUI control that reads them via `ThemeResource` (buttons, toggle switches,
  hyperlinks, focus visuals) in both light and dark mode automatically, the same mechanism already
  used for `SettingsCardStyle` and friends, so this follows the "theme-aware styles, not brushes read
  once" rule.
- The tab strip and title bar area (`TitleArea`) get a fixed dark green background
  (`ObhijatriTabStripBackgroundBrush`, `#0B3D2C`). Deliberately not a `ThemeResource`: like a
  browser's own brand colour, it does not change with light/dark mode, so a plain resource is correct
  here, not a bug.
- The payment lock "নিরাপদ মোড" badge (Milestone 7) gets a small gold dot (`ObhijatriGoldAccentBrush`,
  `#D4AF37`) next to the existing green success text, the one deliberate nod to the gold map. Gold is
  not used anywhere else.
- Verified with a new Debug-only `--theme-selftest` (renders the window's root element to a PNG,
  since the existing self-tests only capture the WebView2 page content, not the native chrome): the
  green tab strip and the red private-window badge both render correctly. The toolbar row appears
  washed out in the captured PNG; this is `RenderTargetBitmap` not compositing the Mica backdrop
  properly during an off-screen render, not a real rendering problem (Mica is a live compositor
  effect). Please glance at the toolbar in the real running app to confirm the accent colour looks
  right on buttons and toggles.
- Not touched: the vertical tabs side panel (keeps default colours), the individual TabViewItem
  selected/hover chrome (kept as WinUI's own default rather than deep-templating it), and the app
  icon (still the Milestone 1 placeholder).

## Session 7 (2026-09-26): Milestone 7, downloads, leak check, permissions, clipboard guard, payment lock

Status: **approved** (2026-09-29). The "Known issues" items below (double-extension block,
camera/mic/location prompts, notification chip, clipboard guard, payment lock badge) were not
hands-on tested before approval; worth checking when convenient. Milestone 8 will run in a fresh
session, possibly on a different model.

### Security notes (read first)
- **Download scanning is real, not a stub**: every finished download goes through Windows'
  Attachment Execution Services (`IAttachmentExecute`, the same mechanism Chrome and Edge use), which
  applies Mark of the Web and calls every antivirus product registered for downloads (Microsoft
  Defender among them) before the file can be opened from the panel. This needed a COM interop detail
  found the hard way this session: the component only answers on a single-threaded apartment (STA); on
  a plain thread-pool thread the interface query fails with E_NOINTERFACE even though the IID and
  CLSID are correct. `AttachmentScanner.Scan` now runs its COM call on a dedicated STA thread
  internally, confirmed against the real Windows component (Zone.Identifier written correctly,
  including the "about:internet" fallback when no source address is known).
- A name with a hidden second extension (`invoice.pdf.exe`, `photo.jpg.scr`) is blocked regardless of
  what the antivirus scan says.
- **HIBP password leak check**: only the SHA-1 hash's first 5 hex characters ever leave the PC
  (k-anonymity). The password itself is hashed inside the page by the browser engine's own Web
  Crypto, and only the hash crosses the page bridge to the app; confirmed by a unit test that reads
  the exact request URL a fake HTTP handler receives.
- **Permission dashboard**: camera, microphone and location requests get a Bangla allow/deny prompt
  (the choice is remembered per site); notification requests are **denied by default** with a quiet
  toolbar chip to allow them later, per plan.md. Private windows keep their own choices in memory only.
- **Clipboard guard**: an injected script wraps `navigator.clipboard.write(Text)` and
  `document.execCommand("copy")`; a write that was not preceded by a real keypress, click, or the
  page's own copy/cut event within 1.5 seconds is reported to the app, which shows a Bangla warning.
- **Payment lock mode**: on bKash, Nagad, Rocket, nine BD banks and PayPal, every third-party script
  is blocked outright (not just ones on the ad/tracker lists), independent of whether ad blocking
  itself is on, and a green "নিরাপদ মোড" badge shows in the toolbar.
- **Cookie auto-delete** (opt-in, off by default): on the app's last window closing, cookies for
  sites that are not bookmarked are removed from the normal profile. Runs once, right before exit.

### What was built
- `Obhijatri.Safety/Downloads`: `AttachmentScanner` (the COM wrapper above) and `DangerousExtensions`
  (double-extension and plain-executable checks).
- `Obhijatri.Safety/Privacy/HibpClient.cs`: SHA-1 hashing, the k-anonymity range request, and local
  comparison of the returned suffixes.
- `Obhijatri.Safety/PaymentLock/PaymentSites.cs`: the protected bank/MFS/payment domain list.
- `Obhijatri.Core/Storage/SitePermissionsStore.cs` (schema version 4): per-site camera/microphone/
  location/notifications, ask/allow/deny, a row exists only while something is away from "ask".
- `DownloadItem`: a completed download is scanned before `IsCompleted` becomes true; a blocked one
  shows "নিরাপত্তার কারণে ব্লক করা হয়েছে" with only a delete action, no open/show-in-folder.
- `Web/password-leak.js` and `Web/clipboard-guard.js`, added to the page bridge alongside the
  existing phonetic and shortcut scripts; new page-to-app messages `PasswordHash` and `ClipboardWrite`.
- `BrowserTab`: handles `CoreWebView2.PermissionRequested` (camera/microphone/geolocation/
  notifications), tracks `IsPaymentLockActive` per navigation, and enforces the third-party-script
  block for payment lock mode inside the existing request-filtering path.
- Settings, নিরাপত্তা: leaked-password check, clipboard guard and payment lock toggles.
  প্রাইভেসি: a site permissions list (per-site, per-kind, with a one-click "remove (will ask again)"),
  and the cookie auto-delete toggle.
- Debug-only `--downloads-selftest` (serves two files from localhost, no internet needed) and
  `--scamshield-selftest` continues to pass from Milestone 6.

### What was tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: **340 passed**, including:
  - `HibpClientTests`: the request URL's last path segment is always exactly 5 characters and matches
    only the hash's first 5 characters; a clean password returns 0; a server error returns null
    (not a false "not leaked"); malformed input never sends a request at all.
  - `DangerousExtensionsTests`: `invoice.pdf.exe`, `photo.jpg.scr`, `resume.docx.bat`,
    `archive.zip.vbs` all flagged; `installer.exe`, `archive.tar.gz`, ordinary multi-dot names not.
  - `SitePermissionsStoreTests`, `PaymentSitesTests`, plus `BookmarkStore.GetAllUrls` (used by cookie
    auto-delete) and new `BrowserSettings` default-value tests for every new toggle.
- **AttachmentScanner, tested directly against the built DLL** (not mocked): a clean file scans and
  gets a correct Zone.Identifier with the real source URL; a file with no source URL gets
  "about:internet"; a referrer URL is accepted; called from an ordinary thread-pool thread (the real
  calling context) it works without the caller needing to know about the STA requirement.
  Windows Defender real-time protection is on and has real detections in its history on this machine,
  but an EICAR test file was not flagged even dropped directly with no app involved at all (confirmed
  with a baseline PowerShell test), this sandbox appears to exclude the EICAR string specifically, so
  "EICAR is caught and blocked" could not be demonstrated end-to-end this session; the mechanism that
  would carry a real detection through (Save() failing, or the file disappearing) is implemented and
  unit-covered for both HRESULT paths.
- **`--downloads-selftest`**: an ordinary text file downloads, completes, and gets a correct
  Zone.Identifier (confirms the full pipeline: WebView2 download to `AttachmentScanner.Scan` to
  `IsCompleted`). The double-extension file never reached a decision either way in this
  environment: WebView2's own `CoreWebView2DownloadOperation` reports 100% received but its `State`
  never leaves `InProgress`, even after 60 seconds, for the `.exe`-suffixed file specifically (the
  plain `.txt` file completed normally in every run). This looks like WebView2's own SmartScreen
  reputation check for executables hanging with no path to Microsoft's reputation service in this
  environment, not a bug in `DangerousExtensions` (which is separately unit-tested and correct) or in
  `DownloadItem`'s scan sequencing. Net effect either way: the file cannot be opened from the panel.
  Please retry `--downloads-selftest` on a normal internet connection and check the result.

### Known issues
- The double-extension block was not confirmed end-to-end this session (see above); please check
  `invoice.pdf.exe`-style downloads by hand.
- HIBP has not been tried against the real api.pwnedpasswords.com service this session (only a fake
  HTTP handler); the request shape follows their documented API.
- Camera/microphone/location permission prompts, the notification chip, the clipboard guard warning,
  and the payment lock badge were built and reviewed but not driven through the live UI this session
  (screen automation was not attempted, following the lesson from Milestone 5's shield flyout). Please
  check: visiting a site that asks for the microphone shows a Bangla prompt; a site that asks for
  notifications is silently denied with a bell icon appearing in the toolbar; pasting right after
  visiting a site that writes to the clipboard on load shows a warning; opening bkash.com or
  paypal.com shows the green "নিরাপদ মোড" badge.
- Payment lock mode blocks **every** third-party script on the protected sites, not just known
  trackers; a bank page that legitimately depends on a third-party script (a payment gateway widget,
  a CAPTCHA) could break. There is no per-site override for this yet, unlike ad blocking's "show ads
  on this site" switch.
- Cookie auto-delete only considers exact bookmarked pages' sites; a site the user visits often but
  never bookmarked loses its cookies on every close. This is the simplest reading of "not bookmarked"
  from plan.md and may be worth revisiting against real usage.
- The scam list signing private key generated in Milestone 6 is still not saved anywhere retrievable;
  unchanged from last session.

### Next
Milestone 8: tab sleeping, RAM meter, low-data mode, and the performance budgets in plan.md section 6.

## Session 6 (2026-09-26): Milestone 6, scam shield and lookalike domain alarm

Status: **approved** (2026-09-26), owner asked to proceed to Milestone 7 without a separate hands-on
pass; the items under "Not yet done" above remain worth checking when convenient.

### Security notes (read first)
- The BD scam list is downloaded and verified with an ECDSA (P-256) signature, not just a hash: a
  compromised host cannot add or remove entries without the private key. The bundled
  `Assets/ScamShield/bd-scam-domains.json` is signed with a throwaway key generated this session; the
  private key is **not** in the repository (it lived only in the scratch temp folder and was not
  copied in). This means a future session cannot re-sign an updated bundled list without generating a
  new keypair and re-embedding the new public key in `ScamShieldService.cs` (and re-signing the
  bundled JSON). Real key custody is an open owner decision (plan.md section 9: "who maintains the BD
  scam list").
- The bundled scam list itself is a placeholder: 3 made-up example domains (`example-bkash-reward.info`
  and similar) to prove the mechanism end to end. No real scam site was visited or embedded, per plan
  rule 4. A real list still needs a host and an owner.
- Google Safe Browsing: only 4-byte hash prefixes are ever downloaded or stored; the address the user
  visits is never sent anywhere by this feature. No API key is configured by default (feature fully
  skipped, zero network calls, confirmed in `SafeBrowsingClientTests.WithoutAnApiKey_NeverContactsTheServer`).
  An owner who wants it can drop a key into `%LOCALAPPDATA%\Obhijatri\safebrowsing.key` (one line, not
  part of the database, matching plan.md section 2's "local user secrets file, never committed").
  **Not exercised against the real Google API this session** (no key was available); see Known issues.
- "Continue anyway" past a scam warning is remembered for the current run only (a separate list from
  the HTTPS "continue anyway" list), never saved to disk, and private windows keep their own list, the
  same pattern as Milestone 5's HTTPS warning.
- The warning page reuses Milestone 5's `Interstitial`/`interstitial.html`: no script, every value
  HTML-encoded, a random nonce per warning so another page cannot forge "continue".

### What was built
- `Obhijatri.Safety/ScamShield`: `ProtectedBrands` (30 BD and global brands: bKash, Nagad, Rocket,
  Dutch-Bangla and 7 other BD banks, Bangladesh Bank, NID/e-Passport/BRTA/gov.bd, Grameenphone/Robi/
  Banglalink, Daraz, Facebook/WhatsApp/Instagram, Google, Microsoft, Apple, Amazon, PayPal, Netflix),
  `Homoglyphs` (Cyrillic/Greek look-alike and digit-for-letter skeletonising), `LookalikeDetector`
  (punycode/homoglyph skeleton match, brand name used as a label elsewhere in the host, and a
  one-letter-typo check restricted to 5+ letter brand names to avoid short-word false positives).
- `ScamListStore`: signed BD scam list, bundled copy plus a daily download from an owner-set URL
  (blank by default, so nothing is fetched until a host is chosen), same "bad download never replaces
  a good list" pattern as Milestone 5's filter lists.
- `Obhijatri.Safety/ScamShield/SafeBrowsing`: `UrlCanonicalizer` (the v4 API's URL expression rules:
  host suffixes down to 2 labels, path prefixes, repeated percent-decoding) and `SafeBrowsingClient`
  (Update API v1 scope: full updates only, hash prefixes cached to disk, no partial-update diffing or
  checksum verification yet).
- `Obhijatri.App/Services/ScamShieldService`: owns the list store and Safe Browsing client, a cached
  `Enabled` flag (mirrors `FilterService`'s pattern), and the combined `Check(Uri)` used per navigation
  (lookalike, then scam list, then cached Safe Browsing prefixes; first hit wins).
- `BrowserTab`: the check runs in `Core_NavigationStarting` right after the HTTPS-upgrade step, before
  a per-site "continue anyway" choice is honoured; on a hit the navigation is cancelled and the scam
  warning page is shown, address bar keeps the address the user typed (same mechanism as the HTTPS
  warning). `ITabHost` gained `IsScamAllowed`/`AllowScamSite`; `Interstitial` gained a `Kind` so
  "continue anyway" records the right kind of session-only exception.
- Settings, নিরাপত্তা: a toggle (on by default), the scam list's entry count and last-updated date
  (or "the list that came with the app"), and an "এখনই আপডেট করুন" button.
- New strings: the warning page's three reason variants (lookalike, known scam, Safe Browsing), 30
  Bangla brand names, and the settings card. `check_strings.py` passes (no hardcoded text, no dashes).
- Debug-only `--scamshield-selftest` (mirrors Milestone 5's `--https-selftest`): navigates a private
  window through safe/flagged/real-subdomain cases, the "continue anyway" flow (wrong code ignored,
  right code proceeds, no second warning this session), and confirms the setting's off switch is
  honoured; screenshots saved to `logs\benchmark`.

### What was tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: **311 passed**, including:
  - `LookalikeDetectorTests`: all 30 fake fixtures in `tests/Fixtures/scam-fixtures.json` flagged, zero
    false positives on the 30 real ones (protected brands' own domains, their real subdomains such as
    `pay.bkash.com`/`accounts.google.com`, and unrelated real sites), plus a specific check that a short
    unrelated word (`brac.net`, the NGO, vs. BRAC Bank's domain) is not flagged.
  - Timing: **0.0044 ms average** per check across all 60 fixtures (measured directly, see below), well
    under the 20 ms budget.
  - `ScamListStoreTests`: bundled list used until a signed download arrives; a tampered download and a
    download signed with the wrong key are both rejected and the previous list is kept; a blank URL
    never sends a request.
  - `SafeBrowsingClientTests`: no key means no HTTP request at all; hash prefixes round-trip through the
    cache file and correctly flag/not-flag URLs; a server error leaves the cache unchanged;
    `UrlCanonicalizer` expression generation checked against the v4 spec's host-suffix and path-prefix
    rules.
- **Self-test** (`--scamshield-selftest`, private window): all 8 checks passed, including a real,
  harmless site (example.com, no warning), a made-up lookalike (bkash-verify.xyz, warned), a
  digit-substitution lookalike (faceb00k.com, warned), and a real bKash subdomain
  (pay.bkash.com, redirected to www.bkash.com by the real site, no warning: this ran with real
  internet access, not a mock). Screenshots in `logs\benchmark\scam-warning.png` and
  `scam-continued.png` show the Bangla warning page and address bar rendering correctly in dark mode.
- **Not yet done**: the plan's owner test (typing `bkash-verify.xyz` and a punycode lookalike of
  facebook.com by hand in the running app, and checking the Settings page toggle visually) was not
  performed this session; the self-test above exercises the same code path but the owner should still
  do the hands-on check.

### Known issues
- Safe Browsing has not been exercised against the real Google API (no API key available this
  session): the request/response shapes follow the v4 docs and are covered by unit tests against a
  fake HTTP handler, but a real key should be tried before relying on this layer.
- Safe Browsing Update API v1 scope only: full updates, no partial-update (diff) support and no
  checksum verification of the fetched list. A prefix hit is trusted directly (no `fullHashes:find`
  confirmation step), so a 4-byte hash collision could in theory warn on a clean address; this only
  matters once a key is configured.
- The bundled scam list's signing private key is not saved anywhere in the repo or project; a future
  session needs to generate a new keypair (and update the embedded public key) to publish a real,
  updated bundled list. This was a deliberate choice (never commit a private key) but means the
  bundled list cannot be refreshed without that step.
- Settings page's new Scam Shield card was built following the same pattern as the existing Filter
  Lists card but was not checked visually in this session (see "What was tested").
- The lookalike detector's typo check only catches a single-character edit for brand names of 5+
  letters (to avoid false positives between short unrelated words, found and fixed this session:
  `brac.net` was initially flagged as a lookalike of BRTA). Two-character typos and typos of 4-letter
  brand names (Robi, NID) are not caught by that path, only by the exact "brand name used as a label
  elsewhere" check.

### Bugs found and fixed this session
- The typo-detection edit-distance check initially flagged `brac.net` (an unrelated NGO domain) as a
  lookalike of `brta.gov.bd`, because both are short (4-letter) words within edit distance 2 of each
  other. Fixed by requiring 5+ letter brand names and an edit distance of exactly 1 for that path.

### Next
Milestone 7: downloads scanning and Mark of the Web, HIBP password leak check, permission dashboard,
clipboard guard, payment lock mode, cookie auto-delete.

## Session 5 (2026-09-26): Milestone 5, ad and tracker blocking, HTTPS-only

Status: **approved** (2026-09-26). The next session (Milestone 6) will run on Claude Sonnet.

### Security notes
- Warning pages have no script, every value in them is HTML-encoded (the address comes from the web), and their two links carry a random code per warning. The self-test confirmed that a link with a wrong code (as another page could create) does nothing.
- "Continue anyway" for a site without HTTPS lasts only for this run and is never saved; private windows keep their own list.
- Filter lists are data from easylist.to. A download is used only if it has the right header and title, is not much shorter than the list in use, and still matches its saved SHA-256 when read back; otherwise the bundled copy is used. Only the list files are requested; nothing about browsing is sent.
- The per-site "show ads" choice is stored only when on (same table as Bangla typing, schema version 3); private windows keep it in memory.

### License
- EasyList and EasyPrivacy: GPL 3.0 or CC BY-SA 3.0; used under CC BY-SA 3.0 with attribution to "The EasyList authors". Only the list files are covered. Bundled snapshots are 1.2 MB compressed. Recorded in `THIRD_PARTY_NOTICES.md`.

### What was built
- `Obhijatri.Safety/Filtering`: Adblock Plus subset parser (`||domain^`, anchors, `*`, `^`, `@@`, third-party, domain=, types, important, match-case, `$document` exceptions). Rules with unsupported options (popup, redirect, csp, ...) are skipped rather than half-applied. Element hiding is not in v1.
- `FilterEngine`: plain domain rules as 64-bit hashes (93,894 of 112,256 rules), the rest indexed by a whole-word token; only 55 rules need a linear check. Build 0.3 s on a background thread, about 8 MB.
- `FilterListStore`: bundled gzip snapshots, weekly background update, integrity checks (see above). Tested against the real easylist.to download: both lists accepted and the newer version used.
- `RegistrableDomain` (the site of a host, handles .com.bd, .gov.bd, .co.uk) and `HttpsUpgrade` (which http addresses are upgraded; local, private and single-word hosts are not).
- Tabs filter requests from every frame and worker (`WebResourceRequested` with all source kinds) and never block the page itself.
- HTTPS-only: http:// opens as https://. If the site redirects back to http, or HTTPS fails (certificate, connection, invalid response), a Bangla warning page offers "নিরাপদ জায়গায় ফিরে যান" (primary) and "ঝুঁকি বুঝেছি, তবুও চালিয়ে যান" (small). The address bar keeps showing the address the user wanted.
- Shield button in the toolbar: blocked count for the page (Bangla digits), whether the connection is secure, and a per-site "সাইটে বিজ্ঞাপন দেখান" switch (reloads the page).
- Settings: নিরাপত্তা has HTTPS-only; প্রাইভেসি has ad blocking and the block list status (rule count, last update) with "এখনই আপডেট করুন". Tracking protection now defaults to স্ট্রিক্ট as the plan asks.
- Bangladesh list (`Assets/Filters/bd-extra.txt`): one verified rule so far, MoMagic's TrueReach ad renderer on jugantor.com. Everything else seen on the five sites was already covered by EasyList and EasyPrivacy.

### What was tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: **286 passed** (rule matching, engine, list store with a fake network, HTTPS upgrade rules, site preferences, Bangladesh list).
- **Page load benchmark** (`--benchmark`, private window, cache cleared before every load, 3 rounds, order alternated), median load time in ms:

  | Site | Blocking off | Blocking on | Change | Requests off to on |
  | --- | --- | --- | --- | --- |
  | Prothom Alo | 1005 | 351 | -65% | 117 to 27 |
  | Kaler Kantho | 2351 | 318 | -87% | 185 to 53 |
  | Jugantor | 2467 | 447 | -82% | 205 to 81 |
  | bdnews24 | 1143 | 809 | -29% | 60 to 44 |
  | The Daily Star | 11473 | 492 | -96% | 238 to 133 |
  | All five | 18439 | 2417 | -87% | |

  Load time is from the start of navigation to the load event, on a home connection. An earlier run gave the same picture (all five faster, -92% overall). Filter cost inside the app: about 73 µs per request.
- Screenshots of all five sites with blocking on (saved by the benchmark) show the full page with headlines, photos and menus; only the ads are gone (empty ad spaces and "Advertisement" labels remain). With blocking off, Prothom Alo showed a full-screen pop-up ad.
- **HTTPS self-test** (`--https-selftest`), all 8 checks passed: example.com upgraded silently; http.badssl.com (redirects back to http), expired.badssl.com (bad certificate) and neverssl.com (no HTTPS) each showed the warning; a wrong code was ignored; "continue anyway" loaded the http page; a second visit in the same session was not warned again.

### Bugs found and fixed this session
- An early self-test flagged neverssl.com. A navigation trace showed the app was right (that site jumps to random http-only subdomains, each correctly warned about) and the test expectation was wrong; the continue checks now use http.badssl.com.
- Filter matching used a redundant case-insensitive regex; removing it cut the cost from about 98 to 73 µs per request.

### Known issues
- Empty ad spaces and "Advertisement" labels remain (element hiding is not in v1).
- In the benchmark, the first load in a fresh private window sometimes reports ConnectionAborted for Prothom Alo (with blocking off). It looks like a race with the window's own first page and did not affect normal browsing.
- A site whose HTTPS hangs without an error takes as long as the engine's own timeout before the warning appears (the self-test cases took about 6 s).
- Bangla searches still show a percent-encoded address (from Milestone 4).
- The shield flyout could not be driven by screen automation this session (other windows kept taking the foreground), so it is covered by code review and unit tests of the stored choice; please check it in the owner test.

### Next
Milestone 6: scam shield and lookalike domain alarm.

## Session 4 (2026-09-26): Milestone 4, Bangla phonetic typing

Status: **approved** (2026-09-26).

### Security notes (read first)
- **Found and avoided a design flaw before shipping:** messages from the app to a page can be read by any script on that page. The page bridge now uses two separate secrets per tab: the page-to-app token is never sent to the page, so pages still cannot forge commands; app-to-page messages use a different marker and carry only harmless data (typing on/off, word suggestions). A page that reads them can at most fake suggestions inside its own tab.
- Phonetic typing never runs in password fields, fields named or marked like a password, PIN, OTP or card code, or email, number, phone and URL fields. The check runs on every key press, so a "show password" button that turns a password field into a text field is still skipped (tested).
- Only real key presses are converted (`isTrusted`); clicks on the on-page button are also checked.
- Per-site memory stores only sites where typing is switched on, so it is not a history of every site. Private windows keep it in memory only (tested: nothing written). "কুকি ও সাইটের ডেটা মুছুন" in Settings also clears it.

### License decision (owner approved)
- Official Avro Phonetic rules, **MPL 2.0**, from ibus-avro (commit dd521a1). Three files are MPL 2.0: the rules JSON, the C# port and the JS engine. Their source (and our changes) must be made available to people who receive Obhijatri; the rest of the app is not affected. Recorded in `THIRD_PARTY_NOTICES.md`.
- Suggestion word list: written for Obhijatri (about 900 common words, 13 KB), no license obligations.

### What was built
- `Obhijatri.Bangla/Phonetic`: `AvroPhonetic` (faithful C# port of the Avro algorithm), `LooseKey` (a "sounds like" key so `tomar`, `tOmar` and তোমার match), `PhoneticSuggester` (exact Avro result first, then same-sounding common words, then words that start the same), `BanglaWords.txt`.
- Web pages: `phonetic-typing.js` converts as you type in `input` (text, search), `textarea` and `contenteditable` (Facebook, Gmail style editors), including iframes. Text is inserted with the editor's own insert command so undo and site scripts (React, Facebook, Google) see normal typing. Floating অ/A button next to the focused box, Ctrl+M toggle, suggestion list (arrow keys, Enter or Tab to pick, Esc to close, click to pick).
- Address bar: its own অ/A button (off by default) and Ctrl+M while the address bar has focus, with the same suggestion list. Web addresses are easy to type because it stays off unless switched on.
- Settings, প্রধান সেটিংস: a বাংলা টাইপিং card explaining how it works, and the address bar switch.
- Database schema version 2 (`site_preferences` table) with an upgrade from version 1 that keeps existing data (tested on the real profile: 33 history rows kept).
- Page script size: about 38 KB per frame after compacting the rules (rules 15 KB, scripts 23 KB).

### What was tested
- `dotnet build` Debug: 0 warnings, 0 errors. `dotnet test`: **229 passed**, including the plan sentence "ami banglay gan gai" giving "আমি বাংলায় গান গাই" and **95 reference words** whose expected output was produced by running the original Avro library. `node tools/test_phonetic_js.js`: the web-page engine also matches all 95.
- **Password-field acceptance test** with `tests/Fixtures/phonetic-fields.html` served from localhost, typing `ami12` into each field with typing on:
  - converted: text (আমি বাংলায়), search (আমি১২), textarea (আমি১২), contenteditable (আমি১, typed key by key), text inside an iframe (আমি১২)
  - untouched: password (ami12), password shown as text by a "show" button (ami12), email (ami12), field named otp (ami), number (12)
- Address bar: `bangladesh` gave বাংলাদেশ; `tomar` offered তমার and তোমার, picking with arrow and Enter worked; Enter then searched Google for "বাংলাদেশ তোমার".
- Per-site memory survived a restart; private window typed Bangla but stored nothing; Google's search box: `amar sonar` gave আমার সনার and Google's own autocomplete reacted.
- Test site choices were removed afterwards; the profile has no stored site choices.

### Bugs found and fixed this session
- Address bar did not convert: in the "before text change" event the caret is already after the new character. Now worked out from the old and new text.
- Escape did not close the web-page suggestion list when a reply was still on its way. Pending replies are now dropped when the list closes.
- The address bar typing button colour would not follow light/dark changes (same class of bug as in Milestone 3). Now a theme-aware style.

### Known issues
- Typing tools that inject characters without real key codes (some on-screen keyboards and automation tools) are not converted; a physical keyboard is. Found because the test tool's "type text" mode was not converted while its key presses were.
- The floating অ/A button sits inside the right edge of the focused box and can cover a site's own icons there (for example Google's keyboard icon). Feedback welcome on placement.
- The suggestion list can cover the field below it until a word is finished or Esc is pressed.
- Searches in Bangla show a percent-encoded address in the address bar (for example `q=%E0%A6...`). Showing it readable needs care against spoofing; planned for later.
- Avro spelling rules apply: for example `sonar` gives সনার (Avro users type `sOnar` for সোনার); the suggestion list covers common words only (about 900).
- Nested iframes (a frame inside a frame) do not get typing; direct iframes do.

### Next
Milestone 5: ad and tracker blocking, HTTPS-only.

## Session 3 (2026-09-25): Milestone 3, full Bangla UI

Status: **approved** (2026-09-26).

### Security notes
- No new security issues. The new home page setting only accepts http and https addresses (for example `javascript:` is rejected), and every setting read from the database is validated, so a damaged or edited value falls back to the safe default.

### What was built
- **Settings page** (`obhijatri://settings`, menu, Ctrl+,) with the five sections from the plan:
  - প্রধান সেটিংস: language (বাংলা / English, applies after a restart with a "এখনই রিস্টার্ট করুন" button that brings the tabs back), home page (validated), search engine (Google, Bing, DuckDuckGo; Google stays the default), reopen previous tabs.
  - নিরাপত্তা: Microsoft Defender SmartScreen on/off (on by default); notes on pop-up blocking and safe addresses, which are always on.
  - প্রাইভেসি: tracking protection (বেসিক / ব্যালান্সড / স্ট্রিক্ট, default ব্যালান্সড; Milestone 5 makes strict the default), clear cookies and site data (with confirmation), link to History.
  - থিম: light and dark mode (Windows-এর মতো / লাইট / ডার্ক, applied live to the window, title bar buttons and web pages), bookmark bar, vertical tabs.
  - অ্যাডভান্সড: data folder (open in Explorer) and version.
- **Language toggle**: UI strings, WinUI's own control text and the web engine's menus all follow the setting. English mode was tested end to end, then switched back to Bangla.
- **Bangla numerals** in `Obhijatri.Bangla/BanglaNumerals` (০ to ৯, decimals, lakh grouping ১২,৩৪,৫৬৭ or thousands grouping). Used for tab counts (vertical tab list), download sizes, history dates, import counts and the version number. The RAM meter arrives in Milestone 8 and will use the same helper.
- **Bangla dates**: for example "২৫ সেপ্টেম্বর ২০২৬, সন্ধ্যা ৭:১১" (part of the day: ভোর, সকাল, দুপুর, বিকাল, সন্ধ্যা, রাত). English: "25 Sep 2026, 7:15 PM".
- **Fonts**: Hind Siliguri (Regular, SemiBold) as the UI font and Noto Sans Bengali as fallback, both SIL OFL 1.1, downloaded with the owner's permission from github.com/google/fonts and listed in `THIRD_PARTY_NOTICES.md`. About 1 MB in total. English UI keeps Segoe UI and uses the bundled fonts for Bangla text.
- **Wording**: after the owner's feedback, Bangla strings use familiar loanwords instead of formal translations (হিস্ট্রি, প্রাইভেট, প্রাইভেসি, সেভ, রিলোড, পেজ, অ্যাড্রেস বার, সার্চ, বাটন, লাইট/ডার্ক, অ্যাডভান্সড, ভার্সন, ইমপোর্ট). 65 strings revised.
- **Tools**: `tools/gen_strings.py` (source of truth for both `.resw` files; checks duplicate keys and dashes) and `tools/check_strings.py` (fails if user-facing text is hardcoded in C# or XAML, or if a dash appears in source, resources or docs).
- Version set to 0.3.0.

### What was tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: 108 passed (38 new numeral tests, 13 settings tests, address-only detection tests).
- `python tools/check_strings.py`: passes; also confirmed it catches a planted Bangla and a planted English string.
- In the running app: every settings section, theme switch live (dark to light and back, descriptions stay readable), language switch to English and back with the restart button (one process, tabs restored), English engine context menu, home page validation (`javascript:alert(1)` rejected, `prothomalo.com` saved as `https://prothomalo.com/`, empty resets to default), tab count "৫টি ট্যাব খোলা", Bangla and English history dates.
- All settings were put back to defaults after testing (Bangla, Google home page, theme same as Windows, horizontal tabs).

### Owner feedback applied
- Section names সাধারণ and চেহারা were unclear: now প্রধান সেটিংস and থিম (the theme option inside is now লাইট ও ডার্ক মোড).

### Bugs found and fixed this session
- Settings card descriptions disappeared after switching theme (colours were read once in code). Now theme-aware styles.
- History and Settings tab titles stayed in the old language after a restart (saved titles were reused). Built-in pages now always use the current language.
- Bangla history dates wrapped onto two lines; the date column is wider.

### Known issues
- Some WinUI built-in text (for example TabView's "close tab" tooltip) uses WinUI's own `bn-IN` translation. Please report any odd wording there.
- Web pages keep their own fonts; the "Fix Bangla fonts" option for websites is Milestone 9.
- Month names in the Bangla UI are Gregorian (সেপ্টেম্বর); the Bangabda calendar is Milestone 9.
- The English UI uses short month names (Sep) while Bangla uses full names; intended, but easy to change.

### Next
Milestone 4: Bangla phonetic typing.

## Session 2 (2026-09-25): Milestone 2, tabs, history, bookmarks, downloads

Status: **approved** (2026-09-25). Milestone 1 was approved and committed; git set up on branch `main`.

### Security notes (read first)
- **Web messages are now enabled** (they were off in Milestone 1). WebView2 in WinUI does not pass Ctrl+T, Ctrl+W and similar keys to the app while a page has focus, so a small injected script catches those keys and posts them to the app. Each tab has its own random 128-bit token held inside the script's closure; the app ignores any message without it, and only real key presses (`isTrusted`) are sent. A page can therefore not trigger browser actions by calling `postMessage` itself. Side effect: pages can see that `window.chrome.webview` exists (a small fingerprinting signal).
- **Downloaded files can be opened without scanning** until Milestone 7. WebView2's own SmartScreen check still runs.

### What was built
- Tabs (TabView in the title bar): new, close, drag to reorder, middle-click close, Ctrl+T, Ctrl+W / Ctrl+F4, Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+Shift+T (reopen, back at the same position). Favicons and page titles on tabs. Closing the last tab closes the window.
- Vertical tabs: menu toggle ("ট্যাব পাশে দেখান"), saved as a setting. Side list with reorder, close button and middle-click close.
- SQLite database `%LOCALAPPDATA%\Obhijatri\obhijatri.db` (WAL): history, bookmarks, settings, session. If the file cannot be opened the app runs with an in-memory database and shows a warning (full recovery is Milestone 11).
- History page (`obhijatri://history`, Ctrl+H): search, remove single entries, clear last hour / last 24 hours / all, with a confirmation dialog. Clearing also clears the engine's own browsing history. Clicking an entry opens it in a new tab.
- Bookmarks: star button (Ctrl+D) adds the page and opens a small editor (name, folder, remove). Bookmark bar with folders (nested menus), right-click to open in new tab, rename, delete, new folder. Ctrl+click or middle-click opens in a new tab. Bar toggle: Ctrl+Shift+B.
- Import from Chrome/Edge "export to HTML" files. Everything goes into a new "আমদানি করা বুকমার্ক" folder; the Chrome "Bookmarks bar" / Edge "Favorites bar" level is flattened into it. Only http and https links are imported (javascript: bookmarklets and file: links are skipped and counted). File size capped at 20 MB.
- Downloads panel (Ctrl+J and toolbar button): progress, size, open, show in folder, cancel. Replaces the engine's own download popup. Private windows keep their own list.
- Private window (Ctrl+Shift+N): WebView2 InPrivate profile, no history, tabs not saved, own downloads list, badge "ব্যক্তিগত" and window title prefix.
- Lazy session restore: all tabs come back with their titles, only the active one starts an engine.
- Popups opened by a user click become new tabs that stay connected to the opener (needed for sign-in popups). Popups the user did not trigger are blocked.
- Single instance: starting Obhijatri again brings the running window to the front with a new tab instead of starting a second process.
- Test project `tests/Obhijatri.Tests` with 49 tests: history store, bookmark store, bookmark import (Chrome and Edge fixtures, unsafe links, deep nesting), session, settings, closed-tab stack, address resolver.

### What was tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors. `dotnet test`: 49 passed.
- By hand in the running app (driven with screen automation):
  - Ctrl+T, Ctrl+Tab, Ctrl+H, Ctrl+W, Ctrl+Shift+T and Ctrl+Shift+N, both with the page focused and with the toolbar focused.
  - Drag reorder, middle-click close (horizontal and vertical), close button, vertical tabs on and off.
  - Star, rename bookmark, bookmark bar, folder menus, import of `tests/Fixtures/bookmarks_chrome.html` (6 added, 2 skipped).
  - History page: search, clear last hour with confirmation, open entry.
  - Download of a local test file served from localhost: panel shows "সম্পন্ন, 2.9 এমবি". The test file was removed from the Downloads folder afterwards.
  - Private window: visited a test URL, database has 0 rows for it; private tabs not in the saved session.
  - Restart: 3 tabs restored with titles, and 0 engine processes until a web tab was shown. Bookmarks persisted.
  - Second launch while running: still one process, new tab opened in the existing window.
- **20-tab acceptance**: baseline 10 WebView2 processes (5 renderers). With 20 Google tabs: 28 processes (23 renderers), about 2.7 GB for all engine processes. After closing 18 tabs: back to 10 processes (5 renderers). After closing the window: 0. No crash, no leaked processes.

### Bugs found and fixed this session
- `BookmarkStore.Move` read inside an open transaction without passing it (caught by a unit test).
- Page-focused keyboard shortcuts did nothing (WebView2 swallows them): fixed with the shortcut bridge.
- A history entry got the next page's title on sites that change the title before the address (Prothom Alo). Same-page navigations are now recorded after a short settle delay, and later title changes only fill a missing title.
- Middle-click did not close tabs in the vertical list.
- Long bookmark titles wrapped onto two lines on the bar.
- Favicons did not show in the vertical tab list.
- Running the exe twice started two independent processes writing the same database: now single instance.
- The private window opened exactly on top of the normal window: now slightly offset.

### Known issues
- Shortcuts do not work while focus is inside a page where scripts cannot run (for example the built-in PDF viewer or engine error pages). Clicking the toolbar first works.
- The shell process used about 142 MB after restore and 179 MB after the 20-tab test. Likely memory the runtime has not collected yet; to be measured properly in Milestone 8.
- 20 open tabs use a lot of memory (about 2.7 GB of engine processes for 20 Google tabs). Tab sleeping is Milestone 8.
- WinUI's own tooltips (for example the TabView "close tab" and "new tab" buttons) could not be checked by screen capture; please check whether they show Bangla.
- The file picker filter label reads "All Files (*.html;*.htm)" from Windows App SDK; it cannot be localized by us.
- The history page shows dates in English style (for example "25 Sep 2026, 17:18"). Bangla numerals and dates are Milestone 3.
- Opening a downloaded file does not wait for a virus scan yet (Milestone 7).
- History is not deduplicated: 20 tabs opening Google give 20 entries, as in other browsers.

### Next
Milestone 3: full Bangla UI (settings page, language toggle, Bangla numerals, bundled fonts).

## Session 1 (2026-09-25): Milestone 1, shell and single tab

Status: **approved** (2026-09-25).

### What was built
- Solution `Obhijatri.sln` with projects `Obhijatri.App` (WinUI 3), `Obhijatri.Core`, `Obhijatri.Safety`, `Obhijatri.Bangla`, `Obhijatri.AI`. Safety, Bangla and AI are empty placeholders for later milestones.
- `Directory.Build.props`: nullable on, warnings treated as errors for all projects.
- Packages: `Microsoft.WindowsAppSDK` 2.5.1, `Microsoft.Web.WebView2` 1.0.4191.47 (both on the approved list).
- Main window: Mica backdrop, custom title bar showing the page title, back, forward, reload (turns into stop while loading), home, address bar, loading bar, error bar.
- WebView2 environment with engine language `bn-IN` and user data in `%LOCALAPPDATA%\Obhijatri\WebView2`.
- Address bar (`Obhijatri.Core/AddressResolver.cs`): web addresses open directly (https assumed, http for localhost), anything else becomes a Google search. Schemes other than http and https (`javascript:`, `data:`, `file:`, `vbscript:`, `ms-*`) are treated as search text.
- Security defaults: host objects and web messages off, password and form autofill off, top-frame navigation limited to http, https and about:blank, popups open in the same view only if the user triggered them, DevTools off in Release builds.
- Keyboard: Alt+Left, Alt+Right, F5 or Ctrl+R, Alt+Home, Ctrl+L or Alt+D or F6 (address bar), Enter, Esc.
- All UI text is in `Strings/bn-BD` and `Strings/en-US` `.resw` files. The UI is Bangla by default.
- Local crash log in `%LOCALAPPDATA%\Obhijatri\logs\crash.log`.

### What was tested
- `dotnet build` Debug and Release: 0 warnings, 0 errors.
- Launched the app: Google loads. Typed `prothomalo.com`: Prothom Alo loads, and the title bar shows the page title.
- Back, forward and reload tested by clicking. Button enable states update correctly.
- Right-click on a page: the engine context menu is in Bangla (পিছনে যান, রিফ্রেশ করুন, মুদ্রণ...).
- Scratch check of `AddressResolver`: 20 cases passed, including the dangerous schemes. Real unit tests come in Milestone 2.
- Rough numbers (Release, one Google tab, not a formal Milestone 8 measurement): window shown in about 1.07 s, shell process about 137 MB working set, 6 WebView2 processes about 362 MB. No WebView2 processes are left running after the app closes.

### Bugs found and fixed this session
- Crash when clicking the address bar: a CsWinRT delegate marshalling bug. Fixed by passing a lambda.
- The first click in the address bar did not select the URL, so typed text was added to the old address. Fixed.
- The engine context menu was in English with `Language = "bn"`, because the runtime ships only `bn-IN`. Changed to `bn-IN`.

### Known issues
- The engine uses Indian Bangla (`bn-IN`) strings for its own menus. They are very close to Bangladesh usage, but a few words may differ. Websites receive `bn-IN` as their preferred language.
- The "Inspect" item in the context menu is in English in Debug builds (DevTools is not localized). It is hidden in Release builds.
- Input such as `file.txt` is treated as a web address, because there is no public suffix list yet.
- The home page is Google until the new tab page arrives (Milestone 9). The default search engine is still an open owner decision.
- Cold start was about 1.07 s against a budget of under 1 s. This will be measured properly and tuned in Milestone 8.
- The project is not a git repository yet.

### Next
Milestone 2: tabs, history, bookmarks, downloads panel, private window, session restore, first unit tests.
