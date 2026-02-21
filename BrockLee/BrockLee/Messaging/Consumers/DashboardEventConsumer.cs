using BrockLee.Hubs;
using BrockLee.Messaging;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace BrockLee.Messaging.Consumers;

/// <summary>
/// Consumes DashboardRefreshEvent and pushes to ALL connected
/// Angular dashboard clients via SignalR.
///
/// Replaces Angular's 10-second polling with event-driven push.
/// Dashboard updates instantly when any user submits a form.
/// </summary>
public sealed class DashboardEventConsumer : IConsumer<DashboardRefreshEvent>
{
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<DashboardEventConsumer> _logger;

    public DashboardEventConsumer(
        IHubContext<DashboardHub> hub,
        ILogger<DashboardEventConsumer> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DashboardRefreshEvent> context)
    {
        var msg = context.Message;

        _logger.LogDebug(
            "Broadcasting dashboard refresh: trigger={Trigger}", msg.TriggerType);

        // Broadcast to ALL connected Angular clients
        await _hub.Clients.All.SendAsync("DashboardRefresh", new
        {
            triggerType = msg.TriggerType,
            occurredAt = msg.OccurredAt
        });
    }
}