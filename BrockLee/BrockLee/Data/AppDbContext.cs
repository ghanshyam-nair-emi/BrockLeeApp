using BrockLee.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrockLee.Data;

/// <summary>
/// EF Core DbContext targeting Azure SQL (production) / SQLite (development).
/// Single table: UserLogs — public logbook, one row per API submission.
/// Connection resiliency is configured at registration in Program.cs.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserLogEntity> UserLogs => Set<UserLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserLogEntity>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
             .HasDefaultValueSql("NEWSEQUENTIALID()");  // Azure SQL sequential GUID — better index perf

            e.Property(x => x.Name)
             .HasMaxLength(200)
             .IsRequired();

            e.Property(x => x.Wage)
             .HasColumnType("decimal(18,2)");

            e.Property(x => x.AnnualIncome)
             .HasColumnType("decimal(18,2)");

            e.Property(x => x.TotalExpenseAmount)
             .HasColumnType("decimal(18,2)");

            e.Property(x => x.TotalRemanent)
             .HasColumnType("decimal(18,2)");

            e.Property(x => x.NpsRealValue)
             .HasColumnType("decimal(18,2)");

            e.Property(x => x.IndexRealValue)
             .HasColumnType("decimal(18,2)");

            e.Property(x => x.TaxBenefit)
             .HasColumnType("decimal(18,2)");

            // Index for fast DESC ordering on dashboard query
            e.HasIndex(x => x.LoggedAt)
             .IsDescending();
        });
    }
}