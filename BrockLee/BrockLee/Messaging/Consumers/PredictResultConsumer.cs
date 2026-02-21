using BrockLee.Hubs;
using BrockLee.Messaging;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace BrockLee.Messaging.Consumers;

/// <summary>
/// Consumes PredictResultMessage and pushes the ML result
/// to the specific Angular client that made the request.
///
/// Uses SignalR group keyed by SubmissionId so only the
/// requesting client receives their prediction result.
/// </summary>
public sealed class PredictResultConsumer : IConsumer<PredictResultMessage>
{
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<PredictResultConsumer> _logger;

    public PredictResultConsumer(
        IHubContext<DashboardHub> hub,
        ILogger<PredictResultConsumer> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PredictResultMessage> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "Delivering prediction result for SubmissionId={Id} bestModel={Model}",
            msg.SubmissionId, msg.BestModel);

        // Push only to the client group that submitted this request
        await _hub.Clients
            .Group(msg.SubmissionId)
            .SendAsync("PredictionResult", msg);
    }
}