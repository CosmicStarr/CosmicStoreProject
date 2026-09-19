# Read-only. Prints row counts for the CJ staging catalog and the storefront tables.
# Useful before and after Scripts\clear-cj-catalog.ps1.

$ErrorActionPreference = 'Stop'

$secretsPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\cosmicstore-api-dev\secrets.json'
$connString = (Get-Content $secretsPath -Raw | ConvertFrom-Json).'ConnectionStrings:DefaultConnection'
if ([string]::IsNullOrWhiteSpace($connString)) {
    throw 'ConnectionStrings:DefaultConnection is not set in user secrets.'
}

$conn = New-Object System.Data.SqlClient.SqlConnection $connString
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT 'dbo.FlatProducts (CJ catalog)' AS TableName, COUNT(*) AS Rows FROM dbo.FlatProducts
UNION ALL SELECT 'dbo.FlatCategories', COUNT(*) FROM dbo.FlatCategories
UNION ALL SELECT 'store.GetProducts (published)', COUNT(*) FROM store.GetProducts
UNION ALL SELECT 'store.Pictures', COUNT(*) FROM store.Pictures
UNION ALL SELECT 'store.ProductVariants', COUNT(*) FROM store.ProductVariants
UNION ALL SELECT 'store.WishlistItems', COUNT(*) FROM store.WishlistItems
UNION ALL SELECT 'store.Orders', COUNT(*) FROM store.Orders
UNION ALL SELECT 'store.OrderItems', COUNT(*) FROM store.OrderItems
"@
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) { "{0,-32} {1,6}" -f $reader['TableName'], $reader['Rows'] }
}
finally { $conn.Close() }
