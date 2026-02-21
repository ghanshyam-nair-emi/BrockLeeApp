using Microsoft.AspNetCore.SignalR;

namespace BrockLee.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates.
///
/// Angular connects on page load.
/// On form submit, Angular sends its SubmissionId.
/// Hub adds the client to a group keyed by SubmissionId.
/// PredictResultConsumer targets that group with the ML result.
///
/// Hub methods the Angular client can call:
///   JoinSubmissionGroup(submissionId) — registers for targeted delivery
///   LeaveSubmissionGroup(submissionId) — cleanup after result received
/// </summary>
public sealed class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called by Angular immediately after form submit.
    /// Groups the connection so PredictResultConsumer can
    /// target just this client with their ML result.
    /// </summary>
    public async Task JoinSubmissionGroup(string submissionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, submissionId);
        _logger.LogDebug(
            "Client {ConnId} joined submission group {SubId}",
            Context.ConnectionId, submissionId);
    }

    /// <summary>
    /// Called by Angular after receiving its prediction result.
    /// Cleans up the group to free server-side resources.
    /// </summary>
    public async Task LeaveSubmissionGroup(string submissionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, submissionId);
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("Dashboard client connected: {ConnId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Dashboard client disconnected: {ConnId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}