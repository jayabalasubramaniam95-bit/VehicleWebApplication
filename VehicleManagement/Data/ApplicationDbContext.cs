using Microsoft.EntityFrameworkCore;
using static VehicleManagement.ViewModels.VehicleCategoryIcons;
using VehicleManagement.Models;

namespace VehicleManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Manufacturer> Manufacturers { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<VehicleCategory> VehicleCategories  { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // OnModelCreating
modelBuilder.Entity<Manufacturer>(e =>
{
    e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");   // SQL Server
    e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
});

modelBuilder.Entity<VehicleCategory>(e =>
{
    e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
    e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
});
            modelBuilder.Entity<Manufacturer>(e =>
            {
                e.Property(m => m.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                e.HasIndex(m => m.Name)
                    .IsUnique();

                e.HasMany(m => m.Vehicles)
                    .WithOne(v => v.Manufacturer)
                    .HasForeignKey(v => v.ManufacturerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<VehicleCategory>(e =>
            {
                e.Property(c => c.Name).IsRequired().HasMaxLength(50);
                e.Property(c => c.MinWeight).HasPrecision(18, 2);
                e.Property(c => c.MaxWeight).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.Manufacturer)
                .WithMany(m => m.Vehicles)
                .HasForeignKey(v => v.ManufacturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.Category)
                .WithMany(c => c.Vehicles)
                .HasForeignKey(v => v.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Vehicle>()
                .Property(v => v.Weight)
                .HasPrecision(10, 2);

            SeedManufacturers(modelBuilder);
            SeedVehicleCategories(modelBuilder);
        }

        private static void SeedManufacturers(ModelBuilder modelBuilder)
        {
            var seedDate = new DateTime(2026, 10, 04, 15, 45, 32);
            modelBuilder.Entity<Manufacturer>().HasData(
                new Manufacturer { Id = 1, Name = "Mazda",IsDefault = true , CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new Manufacturer { Id = 2, Name = "Mercedes", IsDefault = true, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new Manufacturer { Id = 3, Name = "Honda", IsDefault = true, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new Manufacturer { Id = 4, Name = "Ferrari", IsDefault = true, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new Manufacturer { Id = 5, Name = "Toyota", IsDefault = true, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false }
            );
        }

        private static void SeedVehicleCategories(ModelBuilder modelBuilder)
        {
           var seedDate = new DateTime(2026, 10, 04, 15, 45, 32);
            modelBuilder.Entity<VehicleCategory>().HasData(
                new VehicleCategory { Id = 1, Name = "Light", MinWeight = 0, MaxWeight = 500, CreatedAt = seedDate, UpdatedAt = seedDate, Icon = "car-green", IsDeleted = false },
                new VehicleCategory { Id = 2, Name = "Medium", MinWeight = 500, MaxWeight = 2500, CreatedAt = seedDate, UpdatedAt = seedDate, Icon = "van-yellow", IsDeleted = false },
                new VehicleCategory { Id = 3, Name = "Heavy", MinWeight = 2500, MaxWeight = null, CreatedAt = seedDate, UpdatedAt = seedDate, Icon = "truck-red", IsDeleted = false }
            );

        }
    }
}