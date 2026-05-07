namespace SaigonRide.Domain.Enums;

/// <summary>
/// Discriminator for the <see cref="Entities.User"/> hierarchy. See ERD §6.3.1
/// and the overall use-case diagram §3.2 (Local / Tourist generalise BaseUser).
/// </summary>
public enum UserType
{
    LocalCommuter = 1,
    ForeignTourist = 2,
    Admin = 3
}
