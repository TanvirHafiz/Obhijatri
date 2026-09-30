# Builds the Obhijatri MSIX from a framework-dependent Release publish.
# Usage: powershell -File tools\package\build-package.ps1 [-Out <folder>] [-Pfx <file> -PfxPassword <text>]
# Without -Pfx a TEST certificate (CN=Obhijatri) is created, exported next to the package and NOT trusted
# on this PC. A real release needs a real code signing certificate whose subject equals the manifest Publisher.
param(
    [string]$Out = "$PSScriptRoot\..\..\artifacts\package",
    [string]$Pfx,
    [string]$PfxPassword = "obhijatri-test"
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$layout = Join-Path $Out "layout"
if (Test-Path $layout) { [System.IO.Directory]::Delete($layout, $true) }

$proj = Join-Path $root "src\Obhijatri.App\Obhijatri.App.csproj"
[xml]$p = Get-Content $proj
$version = ($p.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "No <Version> in the project file." }
$msixVersion = "$version.0"

dotnet publish $proj -c Release -r win-x64 --self-contained false -p:WindowsAppSDKSelfContained=false -p:Platform=x64 -o $layout
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# The Windows AI libraries are pulled in by the App SDK but Obhijatri never calls them (about 40 MB).
$drop = @("onnxruntime.dll", "DirectML.dll", "Microsoft.Windows.AI*", "Microsoft.Asg.*", "*.pdb")
foreach ($pattern in $drop) {
    Get-ChildItem $layout -Recurse -File -Filter $pattern | ForEach-Object { [System.IO.File]::Delete($_.FullName) }
}

$manifest = (Get-Content (Join-Path $PSScriptRoot "AppxManifest.template.xml") -Raw).Replace("@VERSION@", $msixVersion)
[System.IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding($false)))

$tools = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Recurse -Filter makeappx.exe |
    Where-Object { $_.FullName -match "\\x64\\" } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $tools) { throw "makeappx.exe not found (build the app once so the SDK build tools are restored)." }
$bin = $tools.DirectoryName

$msix = Join-Path $Out "Obhijatri.msix"
if (Test-Path $msix) { [System.IO.File]::Delete($msix) }
& "$bin\makeappx.exe" pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }

if (-not $Pfx) {
    $Pfx = Join-Path $Out "Obhijatri-test.pfx"
    if (-not (Test-Path $Pfx)) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Obhijatri" -KeyUsage DigitalSignature `
            -FriendlyName "Obhijatri test signing" -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
        $secure = ConvertTo-SecureString $PfxPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $Pfx -Password $secure | Out-Null
        Export-Certificate -Cert $cert -FilePath (Join-Path $Out "Obhijatri-test.cer") | Out-Null
        Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey
    }
}
& "$bin\signtool.exe" sign /fd SHA256 /f $Pfx /p $PfxPassword $msix
if ($LASTEXITCODE -ne 0) { throw "signtool failed" }

$size = [math]::Round((Get-Item $msix).Length / 1MB, 2)
Write-Host "Package: $msix ($size MB), version $msixVersion"
