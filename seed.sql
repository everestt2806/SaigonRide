-- ============================================================
-- SaigonRide — Phase 3 (Tier 3)
-- Seed script for SQL Server / LocalDB
-- Run AFTER EF Core migration (the app auto-seeds on startup,
-- but this script is provided for manual re-seeding).
-- ============================================================

USE [SaigonRide];
GO

-- Guard: only seed if VehicleCategories is empty
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories)
BEGIN
    SET IDENTITY_INSERT dbo.VehicleCategories ON;
    INSERT INTO dbo.VehicleCategories (Id, Name, RatePerMinVnd, IsDeleted, CreatedAt)
    VALUES
        (1, N'Standard Bike', 500.00,  0, GETUTCDATE()),
        (2, N'E-Bike',        1000.00, 0, GETUTCDATE()),
        (3, N'E-Scooter',     1500.00, 0, GETUTCDATE());
    SET IDENTITY_INSERT dbo.VehicleCategories OFF;
    PRINT 'Seeded 3 vehicle categories.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Stations)
BEGIN
    SET IDENTITY_INSERT dbo.Stations ON;
    INSERT INTO dbo.Stations (Id, Name, Address, Latitude, Longitude, Capacity, CurrentCount, IsActive, IsDeleted, CreatedAt)
    VALUES
        (1, N'Bến Thành Market', N'Quận 1, TP HCM',  10.7720, 106.6981, 30, 22, 1, 0, GETUTCDATE()),
        (2, N'Thảo Điền',       N'Quận 2, TP HCM',  10.8067, 106.7378, 25,  4, 1, 0, GETUTCDATE()),
        (3, N'Phú Mỹ Hưng',     N'Quận 7, TP HCM',  10.7240, 106.7195, 25, 18, 1, 0, GETUTCDATE()),
        (4, N'Bitexco Tower',   N'Quận 1, TP HCM',  10.7717, 106.7042, 20, 14, 1, 0, GETUTCDATE()),
        (5, N'Landmark 81',     N'Bình Thạnh, HCM', 10.7951, 106.7218, 30,  5, 1, 0, GETUTCDATE());
    SET IDENTITY_INSERT dbo.Stations OFF;
    PRINT 'Seeded 5 stations.';
END
GO

-- NOTE: User passwords are BCrypt-hashed; they are auto-seeded by the
-- application's DbSeeder on first startup. The hashes below are samples
-- and will differ on each run due to BCrypt salting.
--
-- Default credentials:
--   admin@saigonride.local / Admin@123    (Admin)
--   local@saigonride.local / Local@123    (Local Commuter)
--   tourist@saigonride.local / Tourist@123 (Foreign Tourist)
--
-- To manually re-seed users, run the application once with an empty database.
-- The DbSeeder in Program.cs handles user + vehicle creation automatically.

PRINT 'Seed script complete. Run the application to seed users and vehicles.';
GO
