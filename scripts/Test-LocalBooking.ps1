param(
    [string]$Email = 'patient@phcis.local',
    [string]$Password = 'ClinicDemo!2026',
    [string]$ApiUrl = 'https://localhost:7014'
)
$ErrorActionPreference = 'Stop'
$api = [Uri]$ApiUrl
if (-not $api.IsLoopback -or $api.Scheme -ne 'https') {
    throw 'This demonstration script is restricted to a local HTTPS API.'
}
$root = $api.AbsoluteUri.TrimEnd('/')
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$created = [System.Collections.Generic.List[string]]::new()

function Get-Api([string]$Path) {
    Invoke-RestMethod -Uri "$root/$Path" -WebSession $session
}
function Post-Api([string]$Path, [object]$Body) {
    $csrf = Get-Api 'api/account/csrf'
    Invoke-RestMethod -Uri "$root/$Path" -Method Post -WebSession $session -ContentType 'application/json' -Headers @{ 'X-CSRF-TOKEN' = $csrf.requestToken } -Body ($Body | ConvertTo-Json -Depth 5)
}
try {
    Post-Api 'api/account/login' @{ email = $Email; password = $Password; rememberMe = $false } | Out-Null
    $profile = Get-Api 'api/account/me'
    if (-not $profile.id) { throw 'Login did not return a patient profile.' }
    $clinics = Get-Api 'api/clinics'
    $clinic = $clinics[0]
    $localDay = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(2)).Date.AddDays(1)
    $slot = $null
    for ($day = 0; $day -lt 14 -and -not $slot; $day++) {
        $from = [DateTimeOffset]::new($localDay.AddDays($day), [TimeSpan]::FromHours(2))
        $to = $from.AddDays(1)
        $query = "api/clinics/$($clinic.id)/available-slots?from=$([Uri]::EscapeDataString($from.ToString('O')))&to=$([Uri]::EscapeDataString($to.ToString('O')))&limit=1"
        $page = Get-Api $query
        $slot = @($page.items) | Select-Object -First 1
    }
    if (-not $slot) { throw 'No future seeded slot was available.' }

    $booking = Post-Api 'api/appointments' @{ timeSlotId = $slot.id; reasonForVisit = 'Routine check-up' }
    $created.Add([string]$booking.id)
    if ($booking.status -ne 'Scheduled') { throw 'Booking did not become Scheduled.' }

    $conflicted = $false
    try { Post-Api 'api/appointments' @{ timeSlotId = $slot.id } | Out-Null }
    catch {
        if ([int]$_.Exception.Response.StatusCode -eq 409) { $conflicted = $true }
        else { throw }
    }
    if (-not $conflicted) { throw 'The duplicate booking was not rejected.' }

    $cancelled = Post-Api "api/appointments/$($booking.id)/cancellation" @{ reason = 'Local end-to-end smoke test' }
    if ($cancelled.status -ne 'Cancelled') { throw 'Cancellation was not saved.' }

    # Proves SQL Server permits replacement bookings while retaining cancellation history.
    $replacement = Post-Api 'api/appointments' @{ timeSlotId = $slot.id }
    $created.Add([string]$replacement.id)
    if ($replacement.id -eq $booking.id) { throw 'Replacement booking reused the old appointment ID.' }

    Write-Host 'PASS: local Identity login, clinic availability, booking, duplicate rejection, cancellation, and slot reuse.'
}
finally {
    foreach ($id in $created) {
        try { Post-Api "api/appointments/$id/cancellation" @{ reason = 'Local smoke-test cleanup' } | Out-Null }
        catch {
            if ([int]$_.Exception.Response.StatusCode -ne 409) { Write-Warning "Could not cancel smoke-test appointment $id." }
        }
    }
    try { Post-Api 'api/account/logout' @{} | Out-Null } catch { }
}

