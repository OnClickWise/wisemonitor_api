using Microsoft.Extensions.Logging;
using WiseMonitor.Api.DTOs.Team;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class TeamService : ITeamService
    {
        private readonly ITeamRepository _teamRepo;
        private readonly IUserRepository _userRepo;
        private readonly ILogger<TeamService> _logger;

        public TeamService(
            ITeamRepository teamRepo,
            IUserRepository userRepo,
            ILogger<TeamService> logger)
        {
            _teamRepo = teamRepo;
            _userRepo = userRepo;
            _logger = logger;
        }

        public Task AddMemberAsync(Guid teamId, Guid userId, Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public async Task CreateAsync(CreateTeamDTO dto, Guid organizationId)
        {
            var manager = await _userRepo.GetByIdAsync(dto.ManagerId, organizationId);
            if (manager == null)
                throw new Exception("Manager inválido (Usuário não encontrado).");

            
            var normalizedRole = UserRoles.Normalize(manager.Role);
            var managerRoles = new[]
            {
                UserRoles.TenantAdmin, UserRoles.Director, UserRoles.Manager,
                UserRoles.Supervisor, UserRoles.ProjectManager, UserRoles.SuperAdmin
            };

            if (!managerRoles.Contains(normalizedRole))
            {
                _logger.LogWarning("Permissão insuficiente para responsável de equipe. Role: {Role}", manager.Role);
                throw new Exception($"Usuário não tem permissão para ser responsável. Cargo atual: {manager.Role}");
            }

            var team = new Team
            {
                OrganizationId = organizationId,
                Name = dto.Name,
                Description = dto.Description,
                ManagerId = dto.ManagerId,
                DefaultWorkScheduleId = dto.WorkScheduleId,
                Members = new List<TeamMember>()
            };

            // Garante que o administrador principal também esteja na lista de administradores
            var managerIds = dto.ManagerIds
                .Append(dto.ManagerId)
                .Distinct()
                .ToList();

            // Adiciona os administradores da equipe
            foreach (var managerId in managerIds)
            {
                var managerUser = await _userRepo.GetByIdAsync(managerId, organizationId);
                if (managerUser == null) continue;

                team.Members.Add(new TeamMember
                {
                    UserId = managerId,
                    IsManager = true
                });
            }

            // Adiciona os membros comuns (se houver)
            if (dto.MemberIds != null)
            {
                foreach (var userId in dto.MemberIds.Distinct())
                {
                    var user = await _userRepo.GetByIdAsync(userId, organizationId);
                    if (user == null) continue;

                    // Se esse usuário já foi adicionado como administrador,
                    // não adicionamos novamente por causa da chave TeamId + UserId
                    if (team.Members.Any(m => m.UserId == userId)) continue;

                    team.Members.Add(new TeamMember
                    {
                        UserId = userId,
                        IsManager = false
                    });
                }
            }

            await _teamRepo.CreateAsync(team);
        }
        public async Task DeleteAsync(Guid teamId, Guid organizationId)
        {
            var team = await _teamRepo.GetByIdAsync(teamId, organizationId);

            // 2. Se não encontrar, lança erro (o Controller vai transformar em 404 NotFound)
            if (team == null)
            {
                throw new KeyNotFoundException("Equipe não encontrada.");
            }

            // 3. Remove usando o repositório
            await _teamRepo.DeleteAsync(team);
        }

        public async Task<List<TeamResponseDTO>> GetAllAsync(Guid organizationId)
        {
            var teams = await _teamRepo.GetAllAsync(organizationId);

            return teams.Select(t => new TeamResponseDTO
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                CreatedAt = t.CreatedAt,
                ManagerId = t.ManagerId,
                ManagerName = t.Manager.FullName,
                WorkScheduleId = t.DefaultWorkScheduleId,
                WorkScheduleName = t.DefaultWorkSchedule?.Name,
                Managers = t.Members
                    .Where(m => m.IsManager)
                    .Select(m => new TeamMemberDTO
                    {
                        UserId = m.UserId,
                        FullName = m.User.FullName,
                        Role = m.User.Role
                    }).ToList(),
                Members = t.Members
                    .Where(m => !m.IsManager)
                    .Select(m => new TeamMemberDTO
                    {
                        UserId = m.UserId,
                        FullName = m.User.FullName,
                        Role = m.User.Role
                    }).ToList()
            }).ToList();
        }

        public Task<TeamResponseDTO?> GetByIdAsync(Guid teamId, Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public async Task RemoveMemberAsync(Guid teamId, Guid userId, Guid organizationId)
        {
            var team = await _teamRepo.GetByIdAsync(teamId, organizationId);
    
            if (team == null)
                throw new KeyNotFoundException("Equipe não encontrada.");

            // 2. Tenta encontrar o membro dentro da lista da equipe
            var memberToRemove = team.Members.FirstOrDefault(m => m.UserId == userId);

            if (memberToRemove == null)
            {
                throw new KeyNotFoundException("Este usuário não é membro desta equipe.");
            }

            // 3. Remove o objeto da lista
            team.Members.Remove(memberToRemove);

            // 4. Salva a alteração (O Entity Framework identificará a remoção)
            await _teamRepo.UpdateAsync(team);
        }

        public async Task UpdateAsync(Guid teamId, UpdateTeamDTO dto, Guid organizationId)
        {

            var team = await _teamRepo.GetByIdAsync(teamId, organizationId);
            
            if (team == null)
                throw new KeyNotFoundException("Equipe não encontrada.");

        
            team.Name = dto.Name; 
            team.Description = dto.Description;
            team.DefaultWorkScheduleId = dto.WorkScheduleId;


            if (dto.ManagerId.HasValue)
            {
                var mainManager = await _userRepo.GetByIdAsync(dto.ManagerId.Value, organizationId);
                if (mainManager == null)
                    throw new Exception("Administrador principal informado não encontrado.");

                team.ManagerId = dto.ManagerId.Value;
            }

            // Junta o administrador principal com os demais administradores selecionados
            var managerIds = dto.ManagerIds
                .Concat(dto.ManagerId.HasValue ? new[] { dto.ManagerId.Value } : Array.Empty<Guid>())
                .Distinct()
                .ToHashSet();

            // Primeiro, atualiza quem já está na equipe
            foreach (var member in team.Members)
            {
                member.IsManager = managerIds.Contains(member.UserId);
            }

            // Depois adiciona administradores que ainda não fazem parte da equipe
            foreach (var managerId in managerIds)
            {
                var existingMember = team.Members.FirstOrDefault(m => m.UserId == managerId);
                if (existingMember != null)
                {
                    existingMember.IsManager = true;
                    continue;
                }

                var managerUser = await _userRepo.GetByIdAsync(managerId, organizationId);
                if (managerUser == null) continue;

                team.Members.Add(new TeamMember
                {
                    TeamId = team.Id,
                    UserId = managerId,
                    IsManager = true
                });
            }

            // 4. LÓGICA DE MEMBROS (Adicionar/Remover)
            // Se a lista vier vazia, removemos todos os membros comuns (administradores nunca são removidos aqui)
            if (dto.MemberIds != null)
            {
                // A. REMOVER: Quem está no banco, mas NÃO está na nova lista do DTO (nunca remove administradores)
                var membersToRemove = team.Members
                    .Where(m => !m.IsManager && !dto.MemberIds.Contains(m.UserId))
                    .ToList(); // ToList é essencial aqui para não quebrar o loop

                foreach (var member in membersToRemove)
                {
                    team.Members.Remove(member);
                }

                // B. ADICIONAR: Quem está na nova lista, mas NÃO está no banco
                // Criamos um HashSet dos IDs atuais para busca rápida
                var currentMemberIds = team.Members.Select(m => m.UserId).ToHashSet();

                foreach (var newUserId in dto.MemberIds)
                {
                    // Se o usuário já está na equipe, pula
                    if (currentMemberIds.Contains(newUserId)) continue;

                    // Verifica se o usuário realmente existe na organização antes de adicionar
                    var userExists = await _userRepo.GetByIdAsync(newUserId, organizationId);
                    if (userExists != null)
                    {
                        team.Members.Add(new TeamMember
                        {
                            UserId = newUserId,
                            TeamId = team.Id, // O EF preenche isso, mas é bom garantir
                            IsManager = false
                        });
                    }
                }
            }



            // 5. Salva tudo
            await _teamRepo.UpdateAsync(team);
        }
    }
}
