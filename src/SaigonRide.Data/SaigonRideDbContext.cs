using Microsoft.EntityFrameworkCore;
using SaigonRide.Data.Converters;
using SaigonRide.Domain.Entities;

namespace SaigonRide.Data;

/// <summary>
/// Code-First DbContext for the SaigonRide system. Implements every entity in
/// the Phase-2 ERD (§6.3) with the agreed soft-delete filter (D-05),
/// concurrency tokens, AES-256 converter for tourist passports (NFR-03), and
/// the filtered indexes documented in §6.3.3 for the performance NFR-01.
/// </summary>
public class SaigonRideDbContext : DbContext
{
    private readonly byte[]? _aesKey;
    private readonly byte[]? _hmacKey;

    public SaigonRideDbContext(DbContextOptions<SaigonRideDbContext> options)
        : base(options)
    {
    }

    public SaigonRideDbContext(DbContextOptions<SaigonRideDbContext> options, byte[] aesKey, byte[] hmacKey)
        : base(options)
    {
        _aesKey = aesKey;
        _hmacKey = hmacKey;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<LocalCommuterDetails> LocalCommuterDetails => Set<LocalCommuterDetails>();
    public DbSet<ForeignTouristDetails> ForeignTouristDetails => Set<ForeignTouristDetails>();
    public DbSet<VehicleCategory> VehicleCategories => Set<VehicleCategory>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<MaintenanceLog> MaintenanceLogs => Set<MaintenanceLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserPaymentMethod> UserPaymentMethods => Set<UserPaymentMethod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasIndex(u => u.Email).IsUnique();
            b.HasQueryFilter(u => !u.IsDeleted);
            b.Property(u => u.PasswordHash).HasMaxLength(72);
        });

        modelBuilder.Entity<LocalCommuterDetails>(b =>
        {
            b.ToTable("LocalCommuterDetails");
            b.HasKey(x => x.UserId);
            b.HasOne(x => x.User)
                .WithOne(u => u.LocalDetails)
                .HasForeignKey<LocalCommuterDetails>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.NationalId).IsUnique();
        });

        modelBuilder.Entity<ForeignTouristDetails>(b =>
        {
            b.ToTable("ForeignTouristDetails");
            b.HasKey(x => x.UserId);
            b.HasOne(x => x.User)
                .WithOne(u => u.TouristDetails)
                .HasForeignKey<ForeignTouristDetails>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            if (_aesKey is not null && _hmacKey is not null)
            {
                b.Property(x => x.PassportNumber)
                    .HasConversion(new AesEncryptedString(_aesKey, _hmacKey))
                    .HasColumnName("PassportEnc")
                    .HasMaxLength(512);
            }
        });

        modelBuilder.Entity<VehicleCategory>(b =>
        {
            b.ToTable("VehicleCategories");
            b.HasIndex(c => c.Name).IsUnique();
            b.HasQueryFilter(c => !c.IsDeleted);
            b.Property(c => c.RatePerMinVnd).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Station>(b =>
        {
            b.ToTable("Stations", t =>
            {
                t.HasCheckConstraint("CK_Stations_Capacity", "[Capacity] > 0");
                t.HasCheckConstraint("CK_Stations_CurrentCount", "[CurrentCount] >= 0");
            });
            b.HasQueryFilter(s => !s.IsDeleted);
            b.HasIndex(s => new { s.IsActive, s.IsDeleted })
                .IncludeProperties(s => new { s.Capacity, s.CurrentCount })
                .HasDatabaseName("IX_Stations_OccupancyCalc");
        });

        modelBuilder.Entity<Vehicle>(b =>
        {
            b.ToTable("Vehicles");
            b.HasIndex(v => v.LicensePlate).IsUnique();
            b.HasQueryFilter(v => !v.IsDeleted);
            b.HasOne(v => v.Category)
                .WithMany(c => c.Vehicles)
                .HasForeignKey(v => v.VehicleCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(v => v.HomeStation)
                .WithMany(s => s.Vehicles)
                .HasForeignKey(v => v.HomeStationId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(v => new { v.Status, v.HomeStationId })
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_Vehicles_Status_HomeStation");
        });

        modelBuilder.Entity<Rental>(b =>
        {
            b.ToTable("Rentals");
            b.HasOne(r => r.User)
                .WithMany(u => u.Rentals)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.Vehicle)
                .WithMany(v => v.Rentals)
                .HasForeignKey(r => r.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.PickupStation)
                .WithMany()
                .HasForeignKey(r => r.PickupStationId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.ReturnStation)
                .WithMany()
                .HasForeignKey(r => r.ReturnStationId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Property(r => r.RatePerMinSnapshot).HasPrecision(10, 2);
            b.Property(r => r.BaseFare).HasPrecision(12, 2);
            b.Property(r => r.Discount).HasPrecision(12, 2);
            b.Property(r => r.TotalFare).HasPrecision(12, 2);
            b.HasIndex(r => new { r.UserId, r.StartTime })
                .HasDatabaseName("IX_Rentals_UserId_StartTime");
            b.HasIndex(r => r.Status)
                .HasFilter("[Status] = 1")
                .HasDatabaseName("IX_Rentals_Status_Active");
            // BR-01: A user may have at most one active rental at any time.
            // Enforced at DB level to prevent race conditions under concurrent requests.
            b.HasIndex(r => r.UserId)
                .HasFilter("[Status] = 1")
                .IsUnique()
                .HasDatabaseName("IX_Rentals_OneActivePerUser");
        });


        modelBuilder.Entity<UserPaymentMethod>(b =>
        {
            b.ToTable("UserPaymentMethods");
            b.HasIndex(x => new { x.UserId, x.Method }).IsUnique();
            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Transaction>(b =>
        {
            b.ToTable("Transactions");
            b.HasIndex(t => t.RentalId).IsUnique();
            b.HasOne(t => t.Rental)
                .WithOne(r => r.Transaction)
                .HasForeignKey<Transaction>(t => t.RentalId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(t => t.Amount).HasPrecision(12, 2);
            b.Property(t => t.Discount).HasPrecision(12, 2);
            b.HasIndex(t => new { t.PaidAt, t.PaymentMethod })
                .IncludeProperties(t => new { t.Amount, t.Discount })
                .HasDatabaseName("IX_Transactions_PaidAt_Method");
        });

        modelBuilder.Entity<MaintenanceLog>(b =>
        {
            b.ToTable("MaintenanceLogs");
            b.HasOne(m => m.Vehicle)
                .WithMany(v => v.MaintenanceLogs)
                .HasForeignKey(m => m.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLogs");
            b.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(a => new { a.EntityName, a.EntityId, a.LoggedAt });
        });
    }
}

