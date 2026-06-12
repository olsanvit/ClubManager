using System.ComponentModel.DataAnnotations;

namespace ClubManager.Models;

public enum ReservationStatus { Pending, Approved, Rejected, Completed, Cancelled }

public class CarReservation
{
    public int Id { get; set; }

    public int CarId { get; set; }
    public Car Car { get; set; } = null!;

    [Required, MaxLength(450)]
    public string UserId { get; set; } = "";
    public MercenariesAndBeasts.Infrastructure.AppUser User { get; set; } = null!;

    public DateTime DateFrom { get; set; }

    public DateTime DateTo { get; set; }

    [MaxLength(500)]
    public string? Purpose { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public decimal? KmAtStart { get; set; }

    public decimal? KmAtEnd { get; set; }

    public decimal? KmDriven => KmAtEnd.HasValue && KmAtStart.HasValue
        ? KmAtEnd - KmAtStart
        : null;

    public int DaysDriven => (int)(DateTo - DateFrom).TotalDays + 1;

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
