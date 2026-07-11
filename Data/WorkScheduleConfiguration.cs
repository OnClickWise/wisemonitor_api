using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Data
{
    public class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
    {
        public void Configure(EntityTypeBuilder<WorkSchedule> builder)
        {
            builder.ToTable("WorkSchedules");

            builder.HasKey(w => w.Id);

            builder.Property(w => w.Name)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(w => w.Type)
                .IsRequired();

            builder.Property(w => w.CreatedAt)
                .HasDefaultValueSql("NOW()");
        }
    }
}
