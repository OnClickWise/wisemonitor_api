using System;

namespace WiseMonitor.Api.Helpers
{
    /// <summary>
    /// Período de consulta das sessões de teclado/mouse, em dias inteiros.
    /// O front manda só datas (?start=2026-09-28) e, no filtro "dia", nem manda o fim —
    /// usar start/end crus virava o intervalo 00:00–00:00 e nunca achava nada.
    /// </summary>
    public static class ActivityPeriod
    {
        /// <summary>[início do dia de start, início do dia seguinte a end) — intervalo semiaberto.</summary>
        public static (DateTime From, DateTime To) Days(DateTime start, DateTime? end)
        {
            var from = start.Date;
            var last = (end ?? start).Date;
            if (last < from) last = from;

            // UTC explícito: o Npgsql recusa DateTime sem Kind em coluna timestamptz,
            // e a data vinda da query string chega como Unspecified.
            return (DateTime.SpecifyKind(from, DateTimeKind.Utc),
                    DateTime.SpecifyKind(last.AddDays(1), DateTimeKind.Utc));
        }
    }
}
