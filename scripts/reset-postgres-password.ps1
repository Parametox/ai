$ErrorActionPreference = "Stop"

param(
  [string]$NewPassword,
  [string]$ServiceName = "postgresql-x64-17",
  [string]$HostName = "localhost",
  [int]$Port = 5432
)

if (-not $NewPassword) {
  throw "Podaj -NewPassword."
}

function Resolve-PsqlPath {
  $cmd = Get-Command "psql" -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }

  $inst = Get-ChildItem "HKLM:\SOFTWARE\PostgreSQL\Installations" -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $inst) { throw "Nie znaleziono instalacji PostgreSQL w rejestrze." }
  $props = Get-ItemProperty $inst.PSPath -ErrorAction SilentlyContinue
  $base = $props."Base Directory"
  if (-not $base) { $base = $props.BaseDirectory }
  $candidate = Join-Path $base "bin\\psql.exe"
  if (-not (Test-Path $candidate)) { throw "Nie znaleziono psql.exe pod: $candidate" }
  return $candidate
}

function Resolve-DataDirectory {
  $inst = Get-ChildItem "HKLM:\SOFTWARE\PostgreSQL\Installations" -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $inst) { throw "Nie znaleziono instalacji PostgreSQL w rejestrze." }
  $props = Get-ItemProperty $inst.PSPath -ErrorAction SilentlyContinue
  $data = $props."Data Directory"
  if (-not $data) { $data = $props.DataDirectory }
  if (-not $data) { throw "Nie znaleziono 'Data Directory' w rejestrze." }
  return $data
}

$psql = Resolve-PsqlPath
$dataDir = Resolve-DataDirectory
$pgHba = Join-Path $dataDir "pg_hba.conf"

if (-not (Test-Path $pgHba)) {
  throw "Nie znaleziono pg_hba.conf pod: $pgHba"
}

$backup = "$pgHba.bak.$(Get-Date -Format 'yyyyMMddHHmmss')"
Copy-Item $pgHba $backup -Force

Write-Host "Tymczasowo ustawiam TRUST dla localhost (backup: $backup)"

$trustBlock = @"
# --- TEMP TRUST (reset-postgres-password.ps1) ---
host  all  all  127.0.0.1/32  trust
host  all  all  ::1/128       trust
# --- END TEMP TRUST ---

"@

# Wstaw na początek, żeby nadpisać późniejsze reguły
$content = Get-Content $pgHba -Raw
Set-Content -Path $pgHba -Value ($trustBlock + $content) -Encoding ASCII

Write-Host "Restart usługi $ServiceName (wymaga uruchomienia PowerShell jako Administrator)..."
Restart-Service $ServiceName -Force

Write-Host "Ustawiam nowe hasło dla użytkownika postgres..."
& $psql -h $HostName -p $Port -U postgres -d postgres -v ON_ERROR_STOP=1 -c "ALTER USER postgres WITH PASSWORD '$NewPassword';"

Write-Host "Przywracam pg_hba.conf i restartuję usługę..."
Copy-Item $backup $pgHba -Force
Restart-Service $ServiceName -Force

Write-Host "OK: hasło postgres ustawione. Teraz możesz użyć:"
Write-Host '$env:PGPASSWORD="<NOWE_HASLO>"; .\scripts\setup-local-postgres.ps1'

