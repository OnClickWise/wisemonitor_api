using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Data
{
    public class AppDbContext : DbContext
    {
        private readonly ITenantContext _tenant;
        // ==============================
        // DbSets principais
        // ==============================
        public DbSet<Organization> Organizations { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
        public DbSet<Screenshot> Screenshots { get; set; } = null!;
        public DbSet<Device> Devices { get; set; } = null!;
        public DbSet<LiveSession> LiveSessions { get; set; } = null!;
        public DbSet<AppFocusEvent> AppFocusEvents { get; set; } = null!;

        // TEAM
        public DbSet<Team> Teams { get; set; } = null!;
        public DbSet<TeamMember> TeamMembers { get; set; } = null!;

        // DEPARTMENTS
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<DepartmentMember> DepartmentMembers { get; set; } = null!;

        // DELEGATIONS
        public DbSet<Delegation> Delegations { get; set; } = null!;

        // AUDIT LOGS
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        // SUPER ADMIN
        public DbSet<AlertRule> AlertRules { get; set; } = null!;
        public DbSet<AlertHistory> AlertHistories { get; set; } = null!;
        public DbSet<AgentVersion> AgentVersions { get; set; } = null!;
        public DbSet<PlatformIntegration> PlatformIntegrations { get; set; } = null!;
        public DbSet<PlatformSettings> PlatformSettings { get; set; } = null!;

        // WORK SCHEDULE
        public DbSet<WorkSchedule> WorkSchedules { get; set; } = null!;
        public DbSet<WorkScheduleRule> WorkScheduleRules { get; set; } = null!;
        public DbSet<UserWorkSchedule> UserWorkSchedules { get; set; } = null!;

        // KEYBOARD MONITORING
        public DbSet<KeyboardSession> KeyboardSessions { get; set; } = null!;
        public DbSet<KeyboardWord> KeyboardWords { get; set; } = null!;

        // ==============================
        // Construtor
        // ==============================
        public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenant = null)
            : base(options)
        {
            _tenant = tenant ?? new NullTenantContext();
        }

        // ==============================
        // Modelagem EF Core
        // ==============================
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new WorkScheduleConfiguration());

            modelBuilder.Entity<Organization>().ToTable("Organizations");
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Device>().ToTable("Devices");
            modelBuilder.Entity<LiveSession>().ToTable("LiveSessions");
            modelBuilder.Entity<Screenshot>().ToTable("UserScreenshots");
            modelBuilder.Entity<Team>().ToTable("Teams");
            modelBuilder.Entity<TeamMember>().ToTable("TeamMembers");
            modelBuilder.Entity<Department>().ToTable("Departments");
            modelBuilder.Entity<DepartmentMember>().ToTable("DepartmentMembers");
            modelBuilder.Entity<Delegation>().ToTable("Delegations");
            modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");

            // ==============================
            // Isolamento de tenant (global query filters)
            // Sempre que _tenant.IsActive for false (testes, migrations) ou o usuário
            // for SuperAdmin da plataforma, o filtro não restringe nada.
            // ==============================
            modelBuilder.Entity<User>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<Device>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<Screenshot>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<LiveSession>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<AppFocusEvent>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<Team>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<Department>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<Delegation>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<AuditLog>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<WorkSchedule>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<WorkScheduleRule>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<UserWorkSchedule>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);
            modelBuilder.Entity<KeyboardSession>().HasQueryFilter(e =>
                !_tenant.IsActive || _tenant.IsSuperAdmin || e.OrganizationId == _tenant.OrganizationId);

            // ==============================
            // Organization ↔ User
            // ==============================
            modelBuilder.Entity<Organization>(entity =>
            {
                entity.HasOne(o => o.AdminUser)
                      .WithMany()
                      .HasForeignKey(o => o.AdminUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(o => o.Users)
                      .WithOne(u => u.Organization)
                      .HasForeignKey(u => u.OrganizationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(o => o.Departments)
                      .WithOne(d => d.Organization)
                      .HasForeignKey(d => d.OrganizationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==============================
            // User — OrganizationId nullable
            // ==============================
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.OrganizationId).IsRequired(false);
            });

            // ==============================
            // TeamMember (N:N)
            // ==============================
            modelBuilder.Entity<TeamMember>(entity =>
            {
                entity.HasKey(tm => new { tm.TeamId, tm.UserId });
                entity.Property(tm => tm.JoinedAt).IsRequired();

                entity.HasOne(tm => tm.Team)
                      .WithMany(t => t.Members)
                      .HasForeignKey(tm => tm.TeamId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(tm => tm.User)
                      .WithMany(u => u.TeamMemberships)
                      .HasForeignKey(tm => tm.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==============================
            // Team ↔ Department
            // ==============================
            modelBuilder.Entity<Team>(entity =>
            {
                entity.HasOne(t => t.Department)
                      .WithMany(d => d.Teams)
                      .HasForeignKey(t => t.DepartmentId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ==============================
            // DepartmentMember (N:N)
            // ==============================
            modelBuilder.Entity<DepartmentMember>(entity =>
            {
                entity.HasKey(dm => new { dm.DepartmentId, dm.UserId });

                entity.HasOne(dm => dm.Department)
                      .WithMany(d => d.Members)
                      .HasForeignKey(dm => dm.DepartmentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(dm => dm.User)
                      .WithMany(u => u.DepartmentMemberships)
                      .HasForeignKey(dm => dm.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==============================
            // Delegation
            // ==============================
            modelBuilder.Entity<Delegation>(entity =>
            {
                entity.HasOne(d => d.Delegator)
                      .WithMany()
                      .HasForeignKey(d => d.DelegatorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.Delegate)
                      .WithMany()
                      .HasForeignKey(d => d.DelegateId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => new { d.OrganizationId, d.IsActive, d.EndDate });
            });

            // ==============================
            // AuditLog
            // ==============================
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(a => new { a.OrganizationId, a.CreatedAt });
                entity.HasIndex(a => a.Action);
                entity.HasIndex(a => a.UserId);
            });

            // ==============================
            // AlertRule + AlertHistory
            // ==============================
            modelBuilder.Entity<AlertRule>(entity =>
            {
                entity.ToTable("AlertRules");
                entity.HasIndex(r => r.Trigger);
            });

            modelBuilder.Entity<AlertHistory>(entity =>
            {
                entity.ToTable("AlertHistories");
                entity.HasIndex(h => h.AlertRuleId);
                entity.HasIndex(h => h.CreatedAt);
                entity.HasIndex(h => h.IsResolved);

                entity.HasOne(h => h.AlertRule)
                      .WithMany()
                      .HasForeignKey(h => h.AlertRuleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==============================
            // AgentVersion
            // ==============================
            modelBuilder.Entity<AgentVersion>(entity =>
            {
                entity.ToTable("AgentVersions");
                entity.HasIndex(v => v.Version).IsUnique();
                entity.HasIndex(v => v.Channel);
            });

            // ==============================
            // PlatformIntegration
            // ==============================
            modelBuilder.Entity<PlatformIntegration>(entity =>
            {
                entity.ToTable("PlatformIntegrations");
                entity.HasIndex(i => i.Type);
            });

            // ==============================
            // PlatformSettings (singleton)
            // ==============================
            modelBuilder.Entity<PlatformSettings>(entity =>
            {
                entity.ToTable("PlatformSettings");
            });

            // ==============================
            // KeyboardSession
            // ==============================
            modelBuilder.Entity<KeyboardSession>(entity =>
            {
                entity.ToTable("KeyboardSessions");
                entity.HasKey(k => k.Id);
                entity.Property(k => k.Id).ValueGeneratedNever();
                entity.Property(k => k.Application).HasMaxLength(200).IsRequired();
                entity.Property(k => k.StartAt).IsRequired();
                entity.Property(k => k.EndAt).IsRequired();
                entity.Property(k => k.TotalKeystrokes).IsRequired();
                entity.Property(k => k.LettersCount).IsRequired();
                entity.Property(k => k.WordsCount).IsRequired();
                entity.Property(k => k.NumbersCount).IsRequired();
                entity.Property(k => k.SymbolsCount).IsRequired();
                entity.Property(k => k.ProductivityScore).IsRequired();
                entity.Property(k => k.Classification).IsRequired();
                entity.HasIndex(k => new { k.OrganizationId, k.UserId, k.StartAt });

                entity.HasOne<Organization>()
                      .WithMany()
                      .HasForeignKey(k => k.OrganizationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(k => k.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==============================
            // KeyboardWord
            // ==============================
            modelBuilder.Entity<KeyboardWord>(entity =>
            {
                entity.ToTable("KeyboardWords");
                entity.HasKey(w => w.Id);
                entity.Property(w => w.Id).ValueGeneratedNever();
                entity.Property(w => w.Word).HasMaxLength(200).IsRequired();
                entity.Property(w => w.Count).IsRequired();
                entity.Property(w => w.Category).IsRequired();
                entity.HasIndex(w => w.Word);
                entity.HasIndex(w => w.Category);

                entity.HasOne(w => w.KeyboardSession)
                      .WithMany(k => k.Words)
                      .HasForeignKey(w => w.KeyboardSessionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==============================
            // UserWorkSchedule
            // ==============================
            modelBuilder.Entity<UserWorkSchedule>(entity =>
            {
                entity.ToTable("UserWorkSchedules");
                entity.HasKey(us => us.Id);
                entity.Property(us => us.AssignedAt).HasDefaultValueSql("NOW()");

                entity.HasOne(us => us.User)
                      .WithMany()
                      .HasForeignKey(us => us.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(us => us.WorkSchedule)
                      .WithMany(ws => ws.UserSchedules)
                      .HasForeignKey(us => us.WorkScheduleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(us => new { us.UserId, us.WorkScheduleId }).IsUnique();
            });

            // ==============================
            // Team ↔ DefaultWorkSchedule
            // ==============================
            modelBuilder.Entity<Team>(entity =>
            {
                entity.HasOne(t => t.DefaultWorkSchedule)
                      .WithMany()
                      .HasForeignKey(t => t.DefaultWorkScheduleId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }

        // ==============================
        // Controle de timestamps
        // ==============================
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is User &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var user = (User)entry.Entity;
                user.UpdatedAt = DateTime.UtcNow;
                if (entry.State == EntityState.Added)
                    user.CreatedAt = DateTime.UtcNow;
            }
        }
    }
}
