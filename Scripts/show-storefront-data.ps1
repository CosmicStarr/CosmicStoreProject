# Read-only. Shows what the storefront actually has to render: published products,
# their feature flags, and how many picture rows each one owns.

$ErrorActionPreference = 'Stop'

$secretsPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\cosmicstore-api-dev\secrets.json'
$connString = (Get-Content $secretsPath -Raw | ConvertFrom-Json).'ConnectionStrings:DefaultConnection'
if ([string]::IsNullOrWhiteSpace($connString)) {
    throw 'ConnectionStrings:DefaultConnection is not set in user secrets.'
}

function Invoke-Query($sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection $connString
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $reader = $cmd.ExecuteReader()
        $table = New-Object System.Data.DataTable
        $table.Load($reader)
        return $table
    }
    finally { $conn.Close() }
}

Write-Host 'Published products, feature flags and picture counts' -ForegroundColor Cyan
Invoke-Query @"
SELECT LEFT(p.NameEn, 40) AS NameEn,
       p.IsFeatured, p.IsNewArrival, p.IsTopSelling,
       p.StockQuantity,
       CASE WHEN p.BigImage IS NULL OR p.BigImage = '' THEN 'no' ELSE 'yes' END AS HasBigImage,
       (SELECT COUNT(*) FROM store.Pictures pic WHERE pic.ProductsId = p.Id) AS PictureRows
FROM store.GetProducts p
ORDER BY p.NameEn
"@ | Format-Table -AutoSize

Write-Host 'Storefront totals' -ForegroundColor Cyan
Invoke-Query @"
SELECT 'Published products' AS Check_, COUNT(*) AS Value FROM store.GetProducts
UNION ALL SELECT 'IsFeatured = 1',     COUNT(*) FROM store.GetProducts WHERE IsFeatured   = 1
UNION ALL SELECT 'IsNewArrival = 1',   COUNT(*) FROM store.GetProducts WHERE IsNewArrival = 1
UNION ALL SELECT 'IsTopSelling = 1',   COUNT(*) FROM store.GetProducts WHERE IsTopSelling = 1
UNION ALL SELECT 'Picture rows total', COUNT(*) FROM store.Pictures
UNION ALL SELECT 'Pictures orphaned',  COUNT(*) FROM store.Pictures pic LEFT JOIN store.GetProducts p ON p.Id = pic.ProductsId WHERE p.Id IS NULL
"@ | Format-Table -AutoSize

Write-Host 'store.Pictures actual columns' -ForegroundColor Cyan
Invoke-Query @"
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='store' AND TABLE_NAME='Pictures'
ORDER BY ORDINAL_POSITION
"@ | Format-Table -AutoSize

# The stored procedures join on pi.ProductId, but EF's foreign key is ProductsId.
# If only one of the two is populated the join silently yields no images.
Write-Host 'Picture rows: which product column is actually populated?' -ForegroundColor Cyan
Invoke-Query @"
SELECT pic.Id,
       ISNULL(pic.ProductId, '(null)')  AS ProductId_procJoinsThis,
       ISNULL(pic.ProductsId, '(null)') AS ProductsId_efForeignKey,
       LEFT(pic.PhotoUrl, 60) AS PhotoUrl
FROM store.Pictures pic
"@ | Format-Table -AutoSize

Write-Host 'Does the stored-proc join actually match anything?' -ForegroundColor Cyan
Invoke-Query @"
SELECT 'Rows where ProductId matches a product'  AS Check_,
       COUNT(*) AS Value
FROM store.Pictures pic JOIN store.GetProducts p ON p.Id = pic.ProductId
UNION ALL
SELECT 'Rows where ProductsId matches a product',
       COUNT(*)
FROM store.Pictures pic JOIN store.GetProducts p ON p.Id = pic.ProductsId
"@ | Format-Table -AutoSize
