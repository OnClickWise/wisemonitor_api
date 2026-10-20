using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoSegmentStorageKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "VideoSegments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "VideoSegments");
        }
    }
}
