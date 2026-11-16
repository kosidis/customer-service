using CustomerService.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Api.Data;

public class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(c => c.LastName).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256).IsRequired();
            entity.Property(c => c.PhoneNumber).HasMaxLength(32);
            entity.Property(c => c.Domain).HasMaxLength(253);
            entity.HasIndex(c => c.Email).IsUnique();

            // The database stores no kind, so mark values as UTC on read; otherwise JSON drops the "Z".
            entity.Property(c => c.CreatedAtUtc).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(c => c.UpdatedAtUtc).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });
    }
}
