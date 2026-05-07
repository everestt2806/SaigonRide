using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaigonRide.Data.Converters;
using SaigonRide.Data.Repositories;

namespace SaigonRide.Data;

/// <summary>
/// Single entry point for the Web project to register all infrastructure
/// without referencing concrete EF types directly. Aligns with NFR-06: the
/// <c>Web</c> project depends on this extension method, never on
/// <c>SaigonRideDbContext</c> from controllers.
/// </summary>
public static class DependencyInjection
{
    public const string DefaultConnectionName = "DefaultConnection";

    public static IServiceCollection AddSaigonRideData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString(DefaultConnectionName)
            ?? throw new InvalidOperationException(
                $"Connection string '{DefaultConnectionName}' is not configured.");

        var encryptionKeyMaterial = configuration["Security:EncryptionKey"]
            ?? throw new InvalidOperationException(
                "Security:EncryptionKey is not configured (required for AES-256 NFR-03).");

        var aesKey  = AesEncryptedString.DeriveAesKey(encryptionKeyMaterial);
        var hmacKey = AesEncryptedString.DeriveHmacKey(encryptionKeyMaterial);

        services.AddSingleton(sp =>
        {
            var builder = new DbContextOptionsBuilder<SaigonRideDbContext>();
            builder.UseSqlServer(connection, sql =>
                sql.MigrationsAssembly(typeof(SaigonRideDbContext).Assembly.FullName));
            return builder.Options;
        });

        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<SaigonRideDbContext>>();
            return new SaigonRideDbContext(options, aesKey, hmacKey);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleCategoryRepository, VehicleCategoryRepository>();
        services.AddScoped<IStationRepository, StationRepository>();
        services.AddScoped<IRentalRepository, RentalRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IMaintenanceLogRepository, MaintenanceLogRepository>();

        return services;
    }
}
