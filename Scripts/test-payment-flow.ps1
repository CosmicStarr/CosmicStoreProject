# End-to-end test of the Stripe checkout path against a running API on https://localhost:5001.
# Registers a throwaway user, builds a cart, creates a PaymentIntent through our own endpoint,
# settles it with a Stripe test card, then places the order and checks the guard rails.
$ErrorActionPreference = 'Stop'

# The dev API uses a self-signed certificate.
add-type @"
using System.Net; using System.Security.Cryptography.X509Certificates;
public class TrustAllCerts : ICertificatePolicy {
  public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCerts
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$api = 'https://localhost:5001/api'
. "$PSScriptRoot\stripe-common.ps1"
$stripeHeaders = @{ Authorization = "Bearer $(Get-StripeTestSecret)" }
$webhookSecret = Get-StripeWebhookSecret

$pass = 0
$fail = 0
function Check($name, $condition, $detail) {
    if ($condition) { Write-Host "  PASS  $name" -ForegroundColor Green; $script:pass++ }
    else { Write-Host "  FAIL  $name -- $detail" -ForegroundColor Red; $script:fail++ }
}

# Windows PowerShell doesn't reliably populate ErrorDetails, so read the response stream directly.
function Get-ErrorInfo($errorRecord) {
    $status = 0
    $body = ''
    $response = $errorRecord.Exception.Response
    if ($response) {
        $status = [int]$response.StatusCode
        try {
            $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
            $body = $reader.ReadToEnd()
            $reader.Close()
        } catch { }
    }
    if (-not $body -and $errorRecord.ErrorDetails) { $body = $errorRecord.ErrorDetails.Message }

    $message = $body
    try { $message = ($body | ConvertFrom-Json).message } catch { }
    return [pscustomobject]@{ Status = $status; Message = $message }
}

Write-Host "`n=== Setup ==="
$email = "stripetest_$(Get-Random -Maximum 99999)@example.com"
$password = 'Test1234!'

# Registration creates the user and then tries to email a confirmation link. Mailjet refuses
# throwaway domains, which surfaces as a 500 even though the account exists, so tolerate it.
try {
    Invoke-RestMethod -Method Post -Uri "$api/Account/register" -ContentType 'application/json' -Body (@{
        userName = 'stripetester'; email = $email; password = $password; confirmPassword = $password
    } | ConvertTo-Json) | Out-Null
} catch {
    Write-Host "  (registration returned $((Get-ErrorInfo $_).Status); continuing since the account is created)"
}

$login = Invoke-RestMethod -Method Post -Uri "$api/Account/login" -ContentType 'application/json' -Body (@{
    email = $email; password = $password
} | ConvertTo-Json)

$auth = @{ Authorization = "Bearer $($login.token)" }
Write-Host "Registered and logged in as $email"

# Pick a real published product so cart pricing resolves against the database.
$products = Invoke-RestMethod -Method Get -Uri "$api/Products/joined-products?pageSize=1&pageNumber=1"
$product = if ($products.data) { $products.data[0] } else { $products[0] }
if (-not $product) { Write-Host "No published products; cannot test." -ForegroundColor Red; exit 1 }
Write-Host "Using product '$($product.nameEn)' sku=$($product.sku) price=$($product.sellPrice)"

$cartId = [guid]::NewGuid().ToString()
$cart = Invoke-RestMethod -Method Post -Uri "$api/Cart/items" -Headers $auth -ContentType 'application/json' -Body (@{
    cartId = $cartId; productId = $product.id; quantity = 2
} | ConvertTo-Json)
Write-Host "Cart $cartId has $($cart.shoppingCartItems.Count) line(s)"

$shipping = 4.99
$expectedTotal = ($product.sellPrice * 2) + $shipping

Write-Host "`n=== Test 1: PaymentIntent is created with a server-computed amount ==="
$intent = Invoke-RestMethod -Method Post -Uri "$api/Payment/$cartId" -Headers $auth -ContentType 'application/json' -Body (@{
    logisticName = 'CJPacket Ordinary'; shippingCost = $shipping
} | ConvertTo-Json)

Write-Host "  intent=$($intent.paymentIntentId) amount=$($intent.amount) subtotal=$($intent.subtotal) shipping=$($intent.shippingCost)"
Check 'amount = subtotal + shipping' ([math]::Abs($intent.amount - $expectedTotal) -lt 0.01) "got $($intent.amount), expected $expectedTotal"
Check 'client secret returned' (-not [string]::IsNullOrWhiteSpace($intent.clientSecret)) 'empty'

Write-Host "`n=== Test 2: Unpaid intent cannot place an order ==="
$orderBody = @{
    fullName = 'Test Buyer'; streetAddress = '123 Main St'; city = 'Austin'
    provinceOrState = 'TX'; countryCode = 'US'
    stripePaymentMethodId = $intent.paymentIntentId
    logisticName = 'CJPacket Ordinary'; shippingCost = $shipping
    items = @(@{ sku = $product.sku; amount = 2; name = $product.nameEn; price = $product.sellPrice })
} | ConvertTo-Json
try {
    Invoke-RestMethod -Method Post -Uri "$api/Orders/checkout" -Headers $auth -ContentType 'application/json' -Body $orderBody | Out-Null
    Check 'unpaid order rejected' $false 'checkout succeeded without payment'
} catch {
    $err = Get-ErrorInfo $_
    Check 'unpaid order rejected' ($err.Message -like '*has not completed*') "message was: $($err.Message)"
    Write-Host "     server said: $($err.Message)"
}

Write-Host "`n=== Test 3: Pay the intent with a Stripe test card ==="
$confirmed = Invoke-RestMethod -Method Post -Headers $stripeHeaders `
  -Uri "https://api.stripe.com/v1/payment_intents/$($intent.paymentIntentId)/confirm" `
  -Body @{ 'payment_method' = 'pm_card_visa'; 'return_url' = 'https://localhost:4200/checkout' }
Check 'card charge succeeded' ($confirmed.status -eq 'succeeded') "status $($confirmed.status)"
Check 'stripe charged the exact total' ($confirmed.amount_received -eq [long][math]::Round($expectedTotal * 100)) "got $($confirmed.amount_received)"

Write-Host "`n=== Test 4: Paid intent places the order ==="
$order = Invoke-RestMethod -Method Post -Uri "$api/Orders/checkout" -Headers $auth -ContentType 'application/json' -Body $orderBody
Write-Host "  order=$($order.orderId) status=$($order.status) total=$($order.total)"
Check 'order created' (-not [string]::IsNullOrWhiteSpace($order.orderId)) 'no order id'
Check 'order total matches charge' ([math]::Abs($order.total - $expectedTotal) -lt 0.01) "got $($order.total)"
Check 'shipping recorded' ([math]::Abs($order.shippingCost - $shipping) -lt 0.01) "got $($order.shippingCost)"

Write-Host "`n=== Test 5: The same payment cannot be reused ==="
try {
    Invoke-RestMethod -Method Post -Uri "$api/Orders/checkout" -Headers $auth -ContentType 'application/json' -Body $orderBody | Out-Null
    Check 'duplicate order rejected' $false 'second checkout succeeded'
} catch {
    $err = Get-ErrorInfo $_
    Check 'duplicate order rejected' ($err.Message -like '*already been placed*') "message was: $($err.Message)"
    Write-Host "     server said: $($err.Message)"
}

Write-Host "`n=== Test 6: A tampered total is rejected ==="
$cartId2 = [guid]::NewGuid().ToString()
Invoke-RestMethod -Method Post -Uri "$api/Cart/items" -Headers $auth -ContentType 'application/json' -Body (@{
    cartId = $cartId2; productId = $product.id; quantity = 1
} | ConvertTo-Json) | Out-Null

$intent2 = Invoke-RestMethod -Method Post -Uri "$api/Payment/$cartId2" -Headers $auth -ContentType 'application/json' -Body (@{
    logisticName = 'CJPacket Ordinary'; shippingCost = 0
} | ConvertTo-Json)
Invoke-RestMethod -Method Post -Headers $stripeHeaders `
  -Uri "https://api.stripe.com/v1/payment_intents/$($intent2.paymentIntentId)/confirm" `
  -Body @{ 'payment_method' = 'pm_card_visa'; 'return_url' = 'https://localhost:4200/checkout' } | Out-Null

# Pay for one unit, then try to claim ten.
$greedy = @{
    fullName = 'Test Buyer'; streetAddress = '123 Main St'; city = 'Austin'
    provinceOrState = 'TX'; countryCode = 'US'
    stripePaymentMethodId = $intent2.paymentIntentId
    logisticName = 'CJPacket Ordinary'; shippingCost = 0
    items = @(@{ sku = $product.sku; amount = 10; name = $product.nameEn; price = $product.sellPrice })
} | ConvertTo-Json
try {
    Invoke-RestMethod -Method Post -Uri "$api/Orders/checkout" -Headers $auth -ContentType 'application/json' -Body $greedy | Out-Null
    Check 'underpaid order rejected' $false 'checkout accepted an underpayment'
} catch {
    $err = Get-ErrorInfo $_
    Check 'underpaid order rejected' ($err.Message -like '*does not match*') "message was: $($err.Message)"
    Write-Host "     server said: $($err.Message)"
}

Write-Host "`n=== Test 7: Webhook rejects a bad signature ==="
try {
    Invoke-RestMethod -Method Post -Uri "$api/Payment/webhook" -ContentType 'application/json' `
      -Headers @{ 'Stripe-Signature' = 't=1,v1=deadbeef' } -Body '{"id":"evt_1","type":"payment_intent.succeeded"}' | Out-Null
    Check 'forged webhook rejected' $false 'accepted a forged signature'
} catch {
    $err = Get-ErrorInfo $_
    Check 'forged webhook rejected' ($err.Status -eq 400) "status $($err.Status)"
}

Write-Host "`n=== Test 8: Webhook accepts a correctly signed event ==="
# Windows PowerShell's -UFormat %s is local-time based, so use an explicit UTC epoch.
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

$payload = @{
    id = 'evt_test_webhook'; object = 'event'; type = 'payment_intent.succeeded'
    api_version = '2024-06-20'; created = $timestamp
    data = @{ object = @{ id = $intent.paymentIntentId; object = 'payment_intent'; status = 'succeeded' } }
} | ConvertTo-Json -Depth 10 -Compress

$signedPayload = "$timestamp.$payload"
$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [Text.Encoding]::UTF8.GetBytes($webhookSecret)
$sig = ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($signedPayload)) | ForEach-Object { $_.ToString('x2') }) -join ''

try {
    Invoke-RestMethod -Method Post -Uri "$api/Payment/webhook" -ContentType 'application/json' `
      -Headers @{ 'Stripe-Signature' = "t=$timestamp,v1=$sig" } -Body $payload | Out-Null
    Check 'signed webhook accepted' $true ''
} catch {
    $err = Get-ErrorInfo $_
    Check 'signed webhook accepted' $false "status $($err.Status) $($err.Message)"
}

Write-Host "`n=== Test 9: Order reflects the payment status ==="
$stored = Invoke-RestMethod -Method Get -Uri "$api/Orders/$($order.orderId)" -Headers $auth
Write-Host "  order status=$($stored.status) paymentStatus=$($stored.paymentStatus) tracking=$($stored.trackingNumber)"
Check 'order marked paid' ($stored.paymentStatus -eq 'PaymentRecevied') "got '$($stored.paymentStatus)'"

Write-Host "`n====================="
Write-Host "$pass passed, $fail failed" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -gt 0) { exit 1 }
