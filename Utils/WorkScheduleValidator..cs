using System;
using System.Collections.Generic;
using System.Linq;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Utils
{
    public static class WorkScheduleValidator
    {
        public static void ValidateCreate(WorkScheduleCreateDTO dto)
        {
            ValidateBase(dto.Name, dto.Type, dto.Rules);
        }

        public static void ValidateUpdate(WorkScheduleUpdateDTO dto)
        {
            ValidateBase(dto.Name, dto.Type, dto.Rules);
        }

        private static void ValidateBase(
            string name,
            int type,
            List<WorkScheduleRuleDTO> rules)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.");

            if (rules == null || !rules.Any())
                throw new ArgumentException("At least one rule is required.");

            foreach (var rule in rules)
            {
                // WorkDay vai de 1 (segunda) a 7 (domingo); o limite antigo (0–6)
                // recusava domingo.
                if (rule.Day < 1 || rule.Day > 7)
                    throw new ArgumentException("Invalid day of week.");

                // Jornada que passa da meia-noite (ex.: 15:40 às 00:00) termina "antes"
                // de começar no relógio; o fim conta como do dia seguinte.
                var endMinutes = rule.CrossesMidnight && rule.EndTimeMinutes <= rule.StartTimeMinutes
                    ? rule.EndTimeMinutes + 24 * 60
                    : rule.EndTimeMinutes;

                if (rule.StartTimeMinutes >= endMinutes)
                    throw new ArgumentException("Start time must be before end time.");

                if (rule.BreakDurationMinutes < 0)
                    throw new ArgumentException("Break duration cannot be negative.");

                var workDuration =
                    endMinutes - rule.StartTimeMinutes;

                if (rule.BreakDurationMinutes >= workDuration)
                    throw new ArgumentException("Break duration exceeds work duration.");

                if (rule.ToleranceMinutes < 0 || rule.ToleranceMinutes > 120)
                    throw new ArgumentException("Tolerance must be between 0 and 120 minutes.");
            }
        }
    }
}
