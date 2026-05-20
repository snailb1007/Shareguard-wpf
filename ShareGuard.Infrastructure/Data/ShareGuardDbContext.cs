using Microsoft.EntityFrameworkCore;
using ShareGuard.Domain.Entities;

namespace ShareGuard.Infrastructure.Data;

public class ShareGuardDbContext : DbContext
{
    public DbSet<HistoryEvent> HistoryEvents => Set<HistoryEvent>();

    public ShareGuardDbContext(DbContextOptions<ShareGuardDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<HistoryEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalPath).IsRequired();
            entity.Property(e => e.CleanPath).IsRequired();
            entity.Property(e => e.ProcessedAt).IsRequired();
            entity.Property(e => e.IsSuccess).IsRequired();
        });
    }
}
