param(
  [string]$HostName = "localhost",
  [int]$Port = 5432,
  [string]$AdminUser = "postgres",
  [string]$DbName = "kanbanlite",
  [string]$AppUser = "kanbanlite",
  [string]$AppPassword = "kanbanlite"
)

$ErrorActionPreference = "Stop"

function Resolve-PsqlPath {
  $cmd = Get-Command "psql" -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }

  $roots = @(
    "HKLM:\SOFTWARE\PostgreSQL\Installations",
    "HKLM:\SOFTWARE\WOW6432Node\PostgreSQL\Installations"
  )

  foreach ($root in $roots) {
    $inst = Get-ChildItem $root -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $inst) { continue }

    $props = Get-ItemProperty $inst.PSPath -ErrorAction SilentlyContinue
    if (-not $props) { continue }

    # W rejestrze EDB wartości mają spacje w nazwach, np. "Base Directory"
    $base = $props."Base Directory"
    if (-not $base) { $base = $props.BaseDirectory }
    if (-not $base) { $base = $props.InstallLocation }
    if (-not $base) { continue }

    $candidate = Join-Path $base "bin\\psql.exe"
    if (Test-Path $candidate) { return $candidate }
  }

  throw "Nie znaleziono psql.exe. Zainstaluj PostgreSQL (wraz z Command Line Tools) albo dodaj '...\\PostgreSQL\\XX\\bin' do PATH."
}

$psql = Resolve-PsqlPath

Write-Host "Tworzenie roli i bazy w PostgreSQL..."
Write-Host "Host=$HostName Port=$Port AdminUser=$AdminUser Db=$DbName AppUser=$AppUser"

if (-not $env:PGPASSWORD) {
  Write-Host "Ustaw zmienną PGPASSWORD (hasło admina '$AdminUser'), np.:"
  Write-Host '$env:PGPASSWORD="TwojeHasloPostgres"'
  throw "Brak PGPASSWORD."
}

# 1) rola aplikacyjna
$createRoleSql = @'
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{0}') THEN
    CREATE ROLE "{0}" LOGIN PASSWORD '{1}';
  END IF;
END
$$;
'@ -f $AppUser, $AppPassword

& $psql -h $HostName -p $Port -U $AdminUser -d postgres -v ON_ERROR_STOP=1 -c $createRoleSql
if ($LASTEXITCODE -ne 0) { throw "Nie udało się utworzyć roli '$AppUser' (psql exit code: $LASTEXITCODE)." }

# 2) baza danych (CREATE DATABASE nie może być wykonywane wewnątrz DO)
$dbExists = & $psql -h $HostName -p $Port -U $AdminUser -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$DbName';"
if ($LASTEXITCODE -ne 0) { throw "Nie udało się sprawdzić istnienia bazy '$DbName' (psql exit code: $LASTEXITCODE)." }

if (($dbExists | Out-String).Trim() -ne "1") {
  $createDbCmd = "CREATE DATABASE ""$DbName"" OWNER ""$AppUser"";"
  & $psql -h $HostName -p $Port -U $AdminUser -d postgres -v ON_ERROR_STOP=1 -c $createDbCmd
  if ($LASTEXITCODE -ne 0) { throw "Nie udało się utworzyć bazy '$DbName' (psql exit code: $LASTEXITCODE)." }
}

Write-Host "OK: rola + baza utworzone (lub już istniały)."

