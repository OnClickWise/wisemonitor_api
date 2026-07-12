using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoredUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VideoData = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSegments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoSegments_OrganizationId_DeviceId_StartedAt",
                table: "VideoSegments",
                columns: new[] { "OrganizationId", "DeviceId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoSegments_OrganizationId_MonitoredUserId_StartedAt",
                table: "VideoSegments",
                columns: new[] { "OrganizationId", "MonitoredUserId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoSegments");
        }
    }
}
