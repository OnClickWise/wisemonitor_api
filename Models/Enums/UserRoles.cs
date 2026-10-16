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

        /// <summary>
        /// Se quem tem <paramref name="callerRole"/> pode dar <paramref name="targetRole"/> a
        /// alguém (criar/editar). SuperAdmin nunca é atribuível por aqui — só existe via
        /// seed/painel de plataforma. TenantAdmin atribui qualquer papel de tenant; os demais
        /// só papéis estritamente abaixo do seu na hierarquia (senão RH criaria um TenantAdmin).
        /// </summary>
        public static bool CanAssign(string? callerRole, string? targetRole)
        {
            var caller = Normalize(callerRole);
            var target = Normalize(targetRole);

            if (!TenantLevel.Contains(target))
                return false;

            if (caller is SuperAdmin or TenantAdmin)
                return true;

            return GetHierarchyLevel(target) > GetHierarchyLevel(caller);
        }

        /// <summary>
        /// Se quem tem <paramref name="callerRole"/> pode alterar um usuário que hoje tem
        /// <paramref name="targetCurrentRole"/>. Mesma regra de <see cref="CanAssign"/>:
        /// sem isso, RH trocaria o e-mail de um TenantAdmin e tomaria a conta pelo "esqueci a senha".
        /// </summary>
        public static bool CanManage(string? callerRole, string? targetCurrentRole)
        {
            var caller = Normalize(callerRole);
            if (caller is SuperAdmin or TenantAdmin)
                return Normalize(targetCurrentRole) != SuperAdmin;

            return GetHierarchyLevel(Normalize(targetCurrentRole)) > GetHierarchyLevel(caller);
        }

        public static string Normalize(string? role) => role?.Trim() switch
        {
            "admin" or "Admin" or "ADMIN" => TenantAdmin,
            "user"  or "User"  or "USER"  => Employee,
            var r when All.Contains(r)    => r!,
            _                              => Employee
        };
    }
}
