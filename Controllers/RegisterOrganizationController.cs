using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Repositories;

[ApiController]
[Route("api/[controller]")]
public class RegisterOrganizationController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationRepository _organizationRepository;

    public RegisterOrganizationController(
        IOrganizationService organizationService,
        IOrganizationRepository organizationRepository)
    {
        _organizationService = organizationService;
        _organizationRepository = organizationRepository;
    }

    /// <summary>
    /// Registra uma nova organização com usuário administrador
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterOrganizationDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _organizationService.RegisterOrganizationAsync(dto);

            if (!result.Success || result.Data == null)
                return BadRequest(new { Success = false, ErrorMessage = result.ErrorMessage ?? "Falha ao registrar a organização." });

            return Ok(new
            {
                Success = true,
                OrganizationId = result.Data.OrganizationId,
                AdminUserId = result.Data.AdminUserId,
                AdminEmail = result.Data.AdminEmail
            });
        }
        catch (System.Exception ex)
        {
            Console.Error.WriteLine($"Erro ao registrar organização: {ex}");
            return StatusCode(500, new { Success = false, ErrorMessage = "Erro interno ao registrar a organização." });
        }
    }


    /// <summary>
    /// Retorna dados da organização do usuário logado
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyOrganization()
    {
        try
        {
            var orgId = User.GetOrganizationId(); // extensão de Claims
            var organization = await _organizationRepository.GetByIdWithAdminAsync(orgId);

            if (organization == null)
                return NotFound(new { Success = false, ErrorMessage = "Organização não encontrada." });

            var admin = organization.Users.FirstOrDefault(u => u.Role == "admin");

            var dto = new OrganizationWithAdminDTO
            {
                Id = organization.Id,
                Nome = organization.Name,
                RazaoSocial = organization.LegalName,
                Cnpj = organization.Cnpj,
                CriadoEm = organization.CreatedAt,
                Admin = admin == null ? null : new OrganizationWithAdminDTO.AdminDTO
                {
                    Id = admin.Id,
                    FirstName = admin.FirstName,
                    LastName = admin.LastName,
                    Email = admin.Email
                }
            };

            return Ok(dto);
        }
        catch (System.Exception ex)
        {
            Console.Error.WriteLine($"Erro ao buscar organização: {ex}");
            return StatusCode(500, new { Success = false, ErrorMessage = "Erro interno ao buscar organização." });
        }
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMyOrganization([FromBody] UpdateMyOrganizationDTO dto)
    {
        try
        {
            var orgId = User.GetOrganizationId();
            var organization = await _organizationRepository.GetByIdAsync(orgId);

            if (organization == null)
                return NotFound(new { Success = false, ErrorMessage = "Organização não encontrada." });

            if (dto.Nome != null)
            {
                var nome = dto.Nome.Trim();

                if (!string.IsNullOrWhiteSpace(nome))
                {
                    organization.Name = nome;
                    organization.BrandingDisplayName = nome;
                }
            }

            if (dto.RazaoSocial != null)
                organization.LegalName = dto.RazaoSocial.Trim();

            if (dto.Cnpj != null)
                organization.Cnpj = dto.Cnpj.Trim();

            organization.UpdatedAt = DateTime.UtcNow;

            await _organizationRepository.UpdateAsync(organization);

            return Ok(new OrganizationWithAdminDTO
            {
                Id = organization.Id,
                Nome = organization.Name,
                RazaoSocial = organization.LegalName,
                Cnpj = organization.Cnpj,
                CriadoEm = organization.CreatedAt,
                Admin = null
            });
        }
        catch (System.Exception ex)
        {
            Console.Error.WriteLine($"Erro ao atualizar organização: {ex}");
            return StatusCode(500, new { Success = false, ErrorMessage = "Erro interno ao atualizar organização." });
        }
    }
}
