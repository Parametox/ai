param(
  [string]$ConnectionString = "Host=localhost;Port=5432;Database=kanbanlite;Username=kanbanlite;Password=kanbanlite"
)

$ErrorActionPreference = "Stop"

Write-Host "Applying EF Core migrations..."
$env:KANBANLITE_CONNECTION_STRING = $ConnectionString

dotnet tool run dotnet-ef database update --project src/DataAccess --startup-project src/DbMigrator

Write-Host "OK: migrations applied."

