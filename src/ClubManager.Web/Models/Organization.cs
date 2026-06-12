using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public class Organization
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Club> Clubs { get; set; } = [];
    public ICollection<OrganizationMember> Members { get; set; } = [];
    public ICollection<Car> Cars { get; set; } = [];
}
