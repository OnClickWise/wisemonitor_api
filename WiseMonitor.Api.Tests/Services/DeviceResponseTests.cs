using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Controllers;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

/// <summary>
/// GET api/devices/{id} devolvia a entidade: com o usuário do device já carregado na
/// requisição, a resposta levava o User inteiro (PasswordHash incluso) ou quebrava a
/// serialização num ciclo.
/// </summary>
public class DeviceResponseTests
{
    [Fact]
    public async Task Detalhe_do_device_nao_leva_dados_do_usuario_mesmo_com_ele_carregado()
    {
        var org = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Users.Add(new User
        {
            Id = userId, FirstName = "Ana", LastName = "T", Email = "ana@x.com",
            PasswordHash = "SEGREDO", Role = "Employee", IsActive = true, OrganizationId = org
        });
        db.Devices.Add(new Device { Id = deviceId, Hostname = "PC-1", OrganizationId = org, UserId = userId, AgentVersion = "1.7.0" });
        db.SaveChanges();

        // Usuário rastreado no mesmo contexto: o EF liga Device.User sozinho.
        _ = db.Users.First(u => u.Id == userId);

        var controller = new DevicesController(
            new DeviceService(new DeviceRepository(db)),
            new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance),
            null!, null!, new TeamRepository(db), new AccessScopeService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim("userId", Guid.NewGuid().ToString()),
                        new Claim("orgId", org.ToString()),
                        new Claim(ClaimTypes.Role, "TenantAdmin")
                    }, "test"))
                }
            }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.GetById(deviceId));
        var dto = Assert.IsType<DeviceDto>(result.Value);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal("1.7.0", dto.AgentVersion);

        // Serializa como a API faria: sem ciclo e sem nada do usuário além do id.
        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("SEGREDO", json);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
    }
}
