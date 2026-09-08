# Smoke-tests the Stripe test keys directly against Stripe's API, mirroring what
# PaymentService does: create a PaymentIntent, then confirm it with a test card.
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\stripe-common.ps1"
$secret = Get-StripeTestSecret

$headers = @{ Authorization = "Bearer $secret" }

Write-Host "1. Creating PaymentIntent for `$24.98..."
$create = Invoke-RestMethod -Method Post -Uri 'https://api.stripe.com/v1/payment_intents' `
  -Headers $headers `
  -Body @{
    'amount'                  = 2498
    'currency'                = 'usd'
    'payment_method_types[0]' = 'card'
  }

Write-Host "   id=$($create.id) status=$($create.status) amount=$($create.amount)"

Write-Host "2. Confirming with test card pm_card_visa..."
$confirm = Invoke-RestMethod -Method Post -Uri "https://api.stripe.com/v1/payment_intents/$($create.id)/confirm" `
  -Headers $headers `
  -Body @{
    'payment_method' = 'pm_card_visa'
    'return_url'     = 'https://localhost:4200/checkout'
  }

Write-Host "   status=$($confirm.status) amount_received=$($confirm.amount_received)"

if ($confirm.status -eq 'succeeded') {
    Write-Host "PASS: Stripe test keys work and a card payment settled." -ForegroundColor Green
} else {
    Write-Host "FAIL: unexpected status '$($confirm.status)'." -ForegroundColor Red
    exit 1
}
