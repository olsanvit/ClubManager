using ClubManager.Data;
using ClubManager.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Services;

public class CarReservationService
{
    private readonly IDbContextFactory<AppDbContextClubManager> _factory;

    public CarReservationService(IDbContextFactory<AppDbContextClubManager> factory) => _factory = factory;

    // AUDIT:OK
    public async Task<List<Car>> GetCarsAsync(int organizationId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Cars
            .Where(c => c.OrganizationId == organizationId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    // AUDIT:OK
    public async Task<List<CarReservation>> GetReservationsAsync(int organizationId, DateTime? from = null, DateTime? to = null)
    {
        await using var db = _factory.CreateDbContext();
        var q = db.CarReservations
            .Include(r => r.Car)
            .Include(r => r.User)
            .Where(r => r.Car.OrganizationId == organizationId);
        if (from.HasValue) q = q.Where(r => r.DateTo >= from.Value);
        if (to.HasValue) q = q.Where(r => r.DateFrom <= to.Value);
        return await q.OrderBy(r => r.DateFrom).ToListAsync();
    }

    // AUDIT:OK
    public async Task<bool> IsAvailableAsync(int carId, DateTime dateFrom, DateTime dateTo, int? excludeId = null)
    {
        await using var db = _factory.CreateDbContext();
        var q = db.CarReservations.Where(r =>
            r.CarId == carId &&
            r.Status != ReservationStatus.Rejected &&
            r.Status != ReservationStatus.Cancelled &&
            r.DateFrom < dateTo &&
            r.DateTo > dateFrom);
        if (excludeId.HasValue)
            q = q.Where(r => r.Id != excludeId.Value);
        return !await q.AnyAsync();
    }

    // AUDIT:FIXED|byl: race condition — check a insert ve dvou kontextech bez transakce; nyní Serializable tx
    public async Task<CarReservation> CreateAsync(CarReservation reservation)
    {
        await using var db = _factory.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        var conflict = await db.CarReservations.AnyAsync(r =>
            r.CarId == reservation.CarId &&
            r.Status != ReservationStatus.Rejected &&
            r.Status != ReservationStatus.Cancelled &&
            r.DateFrom < reservation.DateTo &&
            r.DateTo > reservation.DateFrom);

        if (conflict)
            throw new InvalidOperationException("Auto je v tomto termínu již rezervováno.");

        reservation.CreatedAt = DateTime.UtcNow;
        db.CarReservations.Add(reservation);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return reservation;
    }

    // AUDIT:OK
    public async Task UpdateStatusAsync(int reservationId, ReservationStatus status)
    {
        await using var db = _factory.CreateDbContext();
        var r = await db.CarReservations.FindAsync(reservationId)
            ?? throw new InvalidOperationException($"Rezervace {reservationId} neexistuje.");
        r.Status = status;
        await db.SaveChangesAsync();
    }

    // AUDIT:OK
    public async Task CompleteAsync(int reservationId, decimal kmAtEnd)
    {
        await using var db = _factory.CreateDbContext();
        var r = await db.CarReservations.FindAsync(reservationId)
            ?? throw new InvalidOperationException($"Rezervace {reservationId} neexistuje.");
        r.KmAtEnd = kmAtEnd;
        r.Status = ReservationStatus.Completed;
        await db.SaveChangesAsync();
    }
}
