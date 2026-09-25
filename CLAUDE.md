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
