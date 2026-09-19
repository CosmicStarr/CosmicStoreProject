# Verifies the admin catalog listing pages, searches and sorts correctly.
#
# The key risk this covers: paging a query without a stable ORDER BY lets SQL Server
# return rows in any order, so products can repeat on one page and never appear on
# another. Test 3 walks every page and asserts the ids form one complete, distinct set.

# The dev API uses a self-signed certificate.
add-type @"
using System.Net; using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPaging : ICertificatePolicy {
  public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPaging
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$api = 'https://localhost:5001/api'
$secretsPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\cosmicstore-api-dev\secrets.json'

$pass = 0
$fail = 0
function Check($name, $ok, $detail) {
    if ($ok) { Write-Host "  PASS  $name" -ForegroundColor Green; $script:pass++ }
    else { Write-Host "  FAIL  $name -- $detail" -ForegroundColor Red; $script:fail++ }
}

function Get-Pagination($response) {
    $header = $response.Headers['X-Pagination']
    if (-not $header) { return $null }
    if ($header -is [array]) { $header = $header[0] }
    return $header | ConvertFrom-Json
}

# --- Set up an admin session -------------------------------------------------
# Registration creates a plain user, so the Admin role is granted directly in the
# database. The account is removed again at the end of the run.
$secrets = Get-Content $secretsPath -Raw | ConvertFrom-Json
$connString = $secrets.'ConnectionStrings:DefaultConnection'
if ([string]::IsNullOrWhiteSpace($connString)) {
    throw 'ConnectionStrings:DefaultConnection is not set in user secrets.'
}

$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$email = "paging-admin-$stamp@cosmicstore.test"
$password = 'Test123$!aA'

Write-Host "`nSetting up admin session..." -ForegroundColor Cyan

try {
    Invoke-RestMethod -Uri "$api/Account/register" -Method POST -ContentType 'application/json' -Body (@{
        userName = 'paging-admin'; email = $email
        password = $password; confirmPassword = $password
    } | ConvertTo-Json) | Out-Null
} catch {
    # Registration can 500 when the confirmation email fails to relay to a .test
    # domain. The account is still created, so carry on to login.
    Write-Host "  (register returned an error; continuing to login)" -ForegroundColor DarkGray
}

function Invoke-Sql($sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection $connString
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        return $cmd.ExecuteScalar()
    } finally { $conn.Close() }
}

Invoke-Sql @"
IF NOT EXISTS (SELECT 1 FROM [store].[AspNetRoles] WHERE [Name] = 'Admin')
  INSERT INTO [store].[AspNetRoles] ([Id],[Name],[NormalizedName]) VALUES (NEWID(), 'Admin', 'ADMIN');
INSERT INTO [store].[AspNetUserRoles] ([UserId],[RoleId])
SELECT u.[Id], r.[Id] FROM [store].[AspNetUsers] u CROSS JOIN [store].[AspNetRoles] r
WHERE u.[Email] = '$email' AND r.[Name] = 'Admin';
SELECT 1;
"@ | Out-Null

$login = Invoke-RestMethod -Uri "$api/Account/login" -Method POST -ContentType 'application/json' `
    -Body (@{ email = $email; password = $password } | ConvertTo-Json)
$token = $login.token
$headers = @{ Authorization = "Bearer $token" }
Write-Host "  admin session ready" -ForegroundColor DarkGray

try {
    # --- Test 1: pagination header ------------------------------------------
    Write-Host "`nTest 1: pagination metadata" -ForegroundColor Cyan
    $r = Invoke-WebRequest -Uri "$api/EditProducts?pageNumber=1&pageSize=10" -Headers $headers -UseBasicParsing
    $page = Get-Pagination $r
    $items = $r.Content | ConvertFrom-Json

    Check 'X-Pagination header present' ($null -ne $page) 'header missing'
    Check 'CurrentPage is 1' ($page.CurrentPage -eq 1) "got $($page.CurrentPage)"
    Check 'ItemsPerPage is 10' ($page.ItemsPerPage -eq 10) "got $($page.ItemsPerPage)"
    Check 'TotalItems > 0' ($page.TotalItems -gt 0) "got $($page.TotalItems)"
    Check 'returns 10 items' ($items.Count -eq 10) "got $($items.Count)"
    Write-Host "  catalog has $($page.TotalItems) products across $($page.TotalPages) pages" -ForegroundColor DarkGray

    # --- Test 2: page size cap ----------------------------------------------
    Write-Host "`nTest 2: page size is capped at 50" -ForegroundColor Cyan
    $r = Invoke-WebRequest -Uri "$api/EditProducts?pageNumber=1&pageSize=500" -Headers $headers -UseBasicParsing
    $capped = Get-Pagination $r
    Check 'pageSize=500 clamped to 50' ($capped.ItemsPerPage -eq 50) "got $($capped.ItemsPerPage)"

    # --- Test 3: every product is reachable exactly once --------------------
    Write-Host "`nTest 3: walking all pages yields every product exactly once" -ForegroundColor Cyan
    $size = 25
    $first = Get-Pagination (Invoke-WebRequest -Uri "$api/EditProducts?pageNumber=1&pageSize=$size" -Headers $headers -UseBasicParsing)
    $seen = New-Object System.Collections.Generic.List[string]

    for ($p = 1; $p -le $first.TotalPages; $p++) {
        $body = Invoke-RestMethod -Uri "$api/EditProducts?pageNumber=$p&pageSize=$size" -Headers $headers
        foreach ($item in $body) { $seen.Add($item.id) }
    }

    $distinct = ($seen | Select-Object -Unique).Count
    Check "collected $($seen.Count) ids over $($first.TotalPages) pages" ($seen.Count -eq $first.TotalItems) `
        "expected $($first.TotalItems), got $($seen.Count)"
    Check 'no duplicate products across pages' ($distinct -eq $seen.Count) `
        "$($seen.Count - $distinct) duplicate(s) -- ordering is not stable"

    # --- Test 4: search -----------------------------------------------------
    Write-Host "`nTest 4: search narrows results" -ForegroundColor Cyan
    $sample = (Invoke-RestMethod -Uri "$api/EditProducts?pageNumber=1&pageSize=1" -Headers $headers)[0]
    $term = ($sample.nameEn -split ' ' | Where-Object { $_.Length -ge 4 } | Select-Object -First 1)
    Write-Host "  searching for '$term'" -ForegroundColor DarkGray

    $r = Invoke-WebRequest -Uri "$api/EditProducts?pageNumber=1&pageSize=50&Search=$([uri]::EscapeDataString($term))" -Headers $headers -UseBasicParsing
    $searchPage = Get-Pagination $r
    $searchItems = $r.Content | ConvertFrom-Json
    $allMatch = @($searchItems | Where-Object { $_.nameEn -notlike "*$term*" -and $_.sku -notlike "*$term*" }).Count -eq 0

    Check 'search returns fewer than the full catalog' ($searchPage.TotalItems -lt $page.TotalItems) `
        "search $($searchPage.TotalItems) vs total $($page.TotalItems)"
    Check 'search returns at least one hit' ($searchPage.TotalItems -ge 1) "got $($searchPage.TotalItems)"
    Check 'every result matches the term' $allMatch 'a result matched neither name nor SKU'

    $bogus = Invoke-WebRequest -Uri "$api/EditProducts?pageNumber=1&pageSize=10&Search=zzzznotathing" -Headers $headers -UseBasicParsing
    Check 'nonsense search returns 0 items' ((Get-Pagination $bogus).TotalItems -eq 0) 'expected no matches'

    # --- Test 5: sorting ----------------------------------------------------
    Write-Host "`nTest 5: sorting" -ForegroundColor Cyan
    $asc = Invoke-RestMethod -Uri "$api/EditProducts?pageNumber=1&pageSize=20&sort=priceAsc" -Headers $headers
    $desc = Invoke-RestMethod -Uri "$api/EditProducts?pageNumber=1&pageSize=20&sort=priceDesc" -Headers $headers

    $ascPrices = @($asc | ForEach-Object { [decimal]$_.sellPrice })
    $descPrices = @($desc | ForEach-Object { [decimal]$_.sellPrice })
    $ascSorted = ($ascPrices -join ',') -eq ((($ascPrices | Sort-Object)) -join ',')
    $descSorted = ($descPrices -join ',') -eq ((($descPrices | Sort-Object -Descending)) -join ',')

    Check 'priceAsc is ascending' $ascSorted "got $($ascPrices -join ', ')"
    Check 'priceDesc is descending' $descSorted "got $($descPrices -join ', ')"
    Check 'priceDesc first >= priceAsc first' ($descPrices[0] -ge $ascPrices[0]) 'sorts look identical'

    $byName = Invoke-RestMethod -Uri "$api/EditProducts?pageNumber=1&pageSize=20" -Headers $headers
    $names = @($byName | ForEach-Object { $_.nameEn })
    Check 'default sort is name A-Z' (($names -join '|') -eq (($names | Sort-Object) -join '|')) 'names not in order'

    # --- Test 6: category filter -------------------------------------------
    Write-Host "`nTest 6: category filter" -ForegroundColor Cyan
    $categories = Invoke-RestMethod -Uri "$api/EditProducts/categories" -Headers $headers
    Check 'categories endpoint returns values' ($categories.Count -gt 0) "got $($categories.Count)"

    if ($categories.Count -gt 0) {
        $cat = $categories[0]
        $r = Invoke-WebRequest -Uri "$api/EditProducts?pageNumber=1&pageSize=50&Category=$([uri]::EscapeDataString($cat))" -Headers $headers -UseBasicParsing
        $catItems = $r.Content | ConvertFrom-Json
        $catPage = Get-Pagination $r
        $allInCat = @($catItems | Where-Object { $_.category.categoryName -ne $cat }).Count -eq 0

        Write-Host "  filtering by '$cat' -> $($catPage.TotalItems) products" -ForegroundColor DarkGray
        Check 'category filter returns items' ($catPage.TotalItems -gt 0) 'no items returned'
        Check 'all items are in that category' $allInCat 'a result was from another category'
        Check 'category filter narrows the catalog' ($catPage.TotalItems -le $page.TotalItems) 'filter widened results'
    }

    # --- Test 7: out of range page -----------------------------------------
    Write-Host "`nTest 7: page beyond the last one" -ForegroundColor Cyan
    $far = Invoke-RestMethod -Uri "$api/EditProducts?pageNumber=9999&pageSize=10" -Headers $headers
    Check 'returns an empty page rather than an error' (@($far).Count -eq 0) "got $(@($far).Count) items"

    # --- Test 8: auth -------------------------------------------------------
    Write-Host "`nTest 8: endpoints require an admin" -ForegroundColor Cyan
    foreach ($path in @('EditProducts?pageNumber=1&pageSize=5', 'EditProducts/categories')) {
        $code = 0
        try { Invoke-WebRequest -Uri "$api/$path" -UseBasicParsing | Out-Null }
        catch { $code = [int]$_.Exception.Response.StatusCode }
        Check "$path rejects anonymous callers" ($code -eq 401) "got $code"
    }
}
finally {
    # --- Clean up the temporary admin account -------------------------------
    Invoke-Sql @"
DELETE FROM [store].[AspNetUserRoles] WHERE [UserId] IN (SELECT [Id] FROM [store].[AspNetUsers] WHERE [Email] = '$email');
DELETE FROM [store].[AspNetUsers] WHERE [Email] = '$email';
SELECT 1;
"@ | Out-Null
    Write-Host "`n  cleaned up temp admin account" -ForegroundColor DarkGray
}

Write-Host "`n================================" -ForegroundColor Cyan
Write-Host " Passed: $pass   Failed: $fail" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host "================================`n" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 }
