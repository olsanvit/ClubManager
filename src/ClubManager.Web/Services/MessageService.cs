using ClubManager.Data;
using ClubManager.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Services;

public class MessageService
{
    private readonly IDbContextFactory<AppDbContextClubManager> _factory;
    private readonly ClubNotificationService _notifier;

    public MessageService(IDbContextFactory<AppDbContextClubManager> factory, ClubNotificationService notifier)
    {
        _factory = factory;
        _notifier = notifier;
    }

    public async Task<List<Message>> GetMessagesAsync(int organizationId, int? clubId = null)
    {
        await using var db = _factory.CreateDbContext();
        var q = db.Messages
            .Include(m => m.SenderUser)
            .Include(m => m.Club)
            .Where(m => m.OrganizationId == organizationId);
        if (clubId.HasValue)
            q = q.Where(m => m.ClubId == clubId);
        return await q.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task<Message?> GetMessageAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Messages
            .Include(m => m.SenderUser)
            .Include(m => m.Club)
            .Include(m => m.Recipients).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Message> CreateDraftAsync(Message message)
    {
        await using var db = _factory.CreateDbContext();
        message.Status = MessageStatus.Draft;
        message.CreatedAt = DateTime.UtcNow;
        db.Messages.Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    public async Task SendMessageAsync(int messageId, List<string> recipientUserIds)
    {
        await using var db = _factory.CreateDbContext();
        var msg = await db.Messages.FindAsync(messageId)
            ?? throw new InvalidOperationException($"Message {messageId} not found");

        var existing = await db.MessageRecipients
            .Where(r => r.MessageId == messageId)
            .Select(r => r.UserId)
            .ToListAsync();

        var toAdd = recipientUserIds.Except(existing).ToList();
        foreach (var uid in toAdd)
        {
            db.MessageRecipients.Add(new MessageRecipient
            {
                MessageId = messageId,
                UserId = uid,
                EmailStatus = msg.SendEmail ? DeliveryStatus.Pending : DeliveryStatus.Sent,
                NtfyStatus = msg.SendNtfy ? DeliveryStatus.Pending : DeliveryStatus.Sent
            });
        }

        msg.Status = MessageStatus.Sent;
        msg.SentAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (msg.SendEmail || msg.SendNtfy)
            await DeliverAsync(messageId);
    }

    private async Task DeliverAsync(int messageId)
    {
        await using var db = _factory.CreateDbContext();
        var msg = await db.Messages
            .Include(m => m.Recipients).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.Id == messageId);
        if (msg is null) return;

        foreach (var r in msg.Recipients)
        {
            if (msg.SendEmail && r.EmailStatus == DeliveryStatus.Pending && !string.IsNullOrWhiteSpace(r.User.Email))
            {
                var ok = await _notifier.SendEmailAsync(r.User.Email, r.User.UserName ?? "", msg.Subject, msg.Body);
                r.EmailStatus = ok ? DeliveryStatus.Sent : DeliveryStatus.Failed;
            }
        }

        await db.SaveChangesAsync();
    }
}
