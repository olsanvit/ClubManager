using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public class Car
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(20)]
    public string? LicensePlate { get; set; }

    [MaxLength(100)]
    public string? Brand { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    public int? Year { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CarReservation> Reservations { get; set; } = [];
}
