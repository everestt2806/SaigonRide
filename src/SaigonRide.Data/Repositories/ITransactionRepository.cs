using SaigonRide.Domain.Entities;

namespace SaigonRide.Data.Repositories;

public interface ITransactionRepository : IRepository<Transaction>
{
}

public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(SaigonRideDbContext db) : base(db) { }
}
