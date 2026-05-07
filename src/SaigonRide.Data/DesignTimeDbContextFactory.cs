using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SaigonRide.Data.Converters;

namespace SaigonRide.Data;

/// <summary>
/// Allows <c>dotnet ef migrations add</c> to create a context without booting
/// the Web host. Uses LocalDB and a design-time-only AES key.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SaigonRideDbContext>
{
    public SaigonRideDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SaigonRideDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=SaigonRide_Design;Trusted_Connection=True;MultipleActiveResultSets=true",
                sql => sql.MigrationsAssembly(typeof(SaigonRideDbContext).Assembly.FullName))
            .Options;

        const string designKey = "design-time-only-not-for-production-Phase3-SaigonRide";
        return new SaigonRideDbContext(
            options,
            AesEncryptedString.DeriveAesKey(designKey),
            AesEncryptedString.DeriveHmacKey(designKey));
    }
}
