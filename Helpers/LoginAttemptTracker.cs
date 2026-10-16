using System;
using System.Collections.Concurrent;

namespace WiseMonitor.Api.Helpers
{
    /// <summary>
    /// Bloqueio por conta depois de várias senhas erradas seguidas. O rate limit por IP
    /// sozinho não segura um ataque distribuído contra um e-mail específico.
    ///
    /// Em memória, por instância: com várias instâncias no Cloud Run o limite efetivo
    /// é multiplicado pelo número de instâncias — ainda corta força bruta, mas para um
    /// bloqueio exato o estado teria que ir para o banco/Redis.
    /// </summary>
    public class LoginAttemptTracker
    {
        public const int MaxFailures = 5;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private readonly ConcurrentDictionary<string, Entry> _entries = new();
        private readonly Func<DateTime> _now;

        public LoginAttemptTracker() : this(() => DateTime.UtcNow) { }

        public LoginAttemptTracker(Func<DateTime> now) => _now = now;

        public bool IsLockedOut(string email, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            if (!_entries.TryGetValue(Key(email), out var entry) || entry.LockedUntil == null)
                return false;

            var remaining = entry.LockedUntil.Value - _now();
            if (remaining <= TimeSpan.Zero)
                return false;

            retryAfter = remaining;
            return true;
        }

        public void RecordFailure(string email)
        {
            var now = _now();
            _entries.AddOrUpdate(
                Key(email),
                _ => new Entry(1, now, null),
                (_, e) =>
                {
                    // Janela expirada (ou bloqueio já cumprido): recomeça a contagem.
                    if (now - e.FirstFailureAt > Window || (e.LockedUntil != null && e.LockedUntil <= now))
                        return new Entry(1, now, null);

                    var failures = e.Failures + 1;
                    return new Entry(failures, e.FirstFailureAt,
                        failures >= MaxFailures ? now + LockoutDuration : e.LockedUntil);
                });

            PruneIfLarge(now);
        }

        public void Reset(string email) => _entries.TryRemove(Key(email), out _);

        private void PruneIfLarge(DateTime now)
        {
            if (_entries.Count < 10_000) return;
            foreach (var (key, e) in _entries)
                if (now - e.FirstFailureAt > Window && (e.LockedUntil == null || e.LockedUntil <= now))
                    _entries.TryRemove(key, out _);
        }

        private static string Key(string email) => email.Trim().ToLowerInvariant();

        private sealed record Entry(int Failures, DateTime FirstFailureAt, DateTime? LockedUntil);
    }
}
