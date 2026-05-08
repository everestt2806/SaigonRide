using Microsoft.EntityFrameworkCore;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Data.Seed;

/// <summary>
/// Idempotent seed for the demo: 3 categories, 5 stations, 1 admin, 1 local
/// commuter, 1 foreign tourist, and 20 vehicles. The password hasher is
/// injected so this layer stays unaware of <c>BCrypt.Net-Next</c>.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(
        SaigonRideDbContext db,
        Func<string, string> passwordHasher,
        CancellationToken ct = default)
    {
        await SeedCategoriesAsync(db, ct);
        await SeedStationsAsync(db, ct);
        await SeedUsersAsync(db, passwordHasher, ct);
        await SeedVehiclesAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCategoriesAsync(SaigonRideDbContext db, CancellationToken ct)
    {
        if (await db.VehicleCategories.AnyAsync(ct)) return;
        db.VehicleCategories.AddRange(
            new VehicleCategory { Name = "Standard Bike", RatePerMinVnd = 500m },
            new VehicleCategory { Name = "E-Bike",        RatePerMinVnd = 1000m },
            new VehicleCategory { Name = "E-Scooter",     RatePerMinVnd = 1500m });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedStationsAsync(SaigonRideDbContext db, CancellationToken ct)
    {
        if (await db.Stations.AnyAsync(ct)) return;
        db.Stations.AddRange(
            new Station { Name = "Bến Thành Market", Address = "Quận 1, TP HCM",  Latitude = 10.7720, Longitude = 106.6981, Capacity = 30, CurrentCount = 22 },
            new Station { Name = "Thảo Điền",         Address = "Quận 2, TP HCM",  Latitude = 10.8067, Longitude = 106.7378, Capacity = 25, CurrentCount = 4  },
            new Station { Name = "Phú Mỹ Hưng",       Address = "Quận 7, TP HCM",  Latitude = 10.7240, Longitude = 106.7195, Capacity = 25, CurrentCount = 18 },
            new Station { Name = "Bitexco Tower",     Address = "Quận 1, TP HCM",  Latitude = 10.7717, Longitude = 106.7042, Capacity = 20, CurrentCount = 14 },
            new Station { Name = "Landmark 81",       Address = "Bình Thạnh, HCM", Latitude = 10.7951, Longitude = 106.7218, Capacity = 30, CurrentCount = 5  });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedUsersAsync(
        SaigonRideDbContext db,
        Func<string, string> passwordHasher,
        CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var admin = new User
        {
            Email = "admin@saigonride.local",
            FullName = "System Admin",
            PasswordHash = passwordHasher("Admin@123"),
            UserType = UserType.Admin,
            IsActive = true
        };

        var local = new User
        {
            Email = "local@saigonride.local",
            FullName = "Nguyễn Văn Khoa",
            PasswordHash = passwordHasher("Local@123"),
            UserType = UserType.LocalCommuter,
            IsActive = true,
            LocalDetails = new LocalCommuterDetails
            {
                NationalId = "079200012345",
                PhoneNumber = "+84901234567"
            }
        };

        var tourist = new User
        {
            Email = "tourist@saigonride.local",
            FullName = "John Smith",
            PasswordHash = passwordHasher("Tourist@123"),
            UserType = UserType.ForeignTourist,
            IsActive = true,
            TouristDetails = new ForeignTouristDetails
            {
                PassportNumber = "US2024-12345678",
                Nationality = "United States"
            }
        };

        db.Users.AddRange(admin, local, tourist);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedVehiclesAsync(SaigonRideDbContext db, CancellationToken ct)
    {
        if (await db.Vehicles.AnyAsync(ct)) return;

        var categories = await db.VehicleCategories.ToListAsync(ct);
        var stations   = await db.Stations.ToListAsync(ct);
        var random     = new Random(42);

        var bike     = categories.First(c => c.Name == "Standard Bike");
        var ebike    = categories.First(c => c.Name == "E-Bike");
        var escooter = categories.First(c => c.Name == "E-Scooter");

        var vehicles = new List<Vehicle>();
        for (int i = 1; i <= 20; i++)
        {
            var cat = i % 3 == 0 ? escooter : i % 2 == 0 ? ebike : bike;
            var station = stations[random.Next(stations.Count)];
            vehicles.Add(new Vehicle
            {
                LicensePlate = $"51H1-{1000 + i:0000}",
                VehicleCategoryId = cat.Id,
                HomeStationId = station.Id,
                Status = VehicleStatus.Available
            });
        }
        db.Vehicles.AddRange(vehicles);
        await db.SaveChangesAsync(ct);
    }
}
