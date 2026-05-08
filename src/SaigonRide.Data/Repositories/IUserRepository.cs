using Microsoft.EntityFrameworkCore;
using SaigonRide.Domain.Entities;

namespace SaigonRide.Data.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByEmailWithDetailsAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(SaigonRideDbContext db) : base(db) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByEmailWithDetailsAsync(string email, CancellationToken ct = default) =>
        Set.Include(u => u.LocalDetails)
           .Include(u => u.TouristDetails)
           .FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default) =>
        Set.Include(u => u.LocalDetails)
           .Include(u => u.TouristDetails)
           .FirstOrDefaultAsync(u => u.Id == id, ct);
}
