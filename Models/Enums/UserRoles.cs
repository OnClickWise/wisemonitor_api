namespace WiseMonitor.Api.Models.Enums
{
    public static class UserRoles
    {
        public const string SuperAdmin    = "SuperAdmin";
        public const string TenantAdmin   = "TenantAdmin";
        public const string Director      = "Director";
        public const string Manager       = "Manager";
        public const string Supervisor    = "Supervisor";
        public const string Employee      = "Employee";
        public const string Auditor       = "Auditor";
        public const string HR            = "HR";
        public const string Financial     = "Financial";
        public const string ProjectManager = "ProjectManager";
        public const string Client        = "Client";

        // Compatibilidade com roles legadas
        public const string Admin = TenantAdmin;
        public const string User  = Employee;

        public static readonly string[] All =
        [
            SuperAdmin, TenantAdmin, Director, Manager, Supervisor,
            Employee, Auditor, HR, Financial, ProjectManager, Client
        ];

        public static readonly string[] TenantLevel =
        [
            TenantAdmin, Director, Manager, Supervisor,
            Employee, Auditor, HR, Financial, ProjectManager, Client
        ];

        // Hierarquia numérica (menor = mais poder)
        public static int GetHierarchyLevel(string role) => role switch
        {
            SuperAdmin     => 0,
            TenantAdmin    => 1,
            Director       => 2,
            Manager        => 3,
            Supervisor     => 4,
            HR             => 5,
            Financial      => 5,
            Auditor        => 5,
            ProjectManager => 5,
            Employee       => 6,
            Client         => 7,
            _              => 99
        };

        public static string Normalize(string? role) => role?.Trim() switch
        {
            "admin" or "Admin" or "ADMIN" => TenantAdmin,
            "user"  or "User"  or "USER"  => Employee,
            var r when All.Contains(r)    => r!,
            _                              => Employee
        };
    }
}
