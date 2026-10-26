param(
    [string]$DemoPassword = 'ClinicDemo!2026'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    if (-not (Get-Command SqlLocalDB.exe -ErrorAction SilentlyContinue)) {
        throw 'Install SQL Server Express LocalDB before running this script.'
    }
    & SqlLocalDB.exe start MSSQLLocalDB
    if ($LASTEXITCODE -ne 0) { throw 'LocalDB could not be started.' }

    & dotnet user-secrets set 'Development:SeedPassword' $DemoPassword --project backend/PHCIS.Api
    if ($LASTEXITCODE -ne 0) { throw 'Could not save the development seed password.' }
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    & dotnet restore PHCIS_Clinic_Booking_System.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
    & dotnet build PHCIS_Clinic_Booking_System.slnx --no-restore --disable-build-servers -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    # The API initializer refuses to seed any database other than PHCIS.Development.
    & dotnet run --project backend/PHCIS.Api --no-build --no-launch-profile -- --environment Development --Development:InitializeDatabase true --initialize-development
    if ($LASTEXITCODE -ne 0) { throw 'Development migration/seed failed.' }

    Write-Host 'Local setup complete. Demo email: patient@phcis.local'
    Write-Host 'The password you supplied is used only when the demo account is first created.'
    Write-Host 'Start API: dotnet run --project backend/PHCIS.Api --launch-profile https'
    Write-Host 'Start UI:  dotnet run --project frontend/PHCIS.Web --launch-profile https'
    Write-Host 'Open https://localhost:7100'
}
finally {
    Pop-Location
}

