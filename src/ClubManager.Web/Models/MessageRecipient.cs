using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public enum DeliveryStatus { Pending, Sent, Failed, Read }

public class MessageRecipient
{
    public int Id { get; set; }

    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;

    [Required, MaxLength(450)]
    public string UserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser User { get; set; } = null!;

    public DeliveryStatus EmailStatus { get; set; } = DeliveryStatus.Pending;
    public DeliveryStatus NtfyStatus { get; set; } = DeliveryStatus.Pending;

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
