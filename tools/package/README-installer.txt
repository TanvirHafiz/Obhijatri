Obhijatri (অভিযাত্রী) installer

1. Run Setup.exe. It installs, only if missing: the WebView2 runtime, the .NET 8 Desktop Runtime and the
   Windows App Runtime 2.5, downloaded from Microsoft and checked for a Microsoft signature. About 180 MB of
   downloads on a PC that has none of them, nothing on a PC that has them all.
2. Setup.exe then installs Obhijatri.msix.

Test builds are signed with a test certificate (CN=Obhijatri). Windows refuses a package whose certificate
it does not trust, so Setup.exe asks (y/n) and then adds it to Trusted People, with one Administrator prompt.
Only a self-signed CN=Obhijatri certificate is ever added this way. Manual way, if you prefer, in an Administrator PowerShell:
    Import-Certificate -FilePath .\Obhijatri-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Remove it again after testing (Certificates snap-in, Trusted People). A release build is signed with a real code
signing certificate, and then no such step is needed.

Uninstall: Settings, Apps, Obhijatri, Uninstall. Your data folder is removed with the app. The prerequisites
stay installed because other programs may use them.
