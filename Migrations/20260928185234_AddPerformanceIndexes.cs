using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        // CONCURRENTLY não bloqueia as escritas enquanto o índice é montado — AppFocusEvents
        // recebe eventos dos agents o tempo todo e a revisão antiga continua no ar durante
        // o deploy. CONCURRENTLY não roda dentro de transação, daí o suppressTransaction —
        // e por isso esta migration não tem mais nada além dos índices.
        // (Os índices estão declarados no AppDbContext; o snapshot já os conhece.)
        private static readonly (string Name, string Table, string Columns)[] Indexes =
        {
            ("IX_UserScreenshots_MonitoredUserId_DeviceId_CapturedAt", "UserScreenshots", "\"MonitoredUserId\", \"DeviceId\", \"CapturedAt\""),
            ("IX_UserScreenshots_OrganizationId_CapturedAt",           "UserScreenshots", "\"OrganizationId\", \"CapturedAt\""),
            ("IX_AppFocusEvents_OrganizationId_StartTime",             "AppFocusEvents",  "\"OrganizationId\", \"StartTime\""),
            ("IX_AppFocusEvents_UserId_StartTime",                     "AppFocusEvents",  "\"UserId\", \"StartTime\""),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, table, columns) in Indexes)
            {
                migrationBuilder.Sql(
                    $"CREATE INDEX CONCURRENTLY IF NOT EXISTS \"{name}\" ON \"{table}\" ({columns});",
                    suppressTransaction: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, _, _) in Indexes)
            {
                migrationBuilder.Sql(
                    $"DROP INDEX CONCURRENTLY IF EXISTS \"{name}\";",
                    suppressTransaction: true);
            }
        }
    }
}
