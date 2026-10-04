# TutorConnect: allow phones on the same network to reach IIS Express, for QR check-in during development.
# Safe to run more than once. Undo with: netsh http delete urlacl url=http://*:44380/
#                                  and: netsh advfirewall firewall delete rule name="TutorConnect phone access (44380)"
$ErrorActionPreference = 'Stop'
$solutionDir = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $solutionDir 'TutorConnect\TutorConnect.csproj'

# 1. Port from the project (IISUrl), default 44380
$port = 44380
if (Test-Path $csproj) {
    $m = Select-String -Path $csproj -Pattern '<IISUrl>https?://localhost:(\d+)/' | Select-Object -First 1
    if ($m) { $port = [int]$m.Matches[0].Groups[1].Value }
}
Write-Host "TutorConnect phone access for port $port" -ForegroundColor Cyan

# 2. Let IIS Express listen on every address for this port (Everyone = SID WD, works in any Windows language)
netsh http delete urlacl url="http://*:$port/" 2>$null | Out-Null
netsh http add urlacl url="http://*:$port/" sddl="D:(A;;GX;;;WD)" | Out-Null
Write-Host "  [ok] URL reservation added" -ForegroundColor Green

# 3. Firewall: allow incoming connections on this port
$rule = "TutorConnect phone access ($port)"
netsh advfirewall firewall delete rule name="$rule" 2>$null | Out-Null
netsh advfirewall firewall add rule name="$rule" dir=in action=allow protocol=TCP localport=$port profile=any | Out-Null
Write-Host "  [ok] Windows Firewall rule added" -ForegroundColor Green

# 4. IIS Express: add a binding for any host name next to the localhost one
$configs = @(
    (Join-Path $solutionDir '.vs\TutorConnect\config\applicationhost.config'),
    (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'IISExpress\config\applicationhost.config')
) | Where-Object { Test-Path $_ }
if (-not $configs) {
    Write-Host "  [!] IIS Express settings not found. Run the site once from Visual Studio (Ctrl+F5), then run this again." -ForegroundColor Yellow
}
foreach ($cfg in $configs) {
    [xml]$xml = Get-Content $cfg
    $changed = $false
    foreach ($site in $xml.configuration.'system.applicationHost'.sites.site) {
        $bindings = $site.bindings
        if (-not $bindings) { continue }
        $local = $bindings.binding | Where-Object { $_.bindingInformation -eq "*:${port}:localhost" }
        if (-not $local) { continue }
        $any = $bindings.binding | Where-Object { $_.bindingInformation -eq "*:${port}:*" }
        if (-not $any) {
            $b = $xml.CreateElement('binding')
            $b.SetAttribute('protocol', 'http')
            $b.SetAttribute('bindingInformation', "*:${port}:*")
            [void]$bindings.AppendChild($b)
            $changed = $true
        }
    }
    if ($changed) { $xml.Save($cfg); Write-Host "  [ok] IIS Express binding added in $cfg" -ForegroundColor Green }
    else { Write-Host "  [ok] IIS Express binding already present in $cfg" -ForegroundColor Green }
}

# 5. Restart IIS Express so the new binding is used
Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 6. Show the address phones will use
$ip = Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
      Select-Object -First 1 -ExpandProperty IPv4Address | Select-Object -First 1 -ExpandProperty IPAddress
Write-Host ""
Write-Host "Done. Start TutorConnect again in Visual Studio (Ctrl+F5)." -ForegroundColor Cyan
if ($ip) {
    Write-Host "Phones on the same Wi-Fi can now open:  http://${ip}:$port/" -ForegroundColor Cyan
    Write-Host "The QR code uses this address automatically."
}
Write-Host "If a phone still can't connect, make sure it's on the same Wi-Fi (not mobile data)."
