using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingEmail",
                table: "Organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CachedDeviceCount",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "CachedStorageGb",
                table: "Organizations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "CachedUserCount",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InternalNotes",
                table: "Organizations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDevices",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsers",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageLimitGb",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendUntil",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionType",
                table: "Organizations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "AgentVersion",
                table: "Devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReleaseNotes = table.Column<string>(type: "text", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ForceUpdate = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WindowsDownloadUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WindowsChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MacOsDownloadUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MacOsChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LinuxDownloadUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinuxChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedByAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConditionOperator = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ConditionValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NotificationChannelsJson = table.Column<string>(type: "text", nullable: false),
                    NotificationRecipientsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EventsJson = table.Column<string>(type: "text", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestSuccess = table.Column<bool>(type: "boolean", nullable: true),
                    LastTestMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformIntegrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllowPublicRegistration = table.Column<bool>(type: "boolean", nullable: false),
                    RequireEmailVerification = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultPlanForNewTenants = table.Column<string>(type: "text", nullable: false),
                    TrialDurationDays = table.Column<int>(type: "integer", nullable: false),
                    EnforceIpAllowlistForSuperAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedIpsJson = table.Column<string>(type: "text", nullable: false),
                    SessionTimeoutMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockoutDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    RequireMfaForSuperAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    GlobalMaxScreenshotRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    ScreenshotCompressionQuality = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceModeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaintenanceModeMessage = table.Column<string>(type: "text", nullable: true),
                    MaintenanceScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotifyOnNewTenantSignup = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnPaymentFailure = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnCriticalErrors = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationEmailsJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertHistories_AlertRules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "AlertRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentVersions_Channel",
                table: "AgentVersions",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_AgentVersions_Version",
                table: "AgentVersions",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertHistories_AlertRuleId",
                table: "AlertHistories",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertHistories_CreatedAt",
                table: "AlertHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AlertHistories_IsResolved",
                table: "AlertHistories",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_Trigger",
                table: "AlertRules",
                column: "Trigger");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformIntegrations_Type",
                table: "PlatformIntegrations",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentVersions");

            migrationBuilder.DropTable(
                name: "AlertHistories");

            migrationBuilder.DropTable(
                name: "PlatformIntegrations");

            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropColumn(
                name: "BillingEmail",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "CachedDeviceCount",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "CachedStorageGb",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "CachedUserCount",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "InternalNotes",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "MaxDevices",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "MaxUsers",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "StorageLimitGb",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SuspendUntil",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SuspensionType",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AgentVersion",
                table: "Devices");
        }
    }
}
