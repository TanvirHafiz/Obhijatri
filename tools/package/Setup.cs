// Obhijatri setup: installs the prerequisites that are missing, then the Obhijatri package.
// Built by build-installer.ps1 with the .NET Framework compiler that ships with Windows (no extra tools, about 15 KB).
// Nothing is sent anywhere except plain downloads from Microsoft's own addresses below. Every download is checked
// for a valid Microsoft Authenticode signature before it is run; a failed check stops the setup.
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Win32;

internal static class Setup
{
    private const string WebView2Url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private const string DotNetUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe";
    private const string AppRuntimeUrl = "https://aka.ms/windowsappsdk/2.5/latest/windowsappruntimeinstall-x64.exe";

    private static bool _bangla;

    private static string T(string bn, string en) { return _bangla ? bn : en; }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        _bangla = !Array.Exists(args, a => a == "/en");
        bool quiet = Array.Exists(args, a => a == "/quiet");
        try
        {
            if (!Environment.Is64BitOperatingSystem)
            {
                return Fail(T("এই ভার্সন শুধু ৬৪ বিটের Windows এ চলে।", "This version needs 64-bit Windows."));
            }
            string package = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Obhijatri.msix");
            if (!File.Exists(package))
            {
                return Fail(T("Obhijatri.msix ফাইলটি এই ফোল্ডারে নেই।", "Obhijatri.msix is missing from this folder."));
            }

            Say(T("অভিযাত্রী ইনস্টল হচ্ছে। দরকারি অংশগুলো দেখা হচ্ছে।", "Installing Obhijatri. Checking what is needed."));
            if (!HasWebView2())
            {
                Prerequisite("WebView2", WebView2Url, "/silent /install");
            }
            if (!HasDotNet8Desktop())
            {
                Prerequisite(".NET 8 Desktop Runtime", DotNetUrl, "/install /quiet /norestart");
            }
            if (!HasWindowsAppRuntime())
            {
                Prerequisite("Windows App Runtime 2.5", AppRuntimeUrl, "--quiet");
            }

            if (!EnsureTestCertificateTrusted(package))
            {
                return Fail(T(
                    "সার্টিফিকেট বিশ্বাস করা হয়নি, তাই ইনস্টল বন্ধ করা হলো।",
                    "The certificate was not trusted, so setup stopped."));
            }

            Say(T("অভিযাত্রী ইনস্টল হচ্ছে।", "Installing the Obhijatri package."));
            int code = RunPowerShell("Add-AppxPackage -Path '" + package.Replace("'", "''") + "'");
            if (code != 0)
            {
                return Fail(T(
                    "প্যাকেজ ইনস্টল হয়নি। সার্টিফিকেট বিশ্বাসযোগ্য না হলে এটা হতে পারে (README দেখুন)।",
                    "The package did not install. This can happen if its certificate is not trusted (see README)."));
            }
            Say(T("ইনস্টল হয়েছে। Start মেনু থেকে অভিযাত্রী খুলুন।", "Done. Open Obhijatri from the Start menu."));
            if (!quiet)
            {
                Console.WriteLine(T("বন্ধ করতে Enter চাপুন।", "Press Enter to close."));
                Console.ReadLine();
            }
            return 0;
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    // Test builds are signed with a self-made certificate (CN=Obhijatri). Windows only installs the package once that
    // certificate is in "Trusted People". This asks first, then adds it (one Administrator prompt). Certificates with any
    // other subject, or ones that are not self-signed, are never added: those need the real trust chain.
    private static bool EnsureTestCertificateTrusted(string package)
    {
        string q = package.Replace("'", "''");
        string probe =
            "$c = (Get-AuthenticodeSignature -LiteralPath '" + q + "').SignerCertificate; " +
            "if (-not $c) { exit 2 }; " +
            "if (Get-ChildItem Cert:\\LocalMachine\\TrustedPeople, Cert:\\LocalMachine\\Root | Where-Object { $_.Thumbprint -eq $c.Thumbprint }) { exit 0 }; " +
            "if ($c.Subject -eq 'CN=Obhijatri' -and $c.Issuer -eq $c.Subject) { exit 1 } else { exit 3 }";
        int state = RunPowerShell(probe);
        if (state == 0 || state == 3)
        {
            // Already trusted, or a certificate that should chain to a real authority: let Windows decide.
            return true;
        }
        if (state != 1)
        {
            return false;
        }

        Console.WriteLine(T(
            "এই প্যাকেজ টেস্ট সার্টিফিকেট (CN=Obhijatri) দিয়ে সাইন করা। ইনস্টলের জন্য এটা এই পিসিতে বিশ্বাসযোগ্য করতে হবে। করব? (y/n)",
            "This package is signed with a test certificate (CN=Obhijatri). It must be trusted on this PC before it can install. Trust it? (y/n)"));
        string answer = Console.ReadLine();
        if (answer == null || !answer.Trim().ToLowerInvariant().StartsWith("y"))
        {
            return false;
        }

        string add =
            "$c = (Get-AuthenticodeSignature -LiteralPath '" + q + "').SignerCertificate; " +
            "$f = Join-Path $env:TEMP 'obhijatri-test.cer'; Export-Certificate -Cert $c -FilePath $f | Out-Null; " +
            "Import-Certificate -FilePath $f -CertStoreLocation Cert:\\LocalMachine\\TrustedPeople | Out-Null; " +
            "Remove-Item $f";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(add));
        try
        {
            var info = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using (var p = Process.Start(info))
            {
                p.WaitForExit();
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The Administrator prompt was declined.
            return false;
        }
        return RunPowerShell(probe) == 0;
    }

    private static bool HasWebView2()
    {
        const string key = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var k = baseKey.OpenSubKey(key))
                {
                    var pv = k == null ? null : k.GetValue("pv") as string;
                    if (!string.IsNullOrEmpty(pv) && pv != "0.0.0.0")
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool HasDotNet8Desktop()
    {
        string dir = Path.Combine(Environment.GetEnvironmentVariable("ProgramW6432") ?? @"C:\Program Files",
            @"dotnet\shared\Microsoft.WindowsDesktop.App");
        return Directory.Exists(dir) && Directory.GetDirectories(dir, "8.*").Length > 0;
    }

    private static bool HasWindowsAppRuntime()
    {
        // Exit code 0 only when a 2.5 framework package (x64) is registered for this user.
        return RunPowerShell(
            "if (Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.2*' | Where-Object { $_.Architecture -eq 'X64' -and $_.Version -ge [version]'2.5.1.0' }) { exit 0 } else { exit 1 }") == 0;
    }

    private static void Prerequisite(string name, string url, string arguments)
    {
        Say(T(name + " ডাউনলোড হচ্ছে।", "Downloading " + name + "."));
        string file = Path.Combine(Path.GetTempPath(), "obhijatri-setup-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = new WebClient())
            {
                client.DownloadFile(url, file);
            }
            if (!IsSignedByMicrosoft(file))
            {
                throw new InvalidOperationException(T(
                    name + " এর ডিজিটাল সিগনেচার মেলেনি। ইনস্টল বন্ধ করা হলো।",
                    "The digital signature of " + name + " could not be verified. Setup stopped."));
            }
            Say(T(name + " ইনস্টল হচ্ছে।", "Installing " + name + "."));
            using (var p = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = true }))
            {
                p.WaitForExit();
                // 3010 means "installed, restart needed".
                if (p.ExitCode != 0 && p.ExitCode != 3010)
                {
                    throw new InvalidOperationException(T(
                        name + " ইনস্টল হয়নি (কোড " + p.ExitCode + ")।",
                        name + " did not install (code " + p.ExitCode + ")."));
                }
            }
        }
        finally
        {
            try { File.Delete(file); } catch (IOException) { }
        }
    }

    private static bool IsSignedByMicrosoft(string file)
    {
        string script =
            "$s = Get-AuthenticodeSignature -LiteralPath '" + file.Replace("'", "''") + "'; " +
            "if ($s.Status -eq 'Valid' -and $s.SignerCertificate.Subject -like '*O=Microsoft Corporation*') { exit 0 } else { exit 1 }";
        return RunPowerShell(script) == 0;
    }

    private static int RunPowerShell(string script)
    {
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var info = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using (var p = Process.Start(info))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    private static void Say(string text) { Console.WriteLine(text); }

    private static int Fail(string text)
    {
        Console.WriteLine(T("সমস্যা: ", "Problem: ") + text);
        Console.WriteLine(T("বন্ধ করতে Enter চাপুন।", "Press Enter to close."));
        try { Console.ReadLine(); } catch (IOException) { }
        return 1;
    }
}
