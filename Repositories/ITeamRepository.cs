using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface ITeamRepository
    {
        Task CreateAsync(Team team);
        Task<Team?> GetByIdAsync(Guid id, Guid organizationId);
        Task<List<Team>> GetAllAsync(Guid organizationId);

        /// <summary>Retorna a equipe (se houver) à qual o usuário pertence como membro comum ou administrador.</summary>
        Task<Team?> GetByUserIdAsync(Guid userId, Guid organizationId);

        /// <summary>
        /// Ids de todos os usuários (administradores e membros comuns) das equipes
        /// em que o usuário informado atua como administrador (Team.ManagerId ou
        /// TeamMember.IsManager), filtrados pela hierarquia — só quem está abaixo
        /// dele, mais ele mesmo. Usado para restringir quem não é administrador.
        /// </summary>
        Task<List<Guid>> GetManagedMemberIdsAsync(Guid managerUserId, Guid organizationId);

        /// <summary>
        /// Define as equipes que o usuário gere: vira administrador das informadas e
        /// deixa as demais. Equipes em que ele é o responsável principal continuam.
        /// </summary>
        Task SetManagedTeamsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> teamIds,
            Guid organizationId);

        Task UpdateAsync(Team team);
        Task DeleteAsync(Team team);
    }
}
