using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextBillingAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionStartedAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationAddOns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceUsd = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAddOns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationAddOns_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAddOns_OrganizationId_Code",
                table: "OrganizationAddOns",
                columns: new[] { "OrganizationId", "Code" });

            // Planos antigos (Free | Basic | Pro) → matriz oficial.
            // Free ativo vira trial do Professional por 14 dias para não perder recursos de imediato.
            migrationBuilder.Sql(@"
                UPDATE ""Organizations""
                   SET ""Plan"" = 'Professional', ""Status"" = 'Trial',
                       ""TrialEndsAt"" = NOW() + INTERVAL '14 days'
                 WHERE lower(""Plan"") = 'free' AND ""Status"" = 'Active';

                UPDATE ""Organizations"" SET ""Plan"" = 'Starter'
                 WHERE lower(""Plan"") IN ('free', 'basic');

                UPDATE ""Organizations"" SET ""Plan"" = 'Professional'
                 WHERE lower(""Plan"") = 'pro';

                UPDATE ""Organizations"" SET ""Plan"" = initcap(""Plan"")
                 WHERE lower(""Plan"") IN ('starter', 'professional', 'business', 'enterprise');

                UPDATE ""Organizations"" SET ""Plan"" = 'Starter'
                 WHERE ""Plan"" NOT IN ('Starter', 'Professional', 'Business', 'Enterprise');

                UPDATE ""PlatformSettings"" SET ""DefaultPlanForNewTenants"" = 'Starter'
                 WHERE lower(""DefaultPlanForNewTenants"") IN ('free', 'basic');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationAddOns");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "NextBillingAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SubscriptionStartedAt",
                table: "Organizations");
        }
    }
}
