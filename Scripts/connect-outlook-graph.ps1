# One-time Outlook login for Microsoft Graph mail.
# Opens a browser. Sign in as CosmicStore46@outlook.com and wait for:
# Saved Microsoft Graph login
$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Set-Location (Join-Path $PSScriptRoot '..\CosmicStoreAPI')
dotnet run --no-launch-profile -- graph-auth
