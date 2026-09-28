using System;
using System.Collections.Generic;
using System.Linq;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Data
{
    /// <summary>
    /// Jornadas de trabalho que toda organização recebe por padrão.
    /// Organizações novas ganham na criação (OrganizationService); as que já existiam
    /// receberam uma única vez pela migration AddDefaultWorkSchedules — por isso
    /// uma jornada padrão apagada por um tenant não volta sozinha.
    /// </summary>
    public static class DefaultWorkSchedules
    {
        public record Rule(WorkDay[] Days, TimeSpan Start, TimeSpan End, int BreakMinutes, bool CrossesMidnight = false);

        public record Definition(string Name, WorkScheduleType Type, Rule[] Rules);

        // Mesmos valores que a tela /jornada grava para uma jornada nova.
        public const int ToleranceMinutes = 5;

        private static readonly WorkDay[] MonToThu = { WorkDay.Monday, WorkDay.Tuesday, WorkDay.Wednesday, WorkDay.Thursday };
        private static readonly WorkDay[] MonToFri = MonToThu.Append(WorkDay.Friday).ToArray();
        private static readonly WorkDay[] MonToSat = MonToFri.Append(WorkDay.Saturday).ToArray();
        private static readonly WorkDay[] Fri = { WorkDay.Friday };
        private static readonly WorkDay[] Sat = { WorkDay.Saturday };

        private static TimeSpan T(string hhmm) => TimeSpan.Parse(hhmm);

        // Intervalos: 5x2 de 8h12 → 1h (7h12/dia, 36h/sem); 6x1 de 6h20 → 20min (6h/dia, 36h/sem);
        // as de 44h semanais usam 1h.
        private static Definition FiveTwo(string name, string start, string end) =>
            new(name, WorkScheduleType.FiveTwo, new[] { new Rule(MonToFri, T(start), T(end), 60) });

        public static readonly IReadOnlyList<Definition> All = new[]
        {
            FiveTwo("Escala 5x2 09:00 ás 17:12",    "09:00", "17:12"),
            FiveTwo("Escala 5x2 10:00 ás 18:12",    "10:00", "18:12"),
            FiveTwo("Escala 5x2 09:48 ás 18:00",    "09:48", "18:00"),
            FiveTwo("5x2 08:30 às 16:42",           "08:30", "16:42"),
            FiveTwo("Escala 5x2 08:00 ás 17:48",    "08:00", "17:48"),
            FiveTwo("Escala 5x2 - 10:20 ás 18:32",  "10:20", "18:32"),
            FiveTwo("Escala 5x2 08:00 ás 16:12",    "08:00", "16:12"),
            FiveTwo("Escala 5x2 - 09:30 - 17:42",   "09:30", "17:42"),
            FiveTwo("Escala 5x2 11:00 ás 19:12",    "11:00", "19:12"),
            FiveTwo("Escala 5x2 11:48 ás 20:00",    "11:48", "20:00"),

            new("Escala 5x2 - Seg a Qui 07:00 ás 17:00 e Sex 07:00 ás 16:00", WorkScheduleType.FiveTwo, new[]
            {
                new Rule(MonToThu, T("07:00"), T("17:00"), 60),
                new Rule(Fri,      T("07:00"), T("16:00"), 60),
            }),

            new("6X1 15:40 ÀS 00:00", WorkScheduleType.SixOne, new[]
            {
                new Rule(MonToSat, T("15:40"), T("00:00"), 60, CrossesMidnight: true),
            }),

            new("6X1 08:00 ÁS 16:20", WorkScheduleType.SixOne, new[]
            {
                new Rule(MonToSat, T("08:00"), T("16:20"), 60),
            }),

            new("6x1 seg a sex - 16:00 a 22:20 e sab 13:00 as 19:20", WorkScheduleType.SixOne, new[]
            {
                new Rule(MonToFri, T("16:00"), T("22:20"), 20),
                new Rule(Sat,      T("13:00"), T("19:20"), 20),
            }),

            new("6x1 Seg a Sex 09:00 ás 15:20 e Sab 08:00 ás 14:20", WorkScheduleType.SixOne, new[]
            {
                new Rule(MonToFri, T("09:00"), T("15:20"), 20),
                new Rule(Sat,      T("08:00"), T("14:20"), 20),
            }),
        };

        /// <summary>Monta as entidades das jornadas padrão para uma organização nova.</summary>
        public static List<WorkSchedule> CreateFor(Guid organizationId)
        {
            return All.Select(def =>
            {
                var schedule = new WorkSchedule
                {
                    OrganizationId = organizationId,
                    Name = def.Name,
                    Type = def.Type,
                    MonitorIdleTime = true,
                    MonitorOutsideSchedule = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var rule in def.Rules)
                foreach (var day in rule.Days)
                {
                    schedule.Rules.Add(new WorkScheduleRule
                    {
                        OrganizationId = organizationId,
                        WorkScheduleId = schedule.Id,
                        Day = day,
                        StartTime = rule.Start,
                        EndTime = rule.End,
                        BreakDuration = TimeSpan.FromMinutes(rule.BreakMinutes),
                        ToleranceMinutes = ToleranceMinutes,
                        AllowOvertime = false,
                        IsActive = true,
                        CrossesMidnight = rule.CrossesMidnight
                    });
                }

                return schedule;
            }).ToList();
        }
    }
}
