# Shared helpers for the Stripe test scripts.
#
# Secrets live in the dotnet user-secrets store, never in a tracked file. These helpers
# refuse to hand back a live key, because the test scripts confirm real card payments
# and would move real money against a live account.

$script:SecretsPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\cosmicstore-api-dev\secrets.json'

function Get-AppSecrets {
    if (-not (Test-Path $script:SecretsPath)) {
        throw "User secrets not found at $script:SecretsPath. Run 'dotnet user-secrets list' in CosmicStoreAPI."
    }
    return Get-Content $script:SecretsPath -Raw | ConvertFrom-Json
}

function Assert-TestMode($key, $name) {
    if ([string]::IsNullOrWhiteSpace($key)) {
        throw "$name is not set in user secrets."
    }
    if ($key -like 'sk_live_*' -or $key -like 'pk_live_*') {
        throw @"
REFUSING TO RUN: $name is a LIVE key.

These tests confirm real card payments. Against a live key they would create real
charges on real money. Switch the secret back to a test key first:

  cd CosmicStoreAPI
  dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
  dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
"@
    }
}

function Get-StripeTestSecret {
    $secrets = Get-AppSecrets
    $key = $secrets.'Stripe:SecretKey'
    Assert-TestMode $key 'Stripe:SecretKey'
    return $key
}

function Get-StripeWebhookSecret {
    $secrets = Get-AppSecrets
    $value = $secrets.'Stripe:WebhookSecret'
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'Stripe:WebhookSecret is not set in user secrets.' }
    return $value
}
