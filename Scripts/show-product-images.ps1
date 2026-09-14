# Read-only. For each published product, shows BigImage next to the joined picture rows,
# which is what the product detail gallery de-duplicates into its thumbnail strip.

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

Write-Host 'Published products with their picture rows (joined the way the stored proc does)' -ForegroundColor Cyan
Invoke-Query @"
SELECT p.Id,
       LEFT(p.NameEn, 34) AS NameEn,
       (SELECT COUNT(*) FROM store.Pictures pic WHERE pic.ProductId = p.Id) AS PictureRows,
       LEFT(p.BigImage, 78) AS BigImage
FROM store.GetProducts p
"@ | Format-Table -AutoSize -Wrap

Write-Host 'Picture URLs per published product' -ForegroundColor Cyan
Invoke-Query @"
SELECT pic.Id, LEFT(p.NameEn, 26) AS NameEn, LEFT(pic.PhotoUrl, 78) AS PhotoUrl
FROM store.Pictures pic
JOIN store.GetProducts p ON p.Id = pic.ProductId
ORDER BY p.NameEn, pic.Id
"@ | Format-Table -AutoSize -Wrap

# The gallery adds BigImage first, then every picture URL, skipping duplicates. If the
# only picture row repeats BigImage the strip collapses to one image and stays hidden.
Write-Host 'Distinct image URLs the gallery would end up with' -ForegroundColor Cyan
Invoke-Query @"
SELECT LEFT(p.NameEn, 34) AS NameEn,
       COUNT(DISTINCT u.Url) AS DistinctGalleryImages
FROM store.GetProducts p
CROSS APPLY (
    SELECT p.BigImage AS Url
    UNION
    SELECT pic.PhotoUrl FROM store.Pictures pic WHERE pic.ProductId = p.Id
) u
WHERE u.Url IS NOT NULL AND u.Url <> ''
GROUP BY p.NameEn
"@ | Format-Table -AutoSize

Write-Host 'Picture rows pointing at a product that no longer exists' -ForegroundColor Cyan
Invoke-Query @"
SELECT pic.Id, pic.ProductId, LEFT(pic.PhotoUrl, 60) AS PhotoUrl
FROM store.Pictures pic
LEFT JOIN store.GetProducts p ON p.Id = pic.ProductId
WHERE p.Id IS NULL
"@ | Format-Table -AutoSize
