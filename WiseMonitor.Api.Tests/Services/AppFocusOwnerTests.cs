using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WiseMonitor.Api.Controllers;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

/// <summary>
/// Dono das atividades enviadas pelo agent. O token do agent é de quem fez login nele
/// (o admin que instalou); o usuário monitorado vem no corpo. Antes o corpo era
/// ignorado e toda atividade da empresa caía no admin.
/// </summary>
public class AppFocusOwnerTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid OtherOrg = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Employee = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    /// <summary>IAppFocusService falso que só guarda o usuário recebido no registro.</summary>
    public class CapturingService : DispatchProxy
    {
        public Guid? RegisteredUser { get; private set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IAppFocusService.RegisterEventAsync))
                RegisteredUser = (Guid)args![1]!;
            return Task.CompletedTask;
        }
    }

    private static (AppFocusController Controller, CapturingService Service) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        foreach (var (id, org) in new[] { (Admin, Org), (Employee, Org), (Stranger, OtherOrg) })
            db.Users.Add(new User
            {
                Id = id, FirstName = "U", LastName = "T", Email = $"{id}@x.com",
                PasswordHash = "x", Role = "Employee", IsActive = true, OrganizationId = org
            });
        db.SaveChanges();

        var proxy = DispatchProxy.Create<IAppFocusService, CapturingService>();
        var userService = new UserService(db, new TeamRepository(db));
        var controller = new AppFocusController(proxy, userService, new AccessScopeService(db), NullLogger<AppFocusController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim("userId", Admin.ToString()),
                        new Claim("orgId", Org.ToString()),
                        new Claim(ClaimTypes.Role, "TenantAdmin")
                    }, "test"))
                }
            }
        };
        return (controller, (CapturingService)(object)proxy);
    }

    private static AppFocusEventCreateDTO Dto(Guid? userId) => new()
    {
        DeviceId = Guid.NewGuid(), UserId = userId, ApplicationName = "chrome", ProcessName = "chrome",
        WindowTitle = "x", StartTime = DateTime.UtcNow.AddMinutes(-5), EndTime = DateTime.UtcNow
    };

    [Fact]
    public async Task Atividade_vai_para_o_usuario_monitorado_e_nao_para_quem_logou_no_agent()
    {
        var (controller, service) = Create();

        await controller.Register(Dto(Employee));

        Assert.Equal(Employee, service.RegisteredUser);
    }

    [Fact]
    public async Task Sem_usuario_informado_vale_o_do_token()
    {
        var (controller, service) = Create();

        await controller.Register(Dto(null));

        Assert.Equal(Admin, service.RegisteredUser);
    }

    [Fact]
    public async Task Usuario_de_outra_organizacao_e_ignorado()
    {
        var (controller, service) = Create();

        await controller.Register(Dto(Stranger));

        Assert.Equal(Admin, service.RegisteredUser);
    }
}
