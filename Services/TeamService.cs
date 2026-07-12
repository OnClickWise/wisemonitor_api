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

            // Adiciona os membros (se houver)
            if (dto.MemberIds != null)
            {
                foreach (var userId in dto.MemberIds.Distinct())
                {
                    var user = await _userRepo.GetByIdAsync(userId, organizationId);
                    if (user == null) continue;

                    team.Members.Add(new TeamMember
                    {
                        UserId = userId
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
                Members = t.Members.Select(m => new TeamMemberDTO
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


            if (dto.ManagerId.HasValue && dto.ManagerId.Value != team.ManagerId)
            {
                var newManager = await _userRepo.GetByIdAsync(dto.ManagerId.Value, organizationId);
                if (newManager == null) 
                    throw new Exception("Novo gerente informado não encontrado.");
                    
                team.ManagerId = dto.ManagerId.Value;
            }

            // 4. LÓGICA DE MEMBROS (Adicionar/Remover)
            // Se a lista vier vazia, removemos todos (exceto se sua regra de negócio impedir)
            if (dto.MemberIds != null)
            {
                // A. REMOVER: Quem está no banco, mas NÃO está na nova lista do DTO
                var membersToRemove = team.Members
                    .Where(m => !dto.MemberIds.Contains(m.UserId))
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
                            TeamId = team.Id // O EF preenche isso, mas é bom garantir
                        });
                    }
                }
            }

            

            // 5. Salva tudo
            await _teamRepo.UpdateAsync(team);
        }
    }
}
