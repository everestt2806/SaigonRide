using SaigonRide.Domain.Entities;

namespace SaigonRide.Web.Models;

public class HomeViewModel
{
    public int ActiveStationCount { get; set; }
    public int AvailableVehicleCount { get; set; }
    public List<VehicleCategory> Categories { get; set; } = new();
    public List<Station> TopStations { get; set; } = new();
}
