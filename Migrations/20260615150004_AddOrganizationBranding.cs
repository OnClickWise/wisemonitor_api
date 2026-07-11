using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandingAccentColor",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandingDisplayName",
                table: "Organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandingFontFamily",
                table: "Organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandingLogoUrl",
                table: "Organizations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandingPrimaryColor",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandingSecondaryColor",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Organizations"" DROP COLUMN IF EXISTS ""BrandingAccentColor"";
                ALTER TABLE ""Organizations"" DROP COLUMN IF EXISTS ""BrandingDisplayName"";
                ALTER TABLE ""Organizations"" DROP COLUMN IF EXISTS ""BrandingFontFamily"";
                ALTER TABLE ""Organizations"" DROP COLUMN IF EXISTS ""BrandingLogoUrl"";
                ALTER TABLE ""Organizations"" DROP COLUMN IF EXISTS ""BrandingPrimaryColor"";
                ALTER TABLE ""Organizations"" DROP COLUMN IF EXISTS ""BrandingSecondaryColor"";
            ");
        }
    }
}
