namespace WiseMonitor.Api.Models.Enums
{
    public static class Permissions
    {
        // Users
        public const string UsersCreate = "users.create";
        public const string UsersEdit   = "users.edit";
        public const string UsersDelete = "users.delete";
        public const string UsersView   = "users.view";

        // Departments
        public const string DepartmentsCreate = "departments.create";
        public const string DepartmentsEdit   = "departments.edit";
        public const string DepartmentsDelete = "departments.delete";

        // Teams
        public const string TeamsCreate = "teams.create";
        public const string TeamsEdit   = "teams.edit";
        public const string TeamsDelete = "teams.delete";

        // Projects
        public const string ProjectsCreate = "projects.create";
        public const string ProjectsEdit   = "projects.edit";
        public const string ProjectsDelete = "projects.delete";

        // Reports
        public const string ReportsView   = "reports.view";
        public const string ReportsExport = "reports.export";

        // Screenshots
        public const string ScreenshotsView   = "screenshots.view";
        public const string ScreenshotsDelete = "screenshots.delete";

        // Activity
        public const string ActivityView   = "activity.view";
        public const string ActivityExport = "activity.export";

        // Livestream
        public const string LivestreamView  = "livestream.view";
        public const string LivestreamStart = "livestream.start";
        public const string LivestreamStop  = "livestream.stop";

        // System Metrics
        public const string SystemMetricsView = "systemmetrics.view";

        // Billing
        public const string BillingView   = "billing.view";
        public const string BillingManage = "billing.manage";

        // Audit
        public const string AuditLogsView = "auditlogs.view";

        // Roles & Permissions
        public const string RolesManage       = "roles.manage";
        public const string PermissionsManage = "permissions.manage";

        // Delegations
        public const string DelegationsCreate = "delegations.create";
        public const string DelegationsView   = "delegations.view";

        // Super Admin (plataforma)
        public const string TenantsView   = "tenants.view";
        public const string TenantsCreate = "tenants.create";
        public const string TenantsEdit   = "tenants.edit";
        public const string TenantsDelete = "tenants.delete";
        public const string TenantsSuspend = "tenants.suspend";
        public const string MetricsGlobal  = "metrics.global";
    }
}
