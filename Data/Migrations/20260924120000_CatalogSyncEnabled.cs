using Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(ApplicationDbStoreContext))]
    [Migration("20260924120000_CatalogSyncEnabled")]
    public partial class CatalogSyncEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.StoreRuntimeSettings', 'CatalogSyncEnabled') IS NULL
                BEGIN
                    ALTER TABLE [store].[StoreRuntimeSettings]
                    ADD [CatalogSyncEnabled] BIT NOT NULL
                        CONSTRAINT [DF_StoreRuntimeSettings_CatalogSyncEnabled] DEFAULT (1);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.StoreRuntimeSettings', 'CatalogSyncEnabled') IS NOT NULL
                BEGIN
                    DECLARE @df sysname;
                    SELECT @df = [name]
                    FROM sys.default_constraints
                    WHERE parent_object_id = OBJECT_ID('store.StoreRuntimeSettings')
                      AND parent_column_id = COLUMNPROPERTY(
                            OBJECT_ID('store.StoreRuntimeSettings'),
                            'CatalogSyncEnabled',
                            'ColumnId');

                    IF @df IS NOT NULL
                        EXEC(N'ALTER TABLE [store].[StoreRuntimeSettings] DROP CONSTRAINT [' + @df + N']');

                    ALTER TABLE [store].[StoreRuntimeSettings]
                    DROP COLUMN [CatalogSyncEnabled];
                END
                """);
        }
    }
}
