# SaigonRide — Phase 3 (Tier 3 Implementation)

> **Course:** Software Engineering — HK2 2025-2026 · **Instructor:** Ky-Trung Pham  
> **Team:** Nguyễn Văn Sơn (521H0148, UC-01 + RPT-02) · Huỳnh Hữu Minh (520H0473, UC-02 + RPT-01)  
> **Tier:** Tier 3 — ASP.NET MVC Code-First + EF Core 8 + Bootstrap 5.3.

This repository hosts the Phase 3 (Implementation, Testing & Demo) deliverable for the SaigonRide distributed vehicle rental system. It builds on the Phase 1 proposal and the Phase 2 design package — all UML, ERD and architecture decisions live in `../Phase_2/`.

## Solution structure

```
SaigonRide.slnx
├── src/
│   ├── SaigonRide.Domain/      Entities, value objects, enums, business invariants
│   ├── SaigonRide.Data/        DbContext, migrations, repositories, UoW, EF converters
│   ├── SaigonRide.Services/    Business rules, IPaymentGateway strategies, FareCalculator
│   └── SaigonRide.Web/         ASP.NET MVC controllers, Razor views, Bootstrap 5, i18n
└── tests/
    └── SaigonRide.Tests/       xUnit + Moq unit tests (EP/BVA on FareCalculator)
```

Layer reference rule (NFR-06):
- `Web → Services → Domain`
- `Services → Data → Domain`
- `Web` does **not** import `SaigonRide.Data` types in controllers (registration uses an extension method).

## Prerequisites

- .NET SDK 8.0 or 10.0
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`)

## Quick start

```powershell
dotnet restore
dotnet ef database update --project src/SaigonRide.Data --startup-project src/SaigonRide.Web
dotnet run --project src/SaigonRide.Web
```

Default seeded credentials:

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@saigonride.local` | `Admin@123` |
| Local Commuter | `local@saigonride.local` | `Local@123` |
| Foreign Tourist | `tourist@saigonride.local` | `Tourist@123` |

## Tests

```powershell
dotnet test tests/SaigonRide.Tests
```

## Mapping Phase 2 → Phase 3

| Phase 2 artefact | Phase 3 location |
|------------------|------------------|
| §6.2 Class diagram | `src/SaigonRide.Domain/` |
| §6.3 ERD | `src/SaigonRide.Data/SaigonRideDbContext.cs` + migrations |
| §7.1 MVC architecture | `src/SaigonRide.Web/` + DI in `Program.cs` |
| UC-01 main flow | `VehicleController` + `VehicleService` |
| UC-02 main flow | `RentalController` + `RentalService` + `FareCalculator` |
| FR-08 15% discount | `Services/Fares/FareCalculator.cs` |
| FR-09 Strategy pattern | `Services/Payment/*Gateway.cs` |
| NFR-02 BCrypt | `Services/Auth/AuthService.cs` |
| NFR-03 AES-256 | `Data/Converters/AesEncryptedString.cs` |
| RPT-01/RPT-02 | `Controllers/ReportController.cs` + `Services/Reporting/` |

See `docs/Detailed_Design_Justification.md` and `docs/RTM.md` for the full traceability.
