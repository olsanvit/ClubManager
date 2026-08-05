namespace ClubManager.Models;

public class ChatMessageRead
{
    public int Id { get; set; }

    public int MessageId { get; set; }
    public ChatMessage Message { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.MaxLength(450)]
    public string UserId { get; set; } = "";

    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
