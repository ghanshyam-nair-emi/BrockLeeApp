namespace BrockLee.Messaging;

// ── Queue names ───────────────────────────────────────────────────────────────
// One constant per queue — referenced by producers and consumers alike.
// Changing a queue name here changes it everywhere.

public static class Queues
{
    public const string PredictRequest = "brocklee.predict.request";
    public const string PredictResult = "brocklee.predict.result";
    public const string LogWrite = "brocklee.log.write";
    public const string DashboardEvent = "brocklee.dashboard.event";
}

// ── Messages ──────────────────────────────────────────────────────────────────
// Plain records — no RabbitMQ dependencies in message contracts.
// MassTransit serialises these to JSON automatically.

/// <summary>
/// Published when a user submits the form.
/// Consumed by: PredictConsumer (Python ML call)
///              LogWriteConsumer (Azure SQL write)
///              DashboardEventConsumer (SignalR push)
/// </summary>
public record UserSubmittedEvent(
    string SubmissionId,        // GUID — used to correlate request/response
    string Name,
    int Age,
    double MonthlyWage,
    double Inflation,
    double TotalRemanent,
    int ExpenseCount,
    double NpsRealValue,        // from /returns:compute
    double IndexRealValue,
    double ResponseTimeMs,
    DateTime SubmittedAt
);

/// <summary>
/// Sent to the predict queue when ML predictions are requested.
/// Consumed by: PredictConsumer
/// </summary>
public record PredictRequestMessage(
    string SubmissionId,
    int Age,
    double MonthlyWage,
    double Inflation,
    double TotalRemanent,
    int ExpenseCount
);

/// <summary>
/// Published after ML predictions are computed.
/// Consumed by: PredictResultConsumer → delivers result to waiting client via SignalR
/// </summary>
public record PredictResultMessage(
    string SubmissionId,
    string BestModel,
    double ConsensusNps,
    double ConsensusIndex,
    string ModelAgreement,
    List<ModelPredictionSummary> Models,
    DateTime CompletedAt
);

public record ModelPredictionSummary(
    string ShortName,
    double NpsPredicted,
    double IndexPredicted,
    double Confidence
);

/// <summary>
/// Triggers a real-time dashboard refresh for all connected clients.
/// Published after every form submission + log write.
/// Consumed by: DashboardEventConsumer → SignalR broadcast
/// </summary>
public record DashboardRefreshEvent(
    string TriggerType,     // "submission" | "prediction_complete"
    DateTime OccurredAt
);