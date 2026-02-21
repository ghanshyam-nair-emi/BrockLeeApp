using BrockLee.Messaging;
using BrockLee.Services;
using MassTransit;

namespace BrockLee.Messaging.Consumers;

/// <summary>
/// Consumes PredictRequestMessage from the predict queue.
///
/// Why this queue exists:
///   Python scikit-learn prediction is CPU-bound (~50–200ms per request).
///   Without a queue, 50 simultaneous users = 50 blocking Python HTTP calls
///   = thread pool exhaustion on the .NET side.
///
///   With this queue:
///   - Requests are accepted instantly (202 Accepted)
///   - PredictConsumer processes them at a controlled concurrency
///   - Results are pushed back to the client via SignalR
///   - No user waits in a blocked HTTP call
/// </summary>
public sealed class PredictConsumer : IConsumer<PredictRequestMessage>
{
    private readonly PythonBridgeService _python;
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<PredictConsumer> _logger;

    public PredictConsumer(
        PythonBridgeService python,
        IPublishEndpoint publish,
        ILogger<PredictConsumer> logger)
    {
        _python = python;
        _publish = publish;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PredictRequestMessage> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Processing prediction for SubmissionId={Id}", msg.SubmissionId);

        try
        {
            var result = await _python.GetPredictionsAsync(new PredictRequest
            {
                Age = msg.Age,
                MonthlyWage = msg.MonthlyWage,
                Inflation = msg.Inflation,
                TotalRemanent = msg.TotalRemanent,
                ExpenseCount = msg.ExpenseCount
            });

            // Publish result — PredictResultConsumer picks it up
            // and pushes to the correct SignalR client
            await _publish.Publish(new PredictResultMessage(
                SubmissionId: msg.SubmissionId,
                BestModel: result.Consensus.BestModel,
                ConsensusNps: result.Consensus.ConsensusNps,
                ConsensusIndex: result.Consensus.ConsensusIndex,
                ModelAgreement: result.Consensus.ModelAgreement,
                Models: result.Models.Select(m => new ModelPredictionSummary(
                    ShortName: m.ShortName,
                    NpsPredicted: m.Nps.PredictedValue,
                    IndexPredicted: m.Index.PredictedValue,
                    Confidence: m.Nps.Confidence
                )).ToList(),
                CompletedAt: DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Prediction failed for SubmissionId={Id}", msg.SubmissionId);
            throw;  // MassTransit will retry based on retry policy
        }
    }
}