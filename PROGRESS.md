# PROGRESS.md

## Session 3 (2026-09-25): Milestone 3, full Bangla UI

Status: **done, waiting for owner approval.**

### Security notes
- No new security issues. The new home page setting only accepts http and https addresses (for example `javascript:` is rejected), and every setting read from the database is validated, so a damaged or edited value falls back to the safe default.

### What was built
- **Settings page** (`obhijatri://settings`, menu, Ctrl+,) with the five sections from the plan:
  - সাধারণ: language (বাংলা / English, applies after a restart with a "এখনই রিস্টার্ট করুন" button that brings the tabs back), home page (validated), search engine (Google, Bing, DuckDuckGo; Google stays the default), reopen previous tabs.
  - নিরাপত্তা: Microsoft Defender SmartScreen on/off (on by default); notes on pop-up blocking and safe addresses, which are always on.
  - প্রাইভেসি: tracking protection (বেসিক / ব্যালান্সড / স্ট্রিক্ট, default ব্যালান্সড; Milestone 5 makes strict the default), clear cookies and site data (with confirmation), link to History.
  - চেহারা: theme (Windows-এর মতো / লাইট / ডার্ক, applied live to the window, title bar buttons and web pages), bookmark bar, vertical tabs.
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
