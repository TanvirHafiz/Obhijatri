# Installing Obhijatri (test build)

Download two files from the latest release and put them in the same folder:

- `Setup.exe`
- `Obhijatri.msix`

Then:

1. Connect to the internet and double click `Setup.exe`. Add `/en` (run it from PowerShell as `.\Setup.exe /en`) for English text.
2. Setup installs what is missing: the WebView2 runtime, the .NET 8 Desktop Runtime and the Windows App Runtime 2.5 (about 180 MB in total on a PC that has none of them). Each download is checked for a Microsoft signature.
3. This is a test build, signed with a test certificate (`CN=Obhijatri`). Setup asks "Trust it? (y/n)". Type `y` and accept the Administrator prompt. Only that one certificate is added, to Trusted People.
4. When it prints "Done", open Obhijatri from the Start menu.

Requirements: 64-bit Windows 10 (1809 or newer) or Windows 11.

## Uninstall

Settings, Apps, Obhijatri, Uninstall. The app's data goes with it. The prerequisites stay, because other programs may use them. To remove the test certificate too: run `certmgr.msc` (or the Certificates snap-in for the computer), open Trusted People, Certificates, and delete `Obhijatri`.

## If something goes wrong

- "could not be resolved": the PC is offline or DNS is failing. Fix the connection and run `Setup.exe` again. It only installs what is missing.
- "The package did not install": the certificate was not trusted. Run `Setup.exe` again and answer `y`.
- Crash log, kept on your PC and never uploaded: `%LOCALAPPDATA%\Obhijatri\logs\crash.log` (or the packaged data folder shown on the About page).
