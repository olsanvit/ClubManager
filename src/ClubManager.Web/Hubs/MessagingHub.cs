using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ClubManager.Hubs;

[Authorize]
public class MessagingHub : Hub
{
    public async Task JoinThread(string threadId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"thread-{threadId}");

    public async Task LeaveThread(string threadId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"thread-{threadId}");
}

public record ChatMessageDto(int Id, int ThreadId, string SenderUserId, string SenderName, string Body, DateTime CreatedAt);
