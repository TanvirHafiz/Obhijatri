# Serves the Milestone 7 test page on http://localhost:8765/ for hands-on checks in Obhijatri.
# Run:  powershell -ExecutionPolicy Bypass -File tools\m7-checks\serve.ps1
# Stop with Ctrl+C. Only this computer can reach it. The "download" files are harmless text;
# the EICAR file is the industry standard antivirus test string, built here from two pieces so
# that this script itself is not flagged.
$port = 8765
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://localhost:$port/")
$listener.Start()
Write-Host "Open http://localhost:$port/ in Obhijatri. Press Ctrl+C to stop."

$eicar = 'X5O!P%@AP[4\PZX54(P^)7CC)7}$' + 'EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*'
try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $response = $context.Response
        $path = $context.Request.Url.AbsolutePath
        try {
            if ($path -eq '/') {
                $bytes = [System.IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'index.html'))
                $response.ContentType = 'text/html; charset=utf-8'
            }
            elseif ($path -like '/download/*') {
                $name = [System.Uri]::UnescapeDataString($path.Substring('/download/'.Length))
                $text = if ($name -eq 'eicar.com') { $eicar } else { 'Harmless test file for Obhijatri.' }
                $bytes = [System.Text.Encoding]::ASCII.GetBytes($text)
                $response.ContentType = 'application/octet-stream'
                $response.AddHeader('Content-Disposition', "attachment; filename=`"$name`"")
            }
            else {
                $response.StatusCode = 404
                $bytes = [byte[]]@()
            }
            $response.ContentLength64 = $bytes.Length
            $response.OutputStream.Write($bytes, 0, $bytes.Length)
        }
        catch { }
        finally { $response.Close() }
    }
}
finally { $listener.Stop() }
