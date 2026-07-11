using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentRolesAndScheduleBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WorkScheduleRules: add CrossesMidnight + BlocksJson
            migrationBuilder.AddColumn<bool>(
                name: "CrossesMidnight",
                table: "WorkScheduleRules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BlocksJson",
                table: "WorkScheduleRules",
                type: "text",
                nullable: true);

            // WorkSchedules: add ScheduleCode
            migrationBuilder.AddColumn<string>(
                name: "ScheduleCode",
                table: "WorkSchedules",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Teams: add DefaultWorkScheduleId FK
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWorkScheduleId",
                table: "Teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DefaultWorkScheduleId",
                table: "Teams",
                column: "DefaultWorkScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_WorkSchedules_DefaultWorkScheduleId",
                table: "Teams",
                column: "DefaultWorkScheduleId",
                principalTable: "WorkSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // UserWorkSchedules: add Reason + NotifyUser
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "UserWorkSchedules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyUser",
                table: "UserWorkSchedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Normalize DepartmentMembers.MemberRole to uppercase
            migrationBuilder.Sql(@"
                UPDATE ""DepartmentMembers"" SET ""MemberRole"" = UPPER(""MemberRole"")
                WHERE ""MemberRole"" NOT IN ('DIRECTOR','SUPERVISOR','MEMBER','GUEST');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_WorkSchedules_DefaultWorkScheduleId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_DefaultWorkScheduleId",
                table: "Teams");

            migrationBuilder.DropColumn(name: "CrossesMidnight", table: "WorkScheduleRules");
            migrationBuilder.DropColumn(name: "BlocksJson", table: "WorkScheduleRules");
            migrationBuilder.DropColumn(name: "ScheduleCode", table: "WorkSchedules");
            migrationBuilder.DropColumn(name: "DefaultWorkScheduleId", table: "Teams");
            migrationBuilder.DropColumn(name: "Reason", table: "UserWorkSchedules");
            migrationBuilder.DropColumn(name: "NotifyUser", table: "UserWorkSchedules");
        }
    }
}
