using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WiseMonitor.Api.Data;

namespace WiseMonitor.Api.Services
{
    public class LiveSessionService : ILiveSessionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LiveSessionService> _logger;

        private static readonly ConcurrentDictionary<Guid, string> _organizationSessions = new();
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>> _userSessions = new();

        public LiveSessionService(AppDbContext context, ILogger<LiveSessionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public Task<string> GetOrCreateSessionForOrganizationAsync(Guid organizationId)
        {
            if (_organizationSessions.TryGetValue(organizationId, out var sessionId))
                return Task.FromResult(sessionId);

            sessionId = Guid.NewGuid().ToString();
            _organizationSessions[organizationId] = sessionId;

            _logger.LogInformation("Nova sessão criada para organização {OrganizationId} -> {SessionId}", organizationId, sessionId);
            return Task.FromResult(sessionId);
        }

        public Task<string> GetOrCreateSessionForUserAsync(Guid organizationId, Guid userId)
        {
            var usersDict = _userSessions.GetOrAdd(organizationId, _ => new ConcurrentDictionary<Guid, string>());
            var sessionId = usersDict.GetOrAdd(userId, _ => Guid.NewGuid().ToString());

            _logger.LogInformation("Sessão do usuário {UserId} da organização {OrganizationId} -> {SessionId}", userId, organizationId, sessionId);
            return Task.FromResult(sessionId);
        }

       public Task EndSessionForOrganizationAsync(Guid organizationId)
        {
            _organizationSessions.TryRemove(organizationId, out _);
            _userSessions.TryRemove(organizationId, out _);

            _logger.LogInformation("Sessão global encerrada para organização {OrganizationId}", organizationId);
            return Task.CompletedTask;
        }


        public Task EndSessionForUserAsync(Guid organizationId, Guid userId)
        {
            if (_userSessions.TryGetValue(organizationId, out var usersDict))
            {
                usersDict.TryRemove(userId, out _);
                _logger.LogInformation("Sessão encerrada do usuário {UserId} na organização {OrganizationId}", userId, organizationId);
            }
            return Task.CompletedTask;
        }

        public bool HasActiveSession(Guid organizationId) => _organizationSessions.ContainsKey(organizationId);

        public bool HasActiveSession(Guid organizationId, Guid userId) =>
            _userSessions.TryGetValue(organizationId, out var usersDict) && usersDict.ContainsKey(userId);
    }
}
