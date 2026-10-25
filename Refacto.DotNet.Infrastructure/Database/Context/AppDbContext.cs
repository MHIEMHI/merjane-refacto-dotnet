using Microsoft.EntityFrameworkCore;
using Refacto.DotNet.Domain.Entities;

namespace Refacto.DotNet.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.Entity<Order>(entity =>
            {
                _ = entity.ToTable("orders");
                _ = entity.HasKey(order => order.Id);
                _ = entity.Property(order => order.Id).HasColumnName("id").ValueGeneratedOnAdd();
                _ = entity.HasMany(order => order.Items).WithMany();
            });

            _ = modelBuilder.Entity<Product>(entity =>
            {
                _ = entity.ToTable("products");
                _ = entity.HasKey(product => product.Id);
                _ = entity.Property(product => product.Id).HasColumnName("id").ValueGeneratedOnAdd();
                _ = entity.Property(product => product.LeadTime).HasColumnName("lead_time");
                _ = entity.Property(product => product.Available).HasColumnName("available");
                _ = entity.Property(product => product.Type).HasColumnName("type");
                _ = entity.Property(product => product.Name).HasColumnName("name");
                _ = entity.Property(product => product.ExpiryDate).HasColumnName("expiry_date");
                _ = entity.Property(product => product.SeasonStartDate).HasColumnName("season_start_date");
                _ = entity.Property(product => product.SeasonEndDate).HasColumnName("season_end_date");
            });
        }

        public DbSet<Order> Orders { get; set; } = null!;

        public DbSet<Product> Products { get; set; } = null!;
    }
}
