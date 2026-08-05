using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public class Invitation
{
    public int Id { get; set; }

    [Required, MaxLength(256)]
    public string Email { get; set; } = "";

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public OrgRole Role { get; set; } = OrgRole.Member;

    [Required, MaxLength(450)]
    public string InvitedByUserId { get; set; } = "";

    [Required, MaxLength(128)]
    public string Token { get; set; } = Guid.NewGuid().ToString();

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime? AcceptedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsAccepted => AcceptedAt.HasValue;
}
