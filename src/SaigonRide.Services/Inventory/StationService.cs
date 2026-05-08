using Microsoft.EntityFrameworkCore;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;

namespace SaigonRide.Services.Inventory;

public interface IStationService
{
    Task<List<Station>> ListActiveAsync(CancellationToken ct = default);
    Task<Station?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Station>> ListWithVehicleCountsAsync(CancellationToken ct = default);
    Task<List<Station>> SuggestAlternativesAsync(int forStationId, int take = 3, CancellationToken ct = default);
}

public class StationService : IStationService
{
    private readonly IStationRepository _stations;

    public StationService(IStationRepository stations) => _stations = stations;

    public Task<List<Station>> ListActiveAsync(CancellationToken ct = default) =>
        _stations.Query(tracking: false).Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(ct);

    public Task<Station?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _stations.GetByIdAsync(id, ct);

    public Task<List<Station>> ListWithVehicleCountsAsync(CancellationToken ct = default) =>
        _stations.Query(tracking: false)
            .Include(s => s.Vehicles)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<List<Station>> SuggestAlternativesAsync(int forStationId, int take = 3, CancellationToken ct = default)
    {
        var origin = await _stations.GetByIdAsync(forStationId, ct);
        if (origin is null) return [];
        return await _stations.Query(tracking: false)
            .Where(s => s.Id != forStationId && s.IsActive && s.CurrentCount < s.Capacity)
            .OrderBy(s => Math.Abs(s.Latitude - origin.Latitude) + Math.Abs(s.Longitude - origin.Longitude))
            .Take(take)
            .ToListAsync(ct);
    }
}
