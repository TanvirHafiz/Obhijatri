# CLAUDE.md: Obhijatri (অভিযাত্রী)

Full plan: `plan.md`. Session log: `PROGRESS.md`. Read both before starting.

## Rules
1. One milestone at a time. Finish, build, test, then STOP and report. Wait for the owner's "approved".
2. Always build before reporting: `dotnet build Obhijatri.sln` (plus tests from Milestone 2). Report errors honestly.
3. Ask before adding any NuGet package not listed in plan.md section 3.
4. No telemetry, no analytics, no cloud AI. Page content never leaves the PC except Safe Browsing hash prefixes and HIBP k-anonymity range queries.
5. Every UI string lives in `Strings/bn-BD/Resources.resw` and `Strings/en-US/Resources.resw`. No hardcoded user-facing text in XAML or C#.
6. Bangla is the default language. English is the fallback and a settings toggle.
7. No em dashes in any UI string, message, or doc. Use commas, colons, or full stops.
8. Keep it light. Performance budgets in plan.md section 6 are requirements.
9. Security fixes over features. Flag security problems in earlier code at the top of the report.
10. Update `PROGRESS.md` at the end of every session.

## Project notes
- Build: `dotnet build Obhijatri.sln` (x64 by default). Run: `src/Obhijatri.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/Obhijatri.exe`.
- App is unpackaged and self-contained Windows App SDK for now; MSIX comes in Milestone 11.
- Read strings with `Localization.Strings.Get("Key")`. The UI language is set explicitly, not taken from Windows.
- WebView2 engine language is `bn-IN`: the runtime ships no `bn` or `bn-BD` locale pack.
- CsWinRT pitfall: pass lambdas, not method groups bound to WinRT objects, to `DispatcherQueue.TryEnqueue` (crashes with "Class not registered").
- Local crash log: `%LOCALAPPDATA%\Obhijatri\logs\crash.log`. Never uploaded.
- Tests: `dotnet test tests/Obhijatri.Tests`. Fixtures live in `tests/Fixtures` and are copied to the test output.
- Single instance: custom `Main` in `Program.cs` (`DISABLE_XAML_GENERATED_MAIN`). A second launch redirects to the running process.
- Page scripts: `Browser/PageBridge.cs` builds one script per tab from `Web/*.js` (embedded resources): `bridge-prelude.js` (messaging), `bridge-shortcuts.js` (browser shortcuts while a page has focus; WinUI XAML accelerators do not fire then, so add new ones in both places), `avro-phonetic.js` (MPL 2.0) and `phonetic-typing.js`.
- Page bridge security: the outgoing token must never be sent to the page or passed to any function a page could replace. Messages from the app to the page can be read by page scripts, so never put anything sensitive in `PageBridge.Compose`. Never act on a web message without `PageBridge.Parse`.
- Phonetic typing: Avro rules live once in `Obhijatri.Bangla/Phonetic/AvroPhoneticRules.json` (MPL 2.0), used by the C# engine and the page script. `tests/Fixtures/phonetic-cases.json` comes from the original Avro library; `node tools/test_phonetic_js.js` checks the JS engine against it. Per-site on/off is stored only when on (`SitePreferencesStore`); private windows keep it in memory.
- Ad blocking: `Obhijatri.Safety/Filtering` (FilterRule, FilterEngine, FilterListStore); `Services/FilterService` owns the engine (built off the UI thread, swapped atomically). The per-request path (`BrowserTab.Core_WebResourceRequested`) runs on the UI thread for every request: never touch the database or do I/O there. Unsupported filter options must skip the rule, not be ignored.
- HTTPS-only and warning pages: `Safety/HttpsUpgrade`, `Browser/Interstitial` (+ `Web/interstitial.html`: no script, HTML-encoded values, nonce in action links on host `obhijatri.invalid`). Milestone 6 scam warnings should reuse `Interstitial`.
- Developer tools (Debug builds only): `Obhijatri.exe --benchmark` (page load on the 5 BD news sites, blocking off vs on, with screenshots) and `--https-selftest`; output in `%LOCALAPPDATA%\Obhijatri\logs`. Files that start with `#if DEBUG` are skipped by the string check.
- Each tab creates its WebView2 lazily; closing a tab must call `BrowserTab.Close()` so engine processes exit.
- Private windows: `IsInPrivateModeEnabled` controller option, `History` is null, session is never saved.
- Strings: `tools/gen_strings.py` is the source of truth. Edit its rows and run `python tools/gen_strings.py` to rewrite both `.resw` files; never hand-edit the `.resw` files. `python tools/check_strings.py` must pass (no hardcoded UI text, no dashes). WinUI's own built-in text follows `ApplicationLanguages.PrimaryLanguageOverride` set in `App()`.
- Bangla wording: use the loanwords people actually say (সেটিংস, থিম, হিস্ট্রি, প্রাইভেসি, প্রাইভেট, সেভ, রিলোড, বাটন, পেজ, সার্চ, লাইট/ডার্ক). Do not force formal translations such as গোপনীয়তা, সংরক্ষণ, সংস্করণ.
- Numbers, sizes and dates shown to users go through `Localization/Formatting` (Bangla digits and lakh grouping in the Bangla UI). Never format numbers straight into UI text.
- Settings: `Obhijatri.Core/Settings/BrowserSettings` (typed, validated). Windows listen to `Changed` and apply changes live; the UI language needs a restart (`App.Restart()`).
- Fonts: bundled in `Assets/Fonts` (OFL, see `THIRD_PARTY_NOTICES.md`) and applied in `Localization/AppFonts`. Colours for UI built in code must come from theme-aware styles in `App.xaml`, not brushes read once in code.

## Lessons learned (read before working)
- The owner may have Obhijatri open. Before building, check with `tasklist`. If a running instance was not started by you (compare its start time), do not close it: ask the owner, or wait. Because of single instance, launching your build while theirs runs only opens a tab in their window.
- Screen automation can lose the foreground to the owner's other windows (Avro Keyboard toolbar, qBittorrent). Prefer the Debug-only `--benchmark` and `--https-selftest` style: add a small self-test mode that captures screenshots with `CapturePreviewAsync` and writes results to `logs`.
- Automation "type text" sends Unicode packets that web pages do not treat as real key presses; use individual key presses to test phonetic typing.
- Fast numbers can hide broken pages: always look at screenshots before reporting speed results.
- Editing files through bash heredocs or inline Python mangles backslashes and quotes (\n, \u, \b). Use the Edit tool, or write a Python script file and run it.
- Colours for UI built in code must come from theme-aware styles, or they break when the theme changes.
- In `TextBox.BeforeTextChanging` the caret is already after the new character.
