using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SaigonRide.Data;
using SaigonRide.Data.Converters;

namespace SaigonRide.Tests.Helpers;

internal static class InMemoryDbHelper
{
    public static SaigonRideDbContext Create(string? name = null)
    {
        var dbName = name ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SaigonRideDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging()
            .Options;
        const string keyMaterial = "test-key-material-for-aes-256-saigonride-tests";
        var aes = AesEncryptedString.DeriveAesKey(keyMaterial);
        var hmac = AesEncryptedString.DeriveHmacKey(keyMaterial);
        return new SaigonRideDbContext(options, aes, hmac);
    }
}
