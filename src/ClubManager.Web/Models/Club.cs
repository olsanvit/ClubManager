using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public class Club
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClubMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
