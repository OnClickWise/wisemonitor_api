using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIconBase64ToAppFocusEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconBase64",
                table: "AppFocusEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconBase64",
                table: "AppFocusEvents");
        }
    }
}
