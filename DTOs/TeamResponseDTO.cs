using System;
using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs.Team
{
    public class TeamResponseDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }

        public Guid ManagerId { get; set; }
        public string ManagerName { get; set; } = string.Empty;

        // Todos os administradores da equipe (inclui o ManagerId principal)
        public List<TeamMemberDTO> Managers { get; set; } = new();

        public List<TeamMemberDTO> Members { get; set; } = new();

        public Guid? WorkScheduleId { get; set; }

        public string? WorkScheduleName { get; set; }
    }

    /// <summary>Equipe que um gestor (gerente, coordenador, supervisor) gere.</summary>
    public class ManagedTeamDTO
    {
        public Guid TeamId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Responsável principal: só sai trocando o responsável na própria equipe.</summary>
        public bool IsResponsible { get; set; }
    }

    public class SetManagedTeamsDTO
    {
        public List<Guid> TeamIds { get; set; } = new();
    }

    public class TeamMemberDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        
        public string Role { get; set; } = string.Empty;
    }
}
