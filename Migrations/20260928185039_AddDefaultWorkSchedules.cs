using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using WiseMonitor.Api.Data;

#nullable disable

namespace WiseMonitor.Api.Migrations
{
    /// <summary>
    /// Backfill único das jornadas padrão (DefaultWorkSchedules) em todas as organizações
    /// que já existiam. Pula a jornada numa org que já tenha uma com o mesmo nome.
    /// Organizações criadas depois recebem as jornadas pelo OrganizationService.
    /// </summary>
    public partial class AddDefaultWorkSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // md5(...)::uuid em vez de gen_random_uuid() para não depender da versão do Postgres.
            const string newId = "md5(random()::text || clock_timestamp()::text || {0})::uuid";

            foreach (var def in DefaultWorkSchedules.All)
            {
                var name = def.Name.Replace("'", "''");

                migrationBuilder.Sql($@"
INSERT INTO ""WorkSchedules""
    (""Id"", ""OrganizationId"", ""Name"", ""Type"", ""MonitorOutsideSchedule"", ""MonitorIdleTime"", ""IsActive"", ""CreatedAt"")
SELECT {string.Format(newId, "o.\"Id\"::text")}, o.""Id"", '{name}', {(int)def.Type}, true, true, true, now()
  FROM ""Organizations"" o
 WHERE NOT EXISTS (SELECT 1 FROM ""WorkSchedules"" w
                    WHERE w.""OrganizationId"" = o.""Id"" AND lower(w.""Name"") = lower('{name}'));");

                var values = string.Join(",\n        ", def.Rules.SelectMany(rule => rule.Days.Select(day =>
                    $"({(int)day}, '{rule.Start:hh\\:mm}', '{rule.End:hh\\:mm}', {rule.BreakMinutes}, {(rule.CrossesMidnight ? "true" : "false")})")));

                // Só preenche regras de jornadas sem nenhuma regra, ou seja, as recém-criadas acima.
                migrationBuilder.Sql($@"
INSERT INTO ""WorkScheduleRules""
    (""Id"", ""OrganizationId"", ""WorkScheduleId"", ""Day"", ""StartTime"", ""EndTime"", ""BreakDuration"",
     ""ToleranceMinutes"", ""AllowOvertime"", ""IsActive"", ""CrossesMidnight"")
SELECT {string.Format(newId, "s.\"Id\"::text || v.day::text")}, s.""OrganizationId"", s.""Id"", v.day,
       v.st::interval, v.en::interval, make_interval(mins => v.brk),
       {DefaultWorkSchedules.ToleranceMinutes}, false, true, v.cm
  FROM ""WorkSchedules"" s
 CROSS JOIN (VALUES
        {values}
       ) AS v(day, st, en, brk, cm)
 WHERE s.""Name"" = '{name}'
   AND NOT EXISTS (SELECT 1 FROM ""WorkScheduleRules"" r WHERE r.""WorkScheduleId"" = s.""Id"");");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem rollback: depois do deploy os tenants podem editar ou atribuir essas jornadas
            // a usuários, e apagá-las por nome levaria junto dados que já são deles.
        }
    }
}
