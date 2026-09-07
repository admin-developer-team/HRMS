using System.Security.Claims;
using Hrms.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Hrms.Api;

[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new HubException("User identity is missing.");
        await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.ForUser(userId));
        await base.OnConnectedAsync();
    }
}

public sealed class SignalRNotificationPublisher(IHubContext<NotificationsHub> hub, ILogger<SignalRNotificationPublisher> logger) : INotificationPublisher
{
    public async Task PublishChangedAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        try
        {
            foreach (var userId in userIds.Distinct())
                await hub.Clients.Group(NotificationGroups.ForUser(userId.ToString()))
                    .SendAsync("NotificationsChanged", cancellationToken: ct);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Notification records were saved, but the real-time update could not be published.");
        }
    }
}

internal static class NotificationGroups
{
    public static string ForUser(string userId) => $"notifications:{userId}";
}
