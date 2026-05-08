using SaigonRide.Domain.Entities;

namespace SaigonRide.Data.Repositories;

public interface IAuditLogRepository : IRepository<AuditLog>
{
}

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(SaigonRideDbContext db) : base(db) { }
}
