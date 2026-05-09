namespace SaigonRide.Services.Audit;

/// <summary>
/// Cross-cutting persistence of audit rows. Every successful service mutation
/// calls <see cref="LogAsync"/> within the same EF transaction (CRC card 14,
/// UC-01 post-conditions, UC-02 post-conditions).
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        int? userId,
        object? detail = null,
        CancellationToken ct = default);
}
