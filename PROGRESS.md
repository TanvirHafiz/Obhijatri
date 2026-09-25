# PROGRESS.md

## Session 1 (2026-09-25): Milestone 1, shell and single tab

Status: **done, waiting for owner approval.**

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
