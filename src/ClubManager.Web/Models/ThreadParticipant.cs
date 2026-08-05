namespace ClubManager.Models;

public class ThreadParticipant
{
    public int Id { get; set; }

    public int ThreadId { get; set; }
    public ClubThread Thread { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.MaxLength(450)]
    public string UserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser User { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
