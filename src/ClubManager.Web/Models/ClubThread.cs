using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public enum ThreadType { General, Announcement, Debt, Event }

public class ClubThread
{
    public int Id { get; set; }

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    [Required, MaxLength(300)]
    public string Title { get; set; } = "";

    public ThreadType ThreadType { get; set; } = ThreadType.General;

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = [];
    public ICollection<ThreadParticipant> Participants { get; set; } = [];
}
