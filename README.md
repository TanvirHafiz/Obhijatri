# Obhijatri (অভিযাত্রী)

A light web browser for Bangla speakers, for Windows 10 and 11. Built on WebView2 and WinUI 3.
Bangla is the default language. Phonetic Bangla typing, ad and tracker blocking, scam warnings,
Bangla calendar and prayer times, reader mode, and low memory use.

## Install

Download `Setup.exe` and `Obhijatri.msix` from the [latest release](https://github.com/TanvirHafiz/Obhijatri/releases/latest)
and follow [INSTALL.md](INSTALL.md).

## Privacy

No telemetry, no analytics, no cloud AI. Page content never leaves your PC. The only network requests
besides the pages you visit are: Safe Browsing hash prefixes, Have I Been Pwned range queries (k-anonymity),
filter list updates, and Google Translate only if you turn it on and use it. Everything is stored locally.

## Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by
[SignPath Foundation](https://signpath.org). (Not approved yet: until it is, releases are signed with a
self-made test certificate.)

- Team roles: author, reviewer and approver are all TanvirHafiz, the sole maintainer.
- Releases are built by the GitHub Actions workflow in `.github/workflows` from this repository and signed from that build only.
- The maintainer uses multi-factor authentication on GitHub and SignPath.
- Privacy: see the section above. The program does not send user data anywhere without the user turning a feature on.

## Building

`dotnet build Obhijatri.sln`, `dotnet test tests/Obhijatri.Tests`. Installer: `tools/package/build-installer.ps1`.

## License

MIT, see [LICENSE](LICENSE). Bundled components keep their own licenses: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
