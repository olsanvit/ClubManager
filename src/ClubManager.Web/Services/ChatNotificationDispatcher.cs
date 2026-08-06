using ClubManager.Data;
using ClubManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace ClubManager.Services;

public record ChatNotificationJob(int ThreadId, int MessageId);

public class ChatNotificationDispatcher(
    IDbContextFactory<AppDbContextClubManager> factory,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatNotificationDispatcher> logger) : BackgroundService
{
    private readonly Channel<ChatNotificationJob> _channel = Channel.CreateUnbounded<ChatNotificationJob>();

    public void Enqueue(ChatNotificationJob job) => _channel.Writer.TryWrite(job);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            try { await ProcessAsync(job, ct); }
            catch (Exception ex) { logger.LogError(ex, "Chat notification failed for msg {Id}", job.MessageId); }
        }
    }

    private async Task ProcessAsync(ChatNotificationJob job, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var notifier = scope.ServiceProvider.GetRequiredService<ClubNotificationService>();
        await using var db = factory.CreateDbContext();
        var msg = await db.ChatMessages
            .Include(m => m.Thread).ThenInclude(t => t.Club)
            .Include(m => m.SenderUser)
            .FirstOrDefaultAsync(m => m.Id == job.MessageId, ct);
        if (msg is null) return;

        var isPriority = msg.Thread.ThreadType is ThreadType.Debt or ThreadType.Announcement or ThreadType.Event;

        var members = await db.ClubMembers
            .Include(cm => cm.OrganizationMember)
            .Where(cm => cm.ClubId == msg.Thread.ClubId && cm.IsActive)
            .ToListAsync(ct);

        foreach (var m in members)
        {
            if (m.OrganizationMember.UserId == msg.SenderUserId) continue;

            var pref = await db.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == m.OrganizationMember.UserId && p.ClubId == msg.Thread.ClubId, ct);

            var emailEnabled = msg.Thread.ThreadType == ThreadType.Debt || (pref?.EmailEnabled ?? isPriority);
            var user = await db.Users.FindAsync([m.OrganizationMember.UserId], ct);
            if (user?.Email is null) continue;

            if (emailEnabled)
            {
                await notifier.SendEmailAsync(
                    user.Email, user.UserName ?? user.Email,
                    $"[{msg.Thread.Club.Name}] {msg.Thread.Title}",
                    $"<p><strong>{msg.SenderUser.UserName}</strong> napsal(a) ve vláknu " +
                    $"<em>{msg.Thread.Title}</em>:</p><blockquote>{msg.Body}</blockquote>");
            }
        }
    }
}
