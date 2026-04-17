$checks = @(
  @{ Name = "dotnet"; Command = "dotnet --version" },
  @{ Name = "node"; Command = "node --version" },
  @{ Name = "npm"; Command = "npm --version" },
  @{ Name = "ng"; Command = "ng version" },
  @{ Name = "docker"; Command = "docker --version" }
)

$missing = @()

foreach ($check in $checks) {
  if (Get-Command $check.Name -ErrorAction SilentlyContinue) {
    Write-Host "[OK] $($check.Name)"
  } else {
    Write-Host "[MISSING] $($check.Name)"
    $missing += $check.Name
  }
}

if ($missing.Count -gt 0) {
  Write-Host ""
  Write-Host "Faltan herramientas: $($missing -join ", ")"
  exit 1
}

Write-Host "Todos los prerequisitos estan disponibles."
