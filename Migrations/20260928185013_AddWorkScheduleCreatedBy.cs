using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkScheduleCreatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quem criou a jornada — supervisor só edita as que ele mesmo criou.
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "WorkSchedules",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "WorkSchedules");
        }
    }
}
