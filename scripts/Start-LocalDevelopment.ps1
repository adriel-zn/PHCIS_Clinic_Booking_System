# Starts both development hosts and leaves them running after this script exits.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$logDirectory = Join-Path $env:TEMP 'PHCIS-development'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

function Test-HostReady([string]$Url) {
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -eq 200
    }
    catch { return $false }
}

$hostsToStart = @(
    @{ Name = 'API'; Project = 'backend/PHCIS.Api'; Url = 'https://localhost:7014/swagger/index.html' },
    @{ Name = 'Blazor'; Project = 'frontend/PHCIS.Web'; Url = 'https://localhost:7100/login' }
)

foreach ($hostInfo in $hostsToStart) {
    if (Test-HostReady $hostInfo.Url) {
        Write-Host "$($hostInfo.Name) is already running: $($hostInfo.Url)"
        continue
    }

    $projectPath = Join-Path $projectRoot $hostInfo.Project
    $logPrefix = Join-Path $logDirectory "$($hostInfo.Name)-$(Get-Date -Format yyyyMMdd-HHmmss)-$PID"
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @(
        'run', '--project', "`"$projectPath`"", '--launch-profile', 'https'
    ) -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput "$logPrefix.log" -RedirectStandardError "$logPrefix.error.log"

    $deadline = (Get-Date).AddMinutes(2)
    while (-not (Test-HostReady $hostInfo.Url)) {
        $process.Refresh()
        if ($process.HasExited -or (Get-Date) -ge $deadline) {
            throw "$($hostInfo.Name) did not become ready. Check $logPrefix.log and $logPrefix.error.log."
        }
        Start-Sleep -Seconds 1
    }
    Write-Host "$($hostInfo.Name) started (PID $($process.Id)): $($hostInfo.Url)"
    Write-Host "Logs: $logPrefix.log and $logPrefix.error.log"
    Write-Host "To stop this host: Stop-Process -Id $($process.Id)"
}

Write-Host 'Both apps are ready. Open https://localhost:7100/login'
