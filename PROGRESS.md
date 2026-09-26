# PROGRESS.md

## Session 5 (2026-09-26): Milestone 5, ad and tracker blocking, HTTPS-only

Status: **done, waiting for owner approval.**

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
