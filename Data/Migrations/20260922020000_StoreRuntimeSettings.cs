using Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(ApplicationDbStoreContext))]
    [Migration("20260922020000_StoreRuntimeSettings")]
    public partial class StoreRuntimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID('store.StoreRuntimeSettings', 'U') IS NULL
                BEGIN
                    CREATE TABLE [store].[StoreRuntimeSettings] (
                        [Id] INT NOT NULL CONSTRAINT [PK_StoreRuntimeSettings] PRIMARY KEY,
                        [DefaultMarkup] DECIMAL(18,2) NOT NULL,
                        [CatalogSyncHours] INT NOT NULL,
                        [UpdatedAt] DATETIME2 NOT NULL
                    );

                    INSERT INTO [store].[StoreRuntimeSettings] ([Id], [DefaultMarkup], [CatalogSyncHours], [UpdatedAt])
                    VALUES (1, 2.0, 6, SYSUTCDATETIME());
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID('store.StoreRuntimeSettings', 'U') IS NOT NULL
                    DROP TABLE [store].[StoreRuntimeSettings];
                """);
        }
    }
}
