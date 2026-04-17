param(
  [string]$Server = "localhost,14333",
  [string]$Database = "SolicitudesTechGovMvp",
  [string]$User = "sa",
  [string]$Password = "StrongP@ssw0rd123"
)

$schemaPath = Join-Path $PSScriptRoot "..\database\sql\001_schema_mvp.sql"

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
  Write-Error "sqlcmd no esta instalado. Instala SQL Server Command Line Utilities."
  exit 1
}

sqlcmd -I -S $Server -U $User -P $Password -Q "IF DB_ID('$Database') IS NULL CREATE DATABASE [$Database];"
sqlcmd -I -S $Server -U $User -P $Password -d $Database -i $schemaPath

Write-Host "Esquema MVP aplicado en $Database"
