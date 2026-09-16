using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models;
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

        // SuperAdmin não tem organização vinculada — devolve null em vez de lançar,
        // para que os endpoints de leitura respondam com um branding padrão em vez
        // de 500 quando quem está logado é o admin da plataforma, não de um tenant.
        private Guid? GetOrgId()
        {
            var claim = User.FindFirst("orgId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private Guid RequireOrgId()
        {
            return GetOrgId() ?? throw new UnauthorizedAccessException("Organização não identificada.");
        }

        /// <summary>Retorna o branding atual da organização do usuário autenticado.</summary>
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var orgId = GetOrgId();

            if (orgId == null)
            {
                return Ok(new
                {
                    success = true,
                    message = "SuperAdmin — branding padrão.",
                    data = new BrandingDTO()
                });
            }

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

        /// <summary>Retorna as configurações avançadas de aparência da organização autenticada.</summary>
        [HttpGet("appearance")]
        public async Task<IActionResult> GetAppearance(CancellationToken ct)
        {
            var orgId = GetOrgId();

            if (orgId == null)
            {
                return Ok(new
                {
                    success = true,
                    message = "SuperAdmin — aparência padrão.",
                    data = Array.Empty<OrganizationAppearanceSettingsDTO>()
                });
            }

            var settings = await _context.OrganizationAppearanceSettings
                .AsNoTracking()
                .Where(a => a.OrganizationId == orgId.Value)
                .OrderBy(a => a.Theme)
                .ToListAsync(ct);

            var data = settings.Select(settingsItem =>
                new OrganizationAppearanceSettingsDTO
                {
                    Theme = settingsItem.Theme,

                    // Fundo da página
                    PageBackgroundType = settingsItem.PageBackgroundType,
                    PageBackgroundColor = settingsItem.PageBackgroundColor,
                    PageGradientStart = settingsItem.PageGradientStart,
                    PageGradientEnd = settingsItem.PageGradientEnd,
                    PageGradientDirection = settingsItem.PageGradientDirection,

                    // Sidebar
                    SidebarBackgroundType = settingsItem.SidebarBackgroundType,
                    SidebarColor = settingsItem.SidebarColor,
                    SidebarGradientStart = settingsItem.SidebarGradientStart,
                    SidebarGradientEnd = settingsItem.SidebarGradientEnd,
                    SidebarGradientDirection = settingsItem.SidebarGradientDirection,
                    SidebarForegroundColor = settingsItem.SidebarForegroundColor,
                    SidebarActiveBackground = settingsItem.SidebarActiveBackground,
                    SidebarActiveForeground = settingsItem.SidebarActiveForeground,
                    SidebarHoverBackground = settingsItem.SidebarHoverBackground,
                    SidebarHoverForeground = settingsItem.SidebarHoverForeground,
                    SidebarProfileBackground = settingsItem.SidebarProfileBackground,

                    // Cards
                    CardBackgroundType = settingsItem.CardBackgroundType,
                    CardBackgroundColor = settingsItem.CardBackgroundColor,
                    CardGradientStart = settingsItem.CardGradientStart,
                    CardGradientEnd = settingsItem.CardGradientEnd,
                    CardGradientDirection = settingsItem.CardGradientDirection,
                    CardPrimaryTextColor = settingsItem.CardPrimaryTextColor,
                    CardSecondaryTextColor = settingsItem.CardSecondaryTextColor,

                    // Botões
                    ButtonBackgroundColor = settingsItem.ButtonBackgroundColor,
                    ButtonForegroundColor = settingsItem.ButtonForegroundColor,
                    ButtonHoverBackgroundColor = settingsItem.ButtonHoverBackgroundColor,

                    // Gráfico
                    ChartBackgroundType = settingsItem.ChartBackgroundType,
                    ChartColor = settingsItem.ChartColor,
                    ChartGradientStart = settingsItem.ChartGradientStart,
                    ChartGradientEnd = settingsItem.ChartGradientEnd,
                    ChartGradientDirection = settingsItem.ChartGradientDirection
                })
                .ToList();

            return Ok(new
            {
                success = true,
                message = data.Count > 0
                    ? "Configurações de aparência carregadas com sucesso."
                    : "Aparência padrão.",
                data
            });
        }

        /// <summary>Atualiza as configurações avançadas de aparência da organização autenticada.</summary>
        [HttpPut("appearance")]
        public async Task<IActionResult> UpdateAppearance(
            [FromBody] OrganizationAppearanceSettingsDTO dto,
            CancellationToken ct)
        {
            var role = User.FindFirstValue(ClaimTypes.Role)
                    ?? User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    ?? string.Empty;

            if (!role.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var orgId = RequireOrgId();

            var theme = dto.Theme?.Trim().ToLowerInvariant();

            if (theme != "light" && theme != "dark")
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Tema inválido. Utilize 'light' ou 'dark'."
                });
            }

            var orgExists = await _context.Organizations
                .AnyAsync(o => o.Id == orgId, ct);

            if (!orgExists)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Organização não encontrada."
                });
            }

            var settings = await _context.OrganizationAppearanceSettings
                .FirstOrDefaultAsync(
                    a => a.OrganizationId == orgId &&
                         a.Theme == theme,
                    ct
                );

            if (settings == null)
            {
                settings = new OrganizationAppearanceSettings
                {
                    OrganizationId = orgId,
                    Theme = theme
                };

                _context.OrganizationAppearanceSettings.Add(settings);
            }

            // Fundo da página
            settings.PageBackgroundType = dto.PageBackgroundType;
            settings.PageBackgroundColor = dto.PageBackgroundColor;
            settings.PageGradientStart = dto.PageGradientStart;
            settings.PageGradientEnd = dto.PageGradientEnd;
            settings.PageGradientDirection = dto.PageGradientDirection;

            // Sidebar
            settings.SidebarBackgroundType = dto.SidebarBackgroundType;
            settings.SidebarColor = dto.SidebarColor;
            settings.SidebarGradientStart = dto.SidebarGradientStart;
            settings.SidebarGradientEnd = dto.SidebarGradientEnd;
            settings.SidebarGradientDirection = dto.SidebarGradientDirection;
            settings.SidebarForegroundColor = dto.SidebarForegroundColor;
            settings.SidebarActiveBackground = dto.SidebarActiveBackground;
            settings.SidebarActiveForeground = dto.SidebarActiveForeground;
            settings.SidebarHoverBackground = dto.SidebarHoverBackground;
            settings.SidebarHoverForeground = dto.SidebarHoverForeground;
            settings.SidebarProfileBackground = dto.SidebarProfileBackground;

            // Cards
            settings.CardBackgroundType = dto.CardBackgroundType;
            settings.CardBackgroundColor = dto.CardBackgroundColor;
            settings.CardGradientStart = dto.CardGradientStart;
            settings.CardGradientEnd = dto.CardGradientEnd;
            settings.CardGradientDirection = dto.CardGradientDirection;
            settings.CardPrimaryTextColor = dto.CardPrimaryTextColor;
            settings.CardSecondaryTextColor = dto.CardSecondaryTextColor;

            // Botões
            settings.ButtonBackgroundColor = dto.ButtonBackgroundColor;
            settings.ButtonForegroundColor = dto.ButtonForegroundColor;
            settings.ButtonHoverBackgroundColor = dto.ButtonHoverBackgroundColor;

            // Gráfico
            settings.ChartBackgroundType = dto.ChartBackgroundType;
            settings.ChartColor = dto.ChartColor;
            settings.ChartGradientStart = dto.ChartGradientStart;
            settings.ChartGradientEnd = dto.ChartGradientEnd;
            settings.ChartGradientDirection = dto.ChartGradientDirection;

            settings.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            return Ok(new
            {
                success = true,
                message = $"Configurações do tema '{theme}' salvas com sucesso.",
                data = new
                {
                    theme
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

            var orgId = RequireOrgId();
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

            var orgId = RequireOrgId();
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
