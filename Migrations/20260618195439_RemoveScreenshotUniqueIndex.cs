using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveScreenshotUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserScreenshots_OrganizationId_MonitoredUserId",
                table: "UserScreenshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserScreenshots_OrganizationId_MonitoredUserId",
                table: "UserScreenshots",
                columns: new[] { "OrganizationId", "MonitoredUserId" },
                unique: true);
        }
    }
}
