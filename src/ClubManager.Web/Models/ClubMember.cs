namespace ClubManager.Models;

public class ClubMember
{
    public int Id { get; set; }

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public int OrganizationMemberId { get; set; }
    public OrganizationMember OrganizationMember { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
