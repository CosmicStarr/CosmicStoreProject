# Empties the CJ staging catalog (dbo.FlatProducts + dbo.FlatCategories) so the next
# CJProductSyncWorker run repopulates it from scratch.
#
# This only touches the staging catalog. Published storefront rows in store.GetProducts
# are left alone: there is no foreign key between the two, so a published product keeps
# working after its source FlatProduct is gone.
#
# Rows are written to CSV first. The catalog is rebuildable from CJ, but re-fetching it
# costs API points, so the export makes a mistake cheap to undo.

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

$backupDir = Join-Path $PSScriptRoot '..\db-backups'
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

foreach ($table in 'FlatProducts', 'FlatCategories') {
    $rows = Invoke-Query "SELECT * FROM dbo.$table"
    $target = Join-Path $backupDir "$table-$stamp.csv"
    $rows | Export-Csv -Path $target -NoTypeInformation -Encoding UTF8
    Write-Host ("Backed up {0,-15} {1,6} rows -> {2}" -f $table, $rows.Rows.Count, (Split-Path $target -Leaf))
}

# FlatProducts.CategoryId has a restricting foreign key onto FlatCategories, so the
# products have to go first. Both statements share a transaction to avoid leaving the
# catalog half-deleted if the second one fails.
# SET NOCOUNT ON matters here: without it each DELETE emits a rowcount message that
# DataTable.Load treats as an empty result set, so the SELECT below is never reached.
$deleted = Invoke-Query @"
SET NOCOUNT ON;
BEGIN TRANSACTION;
    DECLARE @products INT, @categories INT;
    DELETE FROM dbo.FlatProducts;
    SET @products = @@ROWCOUNT;
    DELETE FROM dbo.FlatCategories;
    SET @categories = @@ROWCOUNT;
COMMIT TRANSACTION;
SELECT @products AS ProductsDeleted, @categories AS CategoriesDeleted;
"@

Write-Host ''
Write-Host ("Deleted {0} products and {1} categories." -f $deleted.Rows[0].ProductsDeleted, $deleted.Rows[0].CategoriesDeleted) -ForegroundColor Yellow

$after = Invoke-Query @"
SELECT 'dbo.FlatProducts' AS TableName, COUNT(*) AS RemainingRows FROM dbo.FlatProducts
UNION ALL SELECT 'dbo.FlatCategories', COUNT(*) FROM dbo.FlatCategories
UNION ALL SELECT 'store.GetProducts (untouched)', COUNT(*) FROM store.GetProducts
UNION ALL SELECT 'store.Orders (untouched)', COUNT(*) FROM store.Orders
"@
$after | Format-Table -AutoSize
