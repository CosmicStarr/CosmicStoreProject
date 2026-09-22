using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Snapshot-only sync for schema already applied by earlier SQL migrations
    /// (AcceptedTerms, gift registry, NewArrivalMarkedAt, StoreRuntimeSettings, etc.).
    /// </summary>
    public partial class StoreRuntimeSettingsModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
