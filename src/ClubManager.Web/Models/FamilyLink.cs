using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public class FamilyLink
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(450)]
    public string ParentUserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser ParentUser { get; set; } = null!;

    [Required, MaxLength(450)]
    public string ChildUserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser ChildUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
