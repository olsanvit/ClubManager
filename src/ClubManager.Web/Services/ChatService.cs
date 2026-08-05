using ClubManager.Data;
using ClubManager.Models;
using ClubManager.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Services;

public class ChatService(
    IDbContextFactory<AppDbContextClubManager> factory,
    IHubContext<MessagingHub> hub,
    ChatNotificationDispatcher notifDispatcher)
{
    public async Task<ClubThread> CreateThreadAsync(int clubId, string title, ThreadType type, string createdByUserId)
    {
        await using var db = factory.CreateDbContext();
        var thread = new ClubThread
        {
            ClubId = clubId,
            Title = title,
            ThreadType = type,
            CreatedByUserId = createdByUserId
        };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();
        return thread;
    }

    public async Task<List<ClubThread>> GetClubThreadsAsync(int clubId)
    {
        await using var db = factory.CreateDbContext();
        return await db.Threads
            .Where(t => t.ClubId == clubId && !t.IsArchived)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(int threadId, int skip = 0, int take = 50)
    {
        await using var db = factory.CreateDbContext();
        return await db.ChatMessages
            .Include(m => m.SenderUser)
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync();
    }

    public async Task<ChatMessage> SendMessageAsync(int threadId, string senderUserId, string body)
    {
        await using var db = factory.CreateDbContext();
        var thread = await db.Threads.FindAsync(threadId)
            ?? throw new InvalidOperationException($"Thread {threadId} not found");

        var msg = new ChatMessage { ThreadId = threadId, SenderUserId = senderUserId, Body = body };
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();

        // Načti odesílatele pro broadcast
        var sender = await db.Users.FindAsync(senderUserId);
        var dto = new ChatMessageDto(
            msg.Id, threadId, senderUserId,
            sender?.UserName ?? senderUserId,
            body, msg.CreatedAt);

        await hub.Clients.Group($"thread-{threadId}").SendAsync("NewMessage", dto);
        notifDispatcher.Enqueue(new ChatNotificationJob(threadId, msg.Id));
        return msg;
    }

    public async Task MarkReadAsync(int messageId, string userId)
    {
        await using var db = factory.CreateDbContext();
        var exists = await db.ChatMessageReads.AnyAsync(r => r.MessageId == messageId && r.UserId == userId);
        if (exists) return;
        db.ChatMessageReads.Add(new ChatMessageRead { MessageId = messageId, UserId = userId });
        await db.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(int clubId, string userId)
    {
        await using var db = factory.CreateDbContext();
        var threadIds = await db.Threads
            .Where(t => t.ClubId == clubId && !t.IsArchived)
            .Select(t => t.Id)
            .ToListAsync();

        return await db.ChatMessages
            .Where(m => threadIds.Contains(m.ThreadId))
            .CountAsync(m => !db.ChatMessageReads.Any(r => r.MessageId == m.Id && r.UserId == userId));
    }
}
