using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using WiseMonitor.Api.Data;

namespace WiseMonitor.Api.Services
{
    public class LiveSessionService : ILiveSessionService
    {
        private readonly AppDbContext _context;

        private static readonly ConcurrentDictionary<Guid, string> _organizationSessions = new();
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>> _userSessions = new();

        public LiveSessionService(AppDbContext context)
        {
            _context = context;
        }

        public Task<string> GetOrCreateSessionForOrganizationAsync(Guid organizationId)
        {
            if (_organizationSessions.TryGetValue(organizationId, out var sessionId))
                return Task.FromResult(sessionId);

            sessionId = Guid.NewGuid().ToString();
            _organizationSessions[organizationId] = sessionId;

            Console.WriteLine($"[LiveSessionService] 🟢 Nova sessão criada para organização {organizationId} -> {sessionId}");
            return Task.FromResult(sessionId);
        }

        public Task<string> GetOrCreateSessionForUserAsync(Guid organizationId, Guid userId)
        {
            var usersDict = _userSessions.GetOrAdd(organizationId, _ => new ConcurrentDictionary<Guid, string>());
            var sessionId = usersDict.GetOrAdd(userId, _ => Guid.NewGuid().ToString());

            Console.WriteLine($"[LiveSessionService] 🟢 Sessão do usuário {userId} da organização {organizationId} -> {sessionId}");
            return Task.FromResult(sessionId);
        }

       public Task EndSessionForOrganizationAsync(Guid organizationId)
        {
            _organizationSessions.TryRemove(organizationId, out _);
            _userSessions.TryRemove(organizationId, out _);

            Console.WriteLine($"[LiveSessionService] 🟡 Sessão global encerrada para organização {organizationId}");
            return Task.CompletedTask;
        }


        public Task EndSessionForUserAsync(Guid organizationId, Guid userId)
        {
            if (_userSessions.TryGetValue(organizationId, out var usersDict))
            {
                usersDict.TryRemove(userId, out _);
                Console.WriteLine($"[LiveSessionService] 🟡 Sessão encerrada do usuário {userId} na organização {organizationId}");
            }
            return Task.CompletedTask;
        }

        public bool HasActiveSession(Guid organizationId) => _organizationSessions.ContainsKey(organizationId);

        public bool HasActiveSession(Guid organizationId, Guid userId) =>
            _userSessions.TryGetValue(organizationId, out var usersDict) && usersDict.ContainsKey(userId);
    }
}
