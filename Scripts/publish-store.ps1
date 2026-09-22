# Builds the Angular storefront and publishes CosmicStoreAPI with that UI in wwwroot.
# Do not put live secrets in the repo. Set them on the host before the first live sale:
#   ASPNETCORE_ENVIRONMENT=Production
#   Store__PublicOrigin=https://www.your-domain.com
#   ConnectionStrings__DefaultConnection=...
#   ConnectionStrings__RedisConnection=...
#   JWT__SecretKey=  (64+ characters)
#   Stripe__SecretKey=sk_live_...
#   Stripe__PublishableKey=pk_live_...
#   Stripe__WebhookSecret=whsec_...
#   Graph__AuthMode=ClientCredentials
#   Graph__TenantId=  Graph__ClientId=  Graph__ClientSecret=
#   ReturnPath__SenderEmail=
#   CJDropshipping__ApiKey=
# Register the Stripe webhook at: {Store__PublicOrigin}/api/Payment/webhook
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location (Join-Path $root 'CosmicStock')
npm run build:prod
Set-Location $root
$out = Join-Path $root 'artifacts\store'
dotnet publish .\CosmicStoreAPI\CosmicStoreAPI.csproj -c Release -o $out
Write-Host "Published to $out"
Write-Host "Stripe webhook: {Store__PublicOrigin}/api/Payment/webhook"
