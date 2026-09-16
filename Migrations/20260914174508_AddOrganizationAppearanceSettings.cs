using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAppearanceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationAppearanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Theme = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PageBackgroundType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PageBackgroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PageGradientStart = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PageGradientEnd = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PageGradientDirection = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SidebarBackgroundType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarGradientStart = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarGradientEnd = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarGradientDirection = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SidebarForegroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarActiveBackground = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarActiveForeground = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarHoverBackground = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarHoverForeground = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SidebarProfileBackground = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CardBackgroundType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CardBackgroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CardGradientStart = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CardGradientEnd = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CardGradientDirection = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CardPrimaryTextColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CardSecondaryTextColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ButtonBackgroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ButtonForegroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ButtonHoverBackgroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ChartBackgroundType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ChartColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ChartGradientStart = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ChartGradientEnd = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ChartGradientDirection = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAppearanceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationAppearanceSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAppearanceSettings_OrganizationId_Theme",
                table: "OrganizationAppearanceSettings",
                columns: new[] { "OrganizationId", "Theme" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationAppearanceSettings");
        }
    }
}
