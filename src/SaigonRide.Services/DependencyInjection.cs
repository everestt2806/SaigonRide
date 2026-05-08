using Microsoft.Extensions.DependencyInjection;
using SaigonRide.Services.Audit;
using SaigonRide.Services.Auth;
using SaigonRide.Services.Fares;
using SaigonRide.Services.Inventory;
using SaigonRide.Services.Payment;
using SaigonRide.Services.Payment.Gateways;
using SaigonRide.Services.Rentals;
using SaigonRide.Services.Reporting;

namespace SaigonRide.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddSaigonRideServices(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IVehicleCategoryService, VehicleCategoryService>();
        services.AddScoped<IStationService, StationService>();

        services.AddSingleton<IFareCalculator>(_ => new FareCalculator());
        services.AddScoped<IRentalService, RentalService>();
        services.AddScoped<IReportService, ReportService>();

        services.AddScoped<IPaymentGateway, MoMoGateway>();
        services.AddScoped<IPaymentGateway, VNPayGateway>();
        services.AddScoped<IPaymentGateway, PayPalGateway>();
        services.AddScoped<IPaymentGateway, ApplePayGateway>();
        services.AddScoped<IPaymentGateway, CashGateway>();
        services.AddScoped<IPaymentService, PaymentService>();

        return services;
    }
}
