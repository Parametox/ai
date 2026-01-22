param(
  [string]$ConnectionString = "Host=db.fntdzqdxfbddesaijpdr.supabase.co;Port=6543;Database=postgres;Username=postgres;Password=ytkYBbGznj4NqWcfRvY6;Pooling=true;Trust Server Certificate=true;"
)

$ErrorActionPreference = "Stop"

Write-Host "Applying EF Core migrations..."
$env:KANBANLITE_CONNECTION_STRING = $ConnectionString

dotnet tool run dotnet-ef database update --project src/DataAccess --startup-project src/DbMigrator

Write-Host "OK: migrations applied."

