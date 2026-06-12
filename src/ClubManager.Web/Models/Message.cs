using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public enum MessageType { General, Reminder, Debt, Event }
public enum MessageStatus { Draft, Sent, Cancelled }

public class Message
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public int? ClubId { get; set; }
    public Club? Club { get; set; }

    [Required, MaxLength(450)]
    public string SenderUserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser SenderUser { get; set; } = null!;

    [Required, MaxLength(300)]
    public string Subject { get; set; } = "";

    [Required]
    public string Body { get; set; } = "";

    public MessageType Type { get; set; } = MessageType.General;

    public MessageStatus Status { get; set; } = MessageStatus.Draft;

    public bool SendEmail { get; set; } = true;
    public bool SendNtfy { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public ICollection<MessageRecipient> Recipients { get; set; } = [];
}
