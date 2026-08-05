namespace ClubManager.Models;

public enum NotifyMinPriority { All, High, Urgent }

public class NotificationPreference
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(450)]
    public string UserId { get; set; } = "";

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public bool EmailEnabled { get; set; } = true;
    public bool NtfyEnabled { get; set; } = false;
    public NotifyMinPriority MinPriority { get; set; } = NotifyMinPriority.High;
}
