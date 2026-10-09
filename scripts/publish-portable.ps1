$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

dotnet publish src/Paperdown.App/Paperdown.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/portable/win-x64/Paperdown.App

dotnet publish src/Paperdown.Cli/Paperdown.Cli.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/portable/win-x64/Cli

Write-Host "Portable build ready under artifacts/portable/win-x64"
Pop-Location
