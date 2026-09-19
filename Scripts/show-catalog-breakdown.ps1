# Read-only. Breaks the CJ staging catalog down by category so a sync run can be checked
# against the categories configured under CJDropshipping:Categories.

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

Write-Host 'Products per top-level category' -ForegroundColor Cyan
Invoke-Query @"
SELECT ISNULL(c.CategoryName, '(no category row)') AS CategoryName,
       COUNT(*) AS Products,
       COUNT(DISTINCT p.CategoryId) AS LeafCategories,
       CAST(MIN(p.SellPrice) AS decimal(18,2)) AS MinPrice,
       CAST(MAX(p.SellPrice) AS decimal(18,2)) AS MaxPrice
FROM dbo.FlatProducts p
LEFT JOIN dbo.FlatCategories c ON c.CategoryId = p.CategoryId
GROUP BY c.CategoryName
ORDER BY COUNT(*) DESC
"@ | Format-Table -AutoSize

Write-Host 'Data quality checks' -ForegroundColor Cyan
Invoke-Query @"
SELECT 'Total products'              AS Check_, COUNT(*) AS Value FROM dbo.FlatProducts
UNION ALL SELECT 'Distinct product ids',       COUNT(DISTINCT Id) FROM dbo.FlatProducts
UNION ALL SELECT 'Unknown Category',           COUNT(*) FROM dbo.FlatProducts p JOIN dbo.FlatCategories c ON c.CategoryId=p.CategoryId WHERE c.CategoryName='Unknown Category'
UNION ALL SELECT 'Missing image',              COUNT(*) FROM dbo.FlatProducts WHERE BigImage IS NULL OR BigImage=''
UNION ALL SELECT 'Zero or negative price',     COUNT(*) FROM dbo.FlatProducts WHERE SellPrice<=0
UNION ALL SELECT 'Missing name',               COUNT(*) FROM dbo.FlatProducts WHERE NameEn IS NULL OR NameEn='' OR NameEn='Unknown'
UNION ALL SELECT 'Orphaned category ref',      COUNT(*) FROM dbo.FlatProducts p LEFT JOIN dbo.FlatCategories c ON c.CategoryId=p.CategoryId WHERE c.CategoryId IS NULL
"@ | Format-Table -AutoSize

Write-Host 'Sample rows' -ForegroundColor Cyan
Invoke-Query @"
SELECT TOP 8 LEFT(p.NameEn, 52) AS NameEn, p.Sku, p.SellPrice, LEFT(c.FullPath, 58) AS FullPath
FROM dbo.FlatProducts p JOIN dbo.FlatCategories c ON c.CategoryId = p.CategoryId
ORDER BY NEWID()
"@ | Format-Table -AutoSize
