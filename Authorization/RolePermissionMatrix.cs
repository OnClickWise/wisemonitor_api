using System.Collections.Generic;
using WiseMonitor.Api.Models.Enums;

namespace WiseMonitor.Api.Authorization
{
    /// <summary>
    /// Define quais permissões cada papel possui por padrão.
    /// Permissões são cumulativas; deny explícito sobrescreve grants.
    /// </summary>
    public static class RolePermissionMatrix
    {
        private static readonly Dictionary<string, HashSet<string>> _matrix = new()
        {
            [UserRoles.SuperAdmin] = new HashSet<string>
            {
                // Super Admin tem TODAS as permissões
                Permissions.UsersCreate, Permissions.UsersEdit, Permissions.UsersDelete, Permissions.UsersView,
                Permissions.DepartmentsCreate, Permissions.DepartmentsEdit, Permissions.DepartmentsDelete,
                Permissions.TeamsCreate, Permissions.TeamsEdit, Permissions.TeamsDelete,
                Permissions.ProjectsCreate, Permissions.ProjectsEdit, Permissions.ProjectsDelete,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.ScreenshotsView, Permissions.ScreenshotsDelete,
                Permissions.ActivityView, Permissions.ActivityExport,
                Permissions.LivestreamView, Permissions.LivestreamStart, Permissions.LivestreamStop,
                Permissions.SystemMetricsView,
                Permissions.BillingView, Permissions.BillingManage,
                Permissions.AuditLogsView,
                Permissions.RolesManage, Permissions.PermissionsManage,
                Permissions.DelegationsCreate, Permissions.DelegationsView,
                Permissions.TenantsView, Permissions.TenantsCreate, Permissions.TenantsEdit,
                Permissions.TenantsDelete, Permissions.TenantsSuspend,
                Permissions.MetricsGlobal,
            },

            [UserRoles.TenantAdmin] = new HashSet<string>
            {
                Permissions.UsersCreate, Permissions.UsersEdit, Permissions.UsersDelete, Permissions.UsersView,
                Permissions.DepartmentsCreate, Permissions.DepartmentsEdit, Permissions.DepartmentsDelete,
                Permissions.TeamsCreate, Permissions.TeamsEdit, Permissions.TeamsDelete,
                Permissions.ProjectsCreate, Permissions.ProjectsEdit, Permissions.ProjectsDelete,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.ScreenshotsView, Permissions.ScreenshotsDelete,
                Permissions.ActivityView, Permissions.ActivityExport,
                Permissions.LivestreamView, Permissions.LivestreamStart, Permissions.LivestreamStop,
                Permissions.SystemMetricsView,
                Permissions.BillingView, Permissions.BillingManage,
                Permissions.AuditLogsView,
                Permissions.RolesManage,
                Permissions.DelegationsCreate, Permissions.DelegationsView,
            },

            [UserRoles.Director] = new HashSet<string>
            {
                Permissions.UsersView,
                Permissions.DepartmentsEdit,
                Permissions.TeamsCreate, Permissions.TeamsEdit, Permissions.TeamsDelete,
                Permissions.ProjectsCreate, Permissions.ProjectsEdit,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.ScreenshotsView,
                Permissions.ActivityView, Permissions.ActivityExport,
                Permissions.LivestreamView,
                Permissions.SystemMetricsView,
                Permissions.DelegationsCreate, Permissions.DelegationsView,
            },

            [UserRoles.Manager] = new HashSet<string>
            {
                Permissions.UsersView,
                Permissions.TeamsCreate, Permissions.TeamsEdit,
                Permissions.ProjectsCreate, Permissions.ProjectsEdit,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.ScreenshotsView,
                Permissions.ActivityView, Permissions.ActivityExport,
                Permissions.LivestreamView,
                Permissions.SystemMetricsView,
                Permissions.DelegationsCreate, Permissions.DelegationsView,
            },

            [UserRoles.Supervisor] = new HashSet<string>
            {
                Permissions.UsersView,
                Permissions.TeamsEdit,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.ScreenshotsView,
                Permissions.ActivityView, Permissions.ActivityExport,
                Permissions.LivestreamView,
                Permissions.SystemMetricsView,
                Permissions.DelegationsCreate, Permissions.DelegationsView,
            },

            [UserRoles.Employee] = new HashSet<string>
            {
                // Funcionário vê apenas seus próprios dados (filtragem feita na camada de serviço)
                Permissions.ReportsView,
                Permissions.ActivityView,
                Permissions.ScreenshotsView,
            },

            [UserRoles.Auditor] = new HashSet<string>
            {
                Permissions.UsersView,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.ScreenshotsView,
                Permissions.ActivityView, Permissions.ActivityExport,
                Permissions.SystemMetricsView,
                Permissions.AuditLogsView,
                Permissions.DelegationsView,
            },

            [UserRoles.HR] = new HashSet<string>
            {
                Permissions.UsersCreate, Permissions.UsersEdit, Permissions.UsersView,
                Permissions.DepartmentsEdit,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.ActivityView,
                Permissions.SystemMetricsView,
            },

            [UserRoles.Financial] = new HashSet<string>
            {
                Permissions.UsersView,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.BillingView, Permissions.BillingManage,
                Permissions.SystemMetricsView,
            },

            [UserRoles.ProjectManager] = new HashSet<string>
            {
                Permissions.UsersView,
                Permissions.TeamsCreate, Permissions.TeamsEdit,
                Permissions.ProjectsCreate, Permissions.ProjectsEdit, Permissions.ProjectsDelete,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.ActivityView, Permissions.ActivityExport,
                Permissions.SystemMetricsView,
            },

            [UserRoles.Client] = new HashSet<string>
            {
                Permissions.ReportsView,
            },
        };

        public static bool HasPermission(string role, string permission)
        {
            var normalizedRole = UserRoles.Normalize(role);
            return _matrix.TryGetValue(normalizedRole, out var perms) && perms.Contains(permission);
        }

        public static IEnumerable<string> GetPermissions(string role)
        {
            var normalizedRole = UserRoles.Normalize(role);
            return _matrix.TryGetValue(normalizedRole, out var perms) ? perms : [];
        }
    }
}
