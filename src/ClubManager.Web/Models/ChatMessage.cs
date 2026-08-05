using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public class ChatMessage
{
    public int Id { get; set; }

    public int ThreadId { get; set; }
    public ClubThread Thread { get; set; } = null!;

    [Required, MaxLength(450)]
    public string SenderUserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser SenderUser { get; set; } = null!;

    [Required]
    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    public ICollection<ChatMessageRead> Reads { get; set; } = [];
}
