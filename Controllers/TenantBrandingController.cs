using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.SuperAdmin;
using System.IO;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/tenant/branding")]
    [Authorize]
    public class TenantBrandingController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TenantBrandingController(AppDbContext context)
            => _context = context;

        private Guid GetOrgId()
        {
            var claim = User.FindFirst("orgId")?.Value;
            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var id))
                throw new UnauthorizedAccessException("Organização não identificada.");
            return id;
        }

        /// <summary>Retorna o branding atual da organização do usuário autenticado.</summary>
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var orgId = GetOrgId();
            var org = await _context.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orgId, ct);

            if (org == null)
                return NotFound(new { success = false, message = "Organização não encontrada." });

            await _context.SaveChangesAsync(ct);

            return Ok(new
            {
                success = true,
                message = "Branding atualizado com sucesso.",
                data = new BrandingDTO
                {
                    LogoUrl = org.BrandingLogoUrl,
                    DisplayName = !string.IsNullOrWhiteSpace(org.BrandingDisplayName)
                        ? org.BrandingDisplayName
                        : org.Name,
                    PrimaryColor = org.BrandingPrimaryColor,
                    SecondaryColor = org.BrandingSecondaryColor,
                    AccentColor = org.BrandingAccentColor,
                    FontFamily = org.BrandingFontFamily,
                }
            });
        }

        /// <summary>Atualiza o branding da organização do usuário autenticado (TenantAdmin apenas).</summary>
        [HttpPut]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update([FromForm] UpdateBrandingDTO dto, IFormFile? logoFile, CancellationToken ct)
        {
            var role = User.FindFirstValue(ClaimTypes.Role)
                    ?? User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    ?? string.Empty;

            if (!role.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var orgId = GetOrgId();
            var org = await _context.Organizations.FindAsync(new object[] { orgId }, ct);

            if (org == null)
                return NotFound(new { success = false, message = "Organização não encontrada." });

            if (logoFile != null && logoFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "logos");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var extension = Path.GetExtension(logoFile.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await logoFile.CopyToAsync(stream, ct);

                org.BrandingLogoUrl = $"/uploads/logos/{fileName}";
            }
            else if (dto.LogoUrl != null)
            {
                org.BrandingLogoUrl = dto.LogoUrl;
            }

            if (dto.DisplayName != null)
            {
                var displayName = dto.DisplayName.Trim();

                org.BrandingDisplayName = displayName;

                if (!string.IsNullOrWhiteSpace(displayName))
                    org.Name = displayName;
            }

            if (dto.PrimaryColor != null) org.BrandingPrimaryColor = dto.PrimaryColor;
            if (dto.SecondaryColor != null) org.BrandingSecondaryColor = dto.SecondaryColor;
            if (dto.AccentColor != null) org.BrandingAccentColor = dto.AccentColor;
            if (dto.FontFamily != null) org.BrandingFontFamily = dto.FontFamily;

            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            return Ok(new
            {
                success = true,
                message = "Branding atualizado com sucesso.",
                data = new
                {
                    logoUrl = org.BrandingLogoUrl
                }
            });
        }

        /// <summary>Remove todo o branding personalizado (restaura padrão).</summary>
        [HttpDelete]
        public async Task<IActionResult> Reset(CancellationToken ct)
        {
            var role = User.FindFirstValue(ClaimTypes.Role)
                    ?? User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    ?? string.Empty;

            if (!role.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var orgId = GetOrgId();
            var org = await _context.Organizations.FindAsync(new object[] { orgId }, ct);
            if (org == null)
                return NotFound(new { success = false, message = "Organização não encontrada." });

            org.BrandingLogoUrl        = null;
            org.BrandingDisplayName    = null;
            org.BrandingPrimaryColor   = null;
            org.BrandingSecondaryColor = null;
            org.BrandingAccentColor    = null;
            org.BrandingFontFamily     = null;
            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            return Ok(new { success = true, message = "Branding redefinido para o padrão." });
        }
    }
}
