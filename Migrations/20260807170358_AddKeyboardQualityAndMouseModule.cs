using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKeyboardQualityAndMouseModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackspaceCount",
                table: "KeyboardSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "CorrectionRate",
                table: "KeyboardSessions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "WordsPerMinute",
                table: "KeyboardSessions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "MouseSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Application = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LeftClicks = table.Column<int>(type: "integer", nullable: false),
                    RightClicks = table.Column<int>(type: "integer", nullable: false),
                    MiddleClicks = table.Column<int>(type: "integer", nullable: false),
                    ScrollCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MouseSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MouseSessions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MouseSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MouseSessions_OrganizationId_UserId_StartAt",
                table: "MouseSessions",
                columns: new[] { "OrganizationId", "UserId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MouseSessions_UserId",
                table: "MouseSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MouseSessions");

            migrationBuilder.DropColumn(
                name: "BackspaceCount",
                table: "KeyboardSessions");

            migrationBuilder.DropColumn(
                name: "CorrectionRate",
                table: "KeyboardSessions");

            migrationBuilder.DropColumn(
                name: "WordsPerMinute",
                table: "KeyboardSessions");
        }
    }
}
