using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public enum OrgRole { Member, ClubManager, OrgAdmin }

public class OrganizationMember
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(450)]
    public string UserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser User { get; set; } = null!;

    public OrgRole Role { get; set; } = OrgRole.Member;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(100)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
