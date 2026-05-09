using System.Text.Json;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;

namespace SaigonRide.Services.Audit;

public class AuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAuditLogRepository _repo;

    public AuditLogger(IAuditLogRepository repo) => _repo = repo;

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        int? userId,
        object? detail = null,
        CancellationToken ct = default)
    {
        var entry = new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            UserId = userId,
            DetailJson = detail is null ? null : JsonSerializer.Serialize(detail, JsonOptions),
            LoggedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(entry, ct);
    }
}
